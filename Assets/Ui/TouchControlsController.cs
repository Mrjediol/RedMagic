using System;
using System.Collections.Generic;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Localization;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Controles táctiles en pantalla (izquierda / derecha / abajo / atacar / saltar) para móvil.
    /// Escriben en <see cref="TouchInput"/>, que <c>PlayerMovement</c> combina con el teclado y el mando.
    ///
    /// Reparto de responsabilidades:
    ///  - El contenedor <c>touchControls</c> lleva la clase USS <c>touch-only</c>: el componente
    ///    <see cref="TouchOnlyUI"/> del mismo GameObject lo oculta cuando hay un mando activo.
    ///  - Este script oculta la capa interior mientras el juego está en pausa.
    ///  - Pinta el cooldown de cada acción (<see cref="CooldownDial"/>) sobre su botón: en táctil
    ///    el HUD no enseña casillas, el pulgar ya está aquí. Un botón nuevo con cooldown (parry) =
    ///    una línea <c>AttachDial</c>.
    ///  - Dos esquemas, según <see cref="HudSettings.GestureControls"/> (Opciones ▸ Interfaz): los botones
    ///    clásicos (<c>movePad</c> + <c>actionPad</c>) o los gestos (<c>gestureLayout</c>, lógica en
    ///    <see cref="TouchGestureInput"/>, parámetros en el Inspector ▸ Gestures).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-50)]   // los toques se encolan antes de que el jugador los lea: sin un frame de retraso
    public class TouchControlsController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _inner;
        private Button _leftButton;
        private Button _rightButton;
        private Button _jumpButton;
        private Button _attackButton;
        private Button _downButton;
        private Button _dashButton;
        private Button _weaponButton;
        private Button _parryButton;
        private Button _interactButton;

        [Tooltip("Controles por gestos: zona de movimiento, joystick y dash por doble toque. Se ajusta en vivo en Play.")]
        [SerializeField] private TouchGestureInput gestures = new TouchGestureInput();

        [Tooltip("Margen extra (px del panel) alrededor de saltar / atacar / dash / arma / parry para que un toque " +
                 "al borde también cuente.")]
        [Min(0f)] [SerializeField] private float buttonHitPadding = 24f;

        // Saltar, atacar, dash, arma y parry se leen de los toques directamente (ver TouchTapButtons).
        private readonly TouchTapButtons _tapButtons = new();

        private VisualElement _touchControls, _movePad, _actionPad, _gestureLayout;
        private Button _gestureJumpButton, _gestureAttackButton, _gestureDashButton, _gestureParryButton;
        private bool? _gestureMode;

        // Colocar botones (Opciones ▸ Interfaz ▸ Colocar botones táctiles): el mismo editor que el HUD.
        private HudLayoutEditor _layoutEditor;
        private Action _onLayoutDone;
        private readonly List<(TouchElement element, VisualElement view)> _touchPieces = new();
        private readonly HashSet<VisualElement> _placedPieces = new();

        /// <summary>El controlador táctil activo (no existe en el menú principal), o null.</summary>
        public static TouchControlsController Current { get; private set; }

        /// <summary>True mientras el jugador está colocando los botones táctiles.</summary>
        public static bool IsEditingLayout => Current != null && Current._layoutEditor != null && Current._layoutEditor.IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        // Cooldown pintado sobre el botón que lanza cada acción (el mismo dial que el HUD de PC).
        private readonly List<(CooldownSlot slot, CooldownDial dial)> _dials = new();

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            LocalizedUi.BindTree(_root);
            _inner = _root.Q<VisualElement>("touchControlsInner");
            _leftButton = _root.Q<Button>("leftButton");
            _rightButton = _root.Q<Button>("rightButton");
            _jumpButton = _root.Q<Button>("jumpButton");
            _attackButton = _root.Q<Button>("attackButton");
            _downButton = _root.Q<Button>("downButton");
            _dashButton = _root.Q<Button>("dashButton");
            _weaponButton = _root.Q<Button>("weaponButton");
            _parryButton = _root.Q<Button>("parryButton");
            _interactButton = _root.Q<Button>("interactButton");
            _movePad = _root.Q<VisualElement>("movePad");
            _actionPad = _root.Q<VisualElement>("actionPad");
            _gestureLayout = _root.Q<VisualElement>("gestureLayout");
            _gestureJumpButton = _root.Q<Button>("gestureJumpButton");
            _gestureAttackButton = _root.Q<Button>("gestureAttackButton");
            _gestureDashButton = _root.Q<Button>("gestureDashButton");
            gestures.Bind(_root.Q<VisualElement>("gestureSurface"), _root.Q<VisualElement>("gestureZone"),
                          _root.Q<VisualElement>("gestureStick"), _root.Q<VisualElement>("gestureKnob"),
                          _root.Q<VisualElement>("gestureAim"));
            _gestureParryButton = _root.Q<Button>("gestureParryButton");
            _touchControls = _root.Q<VisualElement>("touchControls");
            if (_interactButton != null) _interactButton.style.display = DisplayStyle.None;
            BuildLayoutEditor();
            Current = this;

            WireHold(_leftButton, -1f);
            WireHold(_rightButton, 1f);

            _tapButtons.Disable();
            _tapButtons.Add(_jumpButton, TouchInput.QueueJump, TouchInput.QueueJumpRelease);
            _tapButtons.Add(_gestureJumpButton, TouchInput.QueueJump, TouchInput.QueueJumpRelease);
            _tapButtons.Add(_attackButton, TouchInput.QueueAttack);
            _tapButtons.Add(_gestureAttackButton, TouchInput.QueueAttack);
            _tapButtons.Add(_dashButton, TouchInput.QueueDash);
            _tapButtons.Add(_gestureDashButton, TouchInput.QueueDash);
            _tapButtons.Add(_weaponButton, TouchInput.QueueWeapon);
            _tapButtons.Add(_parryButton, TouchInput.QueueParry);
            _tapButtons.Add(_gestureParryButton, TouchInput.QueueParry);
            // Interactuar: la pulsación (comprar, abrir…) y el estado mantenido (reciclar un item del suelo).
            _tapButtons.Add(_interactButton, OnInteractPress, OnInteractRelease);
            _tapButtons.Enable();

            if (_downButton != null)
            {
                _downButton.RegisterCallback<PointerDownEvent>(OnDownDown);
                _downButton.RegisterCallback<PointerUpEvent>(OnDownUp);
                _downButton.RegisterCallback<PointerLeaveEvent>(OnDownLeave);
                _downButton.RegisterCallback<PointerCaptureOutEvent>(OnDownCaptureOut);
            }

            _wasPaused = null;   // la pausa se sondea en Update (ver UpdatePause)

            AttachDial(_attackButton, CooldownSlot.Attack);
            AttachDial(_dashButton, CooldownSlot.Dash);
            AttachDial(_parryButton, CooldownSlot.Parry);
            AttachDial(_weaponButton, CooldownSlot.Weapon);
            AttachDial(_gestureAttackButton, CooldownSlot.Attack);
            AttachDial(_gestureDashButton, CooldownSlot.Dash);
            AttachDial(_gestureParryButton, CooldownSlot.Parry);

            HudSettings.Changed -= ApplyScheme;
            HudSettings.Changed += ApplyScheme;
            _gestureMode = null;
            ApplyScheme();

            TouchInput.Clear();
        }

        // ------------------------------------------------------------------ esquema (botones / gestos)

        private void ApplyScheme()
        {
            bool gesture = HudSettings.GestureControls;
            if (_gestureMode == gesture) return;
            _gestureMode = gesture;

            RefreshVisibility();
            gestures.Reset();
            _tapButtons.Release();
            TouchInput.Clear();
        }

        /// <summary>
        /// Qué capa se ve: el esquema (botones o gestos), la pausa y el modo táctil. Colocando botones se fuerza la
        /// de gestos aunque el juego esté en pausa (Opciones) o no se esté en táctil.
        /// </summary>
        private void RefreshVisibility()
        {
            bool editing = _layoutEditor != null && _layoutEditor.IsOpen;
            bool paused = GameStateManager.Instance != null && GameStateManager.Instance.IsPaused;
            bool gesture = _gestureMode == true || editing;

            SetDisplay(_touchControls, editing || InputDeviceManager.TouchActive);
            SetDisplay(_inner, editing || !paused);
            SetDisplay(_movePad, !gesture);
            SetDisplay(_actionPad, !gesture);
            SetDisplay(_gestureLayout, gesture);

            // Con gestos USAR vive entre los botones sueltos (se puede recolocar); si no, en su sitio clásico.
            if (_interactButton != null)
            {
                var parent = gesture ? _gestureLayout : _inner;
                if (parent != null && _interactButton.parent != parent) parent.Add(_interactButton);
                _interactButton.EnableInClassList("touch-button--interact-gesture", gesture);
                _layoutEditor?.BringToFront();
            }
        }

        private static void SetDisplay(VisualElement element, bool show)
        {
            if (element == null) return;
            var display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        // ------------------------------------------------------------------ colocar botones (gestos)

        private void BuildLayoutEditor()
        {
            _touchPieces.Clear();
            _placedPieces.Clear();
            _layoutEditor = null;
            if (_gestureLayout == null) return;

            _touchPieces.Add((TouchElement.Dash, _gestureDashButton));
            _touchPieces.Add((TouchElement.Attack, _gestureAttackButton));
            _touchPieces.Add((TouchElement.Jump, _gestureJumpButton));
            _touchPieces.Add((TouchElement.Parry, _gestureParryButton));
            _touchPieces.Add((TouchElement.Interact, _interactButton));

            // OnEnable puede repetirse sobre el mismo árbol: un solo editor.
            _gestureLayout.Q<HudLayoutEditor>()?.RemoveFromHierarchy();
            _layoutEditor = new HudLayoutEditor(_gestureLayout, "touch.layout.title", "touch.layout.hint", HudSettings.ResetTouchAnchors);
            foreach (var (element, view) in _touchPieces)
            {
                if (view == null) continue;
                var key = element;
                _layoutEditor.Register(view, LabelKey(element),
                                       placed => HudSettings.SetTouchAnchor(key, GameHud.AnchorFromRect(placed, _gestureLayout.layout)));
            }
            _layoutEditor.Finished += EndLayoutEdit;
            _gestureLayout.Add(_layoutEditor);
        }

        // Claves literales para que la auditoría de idiomas las vea.
        private static string LabelKey(TouchElement element) => element switch
        {
            TouchElement.Jump => "touch.jump",
            TouchElement.Attack => "touch.attack",
            TouchElement.Dash => "touch.dash",
            TouchElement.Parry => "touch.parry",
            _ => "touch.interact",
        };

        /// <summary>
        /// Abre "colocar botones táctiles" (los de los controles por gestos). <paramref name="onDone"/> se llama al
        /// terminar (Listo o Escape). Devuelve false si los controles táctiles no existen (menú principal).
        /// </summary>
        public static bool BeginLayoutEdit(Action onDone)
        {
            var self = Current;
            if (self == null || self._layoutEditor == null) return false;
            self._onLayoutDone = onDone;
            self.gestures.Reset();
            self._tapButtons.Release();
            self._layoutEditor.Open();
            self.RefreshVisibility();
            return true;
        }

        /// <summary>Cierra "colocar botones táctiles" (lo soltado ya está guardado).</summary>
        public static void EndLayoutEdit()
        {
            if (!IsEditingLayout) return;
            var self = Current;
            self._layoutEditor.Close();
            self.RefreshVisibility();
            var done = self._onLayoutDone;
            self._onLayoutDone = null;
            done?.Invoke();
        }

        /// <summary>
        /// Pone cada botón de gestos donde lo dejó el jugador (<see cref="HudSettings.TryGetTouchAnchor"/>); sin
        /// posición guardada vuelve a la de fábrica del USS (right / bottom).
        /// </summary>
        private void ApplyTouchLayout()
        {
            var area = _gestureLayout.layout;
            if (float.IsNaN(area.width) || area.width <= 0f) return;

            foreach (var (element, view) in _touchPieces)
            {
                if (view == null) continue;
                bool dragging = _layoutEditor != null && view == _layoutEditor.Dragging;
                bool saved = HudSettings.TryGetTouchAnchor(element, out var anchor);

                if (view.parent != _gestureLayout || (!saved && !dragging))
                {
                    if (_placedPieces.Remove(view))
                    {
                        view.style.left = StyleKeyword.Null;
                        view.style.top = StyleKeyword.Null;
                        view.style.right = StyleKeyword.Null;
                        view.style.bottom = StyleKeyword.Null;
                    }
                    continue;
                }

                // Colocada a mano: manda left/top, el right/bottom de fábrica se anula.
                if (_placedPieces.Add(view))
                {
                    view.style.left = view.layout.x;   // desde donde está, sin saltar a 0,0 antes del primer arrastre
                    view.style.top = view.layout.y;
                    view.style.right = StyleKeyword.Auto;
                    view.style.bottom = StyleKeyword.Auto;
                }
                if (dragging) continue;   // la mueve el editor

                var size = view.layout.size;
                if (float.IsNaN(size.x) || size.x <= 0f) continue;
                var place = GameHud.PlaceFromAnchor(anchor, area, size);
                if (Mathf.Abs(view.resolvedStyle.left - place.x) > 0.5f) view.style.left = place.x;
                if (Mathf.Abs(view.resolvedStyle.top - place.y) > 0.5f) view.style.top = place.y;
            }
        }

        // ------------------------------------------------------------------ cooldowns

        private void AttachDial(Button button, CooldownSlot slot)
        {
            if (button == null || button.Q<CooldownDial>() != null) return;   // OnEnable puede repetirse
            var dial = new CooldownDial { Accent = HudStyle.Accent(slot), RingWidth = 7f };
            button.Add(dial);
            _dials.Add((slot, dial));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdatePause();
            bool editing = IsEditingLayout && Current == this;
            if (_gestureMode == true) gestures.Tick();
            _tapButtons.HitPadding = buttonHitPadding;
            if (editing)
            {
                RefreshVisibility();   // TouchOnlyUI la ocultaría al usar el ratón
                _layoutEditor.Tick();
            }
            else _tapButtons.Tick(_root?.panel);   // colocando, tocar un botón no lo pulsa
            if (_gestureLayout != null && _gestureLayout.resolvedStyle.display == DisplayStyle.Flex) ApplyTouchLayout();

            // Interactuar sólo con algo a mano: el cartel de interacción a la vista o un interactuable que lo pide.
            // Colocando botones se enseña siempre, para poder moverlo.
            if (_interactButton != null)
            {
                bool wanted = editing || TouchInput.InteractButtonWanted || Hub.InteractionPromptUi.IsVisible;
                var display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
                if (_interactButton.style.display != display) _interactButton.style.display = display;
                if (!wanted) TouchInput.InteractHeld = false;
            }

            // El botón del arma sólo con un arma equipada (sin arma no hace nada).
            if (_weaponButton != null)
            {
                bool armed = HudPlayer.Cooldown(CooldownSlot.Weapon) != null;
                var display = armed ? DisplayStyle.Flex : DisplayStyle.None;
                if (_weaponButton.style.display != display) _weaponButton.style.display = display;
            }
            foreach (var (slot, dial) in _dials)
            {
                var source = HudPlayer.Cooldown(slot);
                if (source == null)
                {
                    if (!dial.Ready) dial.ResetState();
                    continue;
                }
                dial.Tick(source.Cooldown, dt);
            }
        }

        private void OnDisable()
        {
            UnwireHold(_leftButton);
            UnwireHold(_rightButton);

            if (IsEditingLayout && Current == this) EndLayoutEdit();
            if (Current == this) Current = null;

            _tapButtons.Disable();
            TouchInput.InteractHeld = false;
            gestures.Unbind();
            HudSettings.Changed -= ApplyScheme;

            if (_downButton != null)
            {
                _downButton.UnregisterCallback<PointerDownEvent>(OnDownDown);
                _downButton.UnregisterCallback<PointerUpEvent>(OnDownUp);
                _downButton.UnregisterCallback<PointerLeaveEvent>(OnDownLeave);
                _downButton.UnregisterCallback<PointerCaptureOutEvent>(OnDownCaptureOut);
            }

            TouchInput.Clear();
        }

        // ------------------------------------------------------------------ pulsación mantenida

        private void WireHold(Button button, float value)
        {
            if (button == null) return;

            button.userData = value;
            button.RegisterCallback<PointerDownEvent>(OnMoveDown);
            button.RegisterCallback<PointerUpEvent>(OnMoveUp);
            button.RegisterCallback<PointerLeaveEvent>(OnMoveLeave);
            button.RegisterCallback<PointerCaptureOutEvent>(OnMoveCaptureOut);
        }

        private void UnwireHold(Button button)
        {
            if (button == null) return;

            button.UnregisterCallback<PointerDownEvent>(OnMoveDown);
            button.UnregisterCallback<PointerUpEvent>(OnMoveUp);
            button.UnregisterCallback<PointerLeaveEvent>(OnMoveLeave);
            button.UnregisterCallback<PointerCaptureOutEvent>(OnMoveCaptureOut);
        }

        private static void OnMoveDown(PointerDownEvent evt)
        {
            if (evt.currentTarget is VisualElement ve && ve.userData is float value)
                TouchInput.Horizontal = value;
        }

        private static void OnMoveUp(PointerUpEvent evt) => Release(evt.currentTarget);

        private static void OnMoveLeave(PointerLeaveEvent evt) => Release(evt.currentTarget);

        private static void OnMoveCaptureOut(PointerCaptureOutEvent evt) => Release(evt.currentTarget);

        private static void Release(IEventHandler target)
        {
            // Sólo se suelta si el botón liberado es el que marcaba la dirección actual, para no
            // cancelar la otra dirección cuando se pulsan las dos a la vez.
            if (target is VisualElement ve && ve.userData is float value &&
                Mathf.Approximately(TouchInput.Horizontal, value))
            {
                TouchInput.Horizontal = 0f;
            }
        }

        // ------------------------------------------------------------------ abajo (atravesar plataformas)

        private static void OnInteractPress() { TouchInput.QueueInteract(); TouchInput.InteractHeld = true; }

        private static void OnInteractRelease() => TouchInput.InteractHeld = false;

        private static void OnDownDown(PointerDownEvent evt) => TouchInput.Down = true;

        private static void OnDownUp(PointerUpEvent evt) => TouchInput.Down = false;

        private static void OnDownLeave(PointerLeaveEvent evt) => TouchInput.Down = false;

        private static void OnDownCaptureOut(PointerCaptureOutEvent evt) => TouchInput.Down = false;

        // ------------------------------------------------------------------ pausa

        private bool? _wasPaused;

        /// <summary>
        /// Se sondea cada frame en vez de suscribirse a <c>GameStateManager.PausedChanged</c>: este objeto
        /// (Resources/MenusUI) nace antes de la escena y el GameStateManager vive en ella, así que en OnEnable aún no
        /// existe y el evento no llegaba nunca — al salir de la pausa la capa táctil se quedaba oculta.
        /// </summary>
        private void UpdatePause()
        {
            bool paused = GameStateManager.Instance != null && GameStateManager.Instance.IsPaused;
            if (_wasPaused == paused) return;
            _wasPaused = paused;
            OnPausedChanged(paused);
        }

        private void OnPausedChanged(bool paused)
        {
            RefreshVisibility();

            if (!paused) return;
            gestures.Reset();
            _tapButtons.Release();
            TouchInput.Clear();
        }
    }
}
