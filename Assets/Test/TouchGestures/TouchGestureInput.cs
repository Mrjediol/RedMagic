using System;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Controles táctiles por gestos (alternativa a los botones de <see cref="TouchControlsController"/>).
    /// Una superficie a pantalla completa, debajo de los botones de acción, reparte cada dedo según dónde cae:
    /// <list type="bullet">
    /// <item><b>Zona de movimiento</b> (abajo a la izquierda): joystick flotante. El origen es donde se toca;
    /// arrastrar a un lado anda a tope hacia ese lado, abajo atraviesa plataformas. Soltar andando y volver a
    /// tocar enseguida cerca del mismo sitio = <b>dash</b>, y se sigue andando hacia el mismo lado.</item>
    /// <item><b>Resto de la pantalla</b>: el arma. Si el arma equipada apunta al cursor, ahí aparece un <b>joystick de
    /// apuntado</b> (el origen es donde se toca): arrastrar elige la dirección del disparo
    /// (<see cref="TouchInput.AimDirection"/>) y mantiene el arma; un toque sin arrastrar dispara hacia donde se mira. Si
    /// no apunta al cursor, se mantiene el arma tocando en cualquier sitio (<see cref="TouchInput.WeaponHeld"/>,
    /// <see cref="TouchInput.AimScreen"/>). Con arma de carga, soltar con la carga completa dispara.</item>
    /// </list>
    /// Distancias en píxeles del panel (referencia 2688×1216 de TouchControlsPanelSettings).
    /// </summary>
    [Serializable]
    public class TouchGestureInput
    {
        [Header("Zona de movimiento")]
        [Tooltip("Ancho de la zona de movimiento, en fracción de la pantalla desde la izquierda.")]
        [Range(0.1f, 1f)] public float moveZoneWidth = 0.336f;

        [Tooltip("Alto de la zona de movimiento, en fracción de la pantalla desde abajo.")]
        [Range(0.1f, 1f)] public float moveZoneHeight = 0.375f;

        [Tooltip("Pintar la zona de movimiento (para probar). Apagado = invisible.")]
        public bool showMoveZone = true;

        [Tooltip("Opacidad de la zona y del joystick de movimiento (0 = invisibles, siguen funcionando).")]
        [Range(0f, 1f)] public float moveAlpha = 0f;

        [Header("Joystick")]
        [Tooltip("Píxeles de arrastre horizontal antes de empezar a andar.")]
        [Min(0f)] public float deadzone = 30f;

        [Tooltip("Arrastre máximo desde el origen. Más allá, el origen sigue al dedo: volver hacia el otro lado " +
                 "cambia de dirección tras recorrer sólo 'deadzone', no todo lo arrastrado.")]
        [Min(1f)] public float stickRadius = 140f;

        [Tooltip("Arrastrar hacia abajo atraviesa plataformas.")]
        public bool swipeDownDropsThrough = true;

        [Tooltip("Píxeles hacia abajo para atravesar plataformas.")]
        [Min(1f)] public float dropThreshold = 110f;

        [Header("Joystick de apuntado (armas que apuntan al cursor)")]
        [Tooltip("Píxeles de arrastre antes de fijar la dirección. Por debajo, soltar dispara hacia donde se mira.")]
        [Min(0f)] public float aimDeadzone = 25f;

        [Tooltip("Arrastre máximo desde el origen; más allá, el origen sigue al dedo.")]
        [Min(1f)] public float aimStickRadius = 110f;

        [Header("Dash (soltar y volver a tocar)")]
        [Tooltip("Segundos máximos entre soltar el dedo andando y volver a tocar para que sea dash.")]
        [Min(0f)] public float retapWindow = 0.25f;

        [Tooltip("Distancia máxima entre donde se soltó y donde se vuelve a tocar.")]
        [Min(0f)] public float retapRadius = 200f;

        private VisualElement _surface, _zone, _stick, _knob, _aim, _aimStick, _aimKnob;

        private int _movePointer = -1;
        private Vector2 _origin;
        private Vector2 _offset;        // dedo − origen, ya recortado a stickRadius
        private Vector2 _movePos;

        private float _releaseTime = -10f;
        private Vector2 _releasePos;
        private Vector2 _releaseOffset;
        private float _releaseDir;

        private int _weaponPointer = -1;
        private Vector2 _weaponPos;
        private Vector2 _weaponPanelPos;
        private bool _aimWithStick;     // este dedo es el joystick de apuntado
        private bool _aimChosen;        // ya pasó la zona muerta: hay dirección
        private Vector2 _aimOrigin;

        /// <summary>Engancha la superficie y los elementos visuales (todos menos la superficie pueden ser null).</summary>
        public void Bind(VisualElement surface, VisualElement zone, VisualElement stick, VisualElement knob, VisualElement aim,
                         VisualElement aimStick = null, VisualElement aimKnob = null)
        {
            Unbind();
            _surface = surface;
            _zone = zone;
            _stick = stick;
            _knob = knob;
            _aim = aim;
            _aimStick = aimStick;
            _aimKnob = aimKnob;
            if (_surface == null) return;

            _surface.RegisterCallback<PointerDownEvent>(OnDown);
            _surface.RegisterCallback<PointerMoveEvent>(OnMove);
            _surface.RegisterCallback<PointerUpEvent>(OnUp);
            _surface.RegisterCallback<PointerCancelEvent>(OnCancel);
            _surface.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            Reset();
        }

        public void Unbind()
        {
            if (_surface != null)
            {
                _surface.UnregisterCallback<PointerDownEvent>(OnDown);
                _surface.UnregisterCallback<PointerMoveEvent>(OnMove);
                _surface.UnregisterCallback<PointerUpEvent>(OnUp);
                _surface.UnregisterCallback<PointerCancelEvent>(OnCancel);
                _surface.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            }
            Reset();
            _surface = null;
        }

        /// <summary>Suelta todos los dedos (pausa, cambio de esquema, desactivar).</summary>
        public void Reset()
        {
            if (_surface != null)
            {
                if (_movePointer >= 0 && _surface.HasPointerCapture(_movePointer)) _surface.ReleasePointer(_movePointer);
                if (_weaponPointer >= 0 && _surface.HasPointerCapture(_weaponPointer)) _surface.ReleasePointer(_weaponPointer);
            }
            if (_movePointer >= 0) { TouchInput.Horizontal = 0f; TouchInput.Down = false; }
            if (_weaponPointer >= 0) TouchInput.WeaponHeld = false;
            _movePointer = -1;
            _weaponPointer = -1;
            _releaseTime = -10f;
            SetVisible(_stick, false);
            SetVisible(_knob, false);
            SetVisible(_aim, false);
            SetVisible(_aimStick, false);
            SetVisible(_aimKnob, false);
        }

        /// <summary>Cada frame: tamaño y visibilidad de la zona (se ajusta en vivo desde el Inspector).</summary>
        public void Tick()
        {
            if (_stick != null) _stick.style.opacity = moveAlpha;
            if (_knob != null) _knob.style.opacity = moveAlpha;
            if (_zone == null) return;
            SetVisible(_zone, showMoveZone && moveAlpha > 0f);
            _zone.style.opacity = moveAlpha;
            _zone.style.width = Length.Percent(moveZoneWidth * 100f);
            _zone.style.height = Length.Percent(moveZoneHeight * 100f);
        }

        // ------------------------------------------------------------------ eventos

        private void OnDown(PointerDownEvent evt)
        {
            Vector2 pos = evt.localPosition;

            if (InMoveZone(pos) && _movePointer < 0)
            {
                _movePointer = evt.pointerId;

                bool retap = _releaseDir != 0f
                             && Time.unscaledTime - _releaseTime <= retapWindow
                             && (pos - _releasePos).sqrMagnitude <= retapRadius * retapRadius;
                _releaseTime = -10f;

                // Dash: se recupera el desplazamiento de antes de soltar, así se sigue andando hacia el mismo lado.
                _origin = retap ? pos - _releaseOffset : pos;
                if (retap) TouchInput.QueueDash();

                ApplyMove(pos);
                SetVisible(_stick, true);
                SetVisible(_knob, true);
            }
            else if (!InMoveZone(pos) && _weaponPointer < 0)
            {
                _weaponPointer = evt.pointerId;
                _weaponPos = pos;
                _weaponPanelPos = evt.position;
                _aimWithStick = AimsWithStick();
                if (_aimWithStick)
                {
                    // joystick de apuntado: aún no hay dirección; no se dispara hasta arrastrar (o al soltar, si es un toque)
                    _aimOrigin = pos;
                    _aimChosen = false;
                    PlaceAimStick(Vector2.zero);
                    SetVisible(_aimStick, true);
                    SetVisible(_aimKnob, true);
                }
                else
                {
                    TouchInput.AimDirection = null;
                    TouchInput.AimScreen = PanelToScreen(evt.position);
                    TouchInput.WeaponHeld = true;
                    TouchInput.QueueWeapon();   // un toque más corto que un frame también dispara un arma sin carga
                    PlaceAim();
                    SetVisible(_aim, true);
                }
            }
            else return;

            _surface.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == _movePointer) ApplyMove(evt.localPosition);
            else if (evt.pointerId == _weaponPointer)
            {
                _weaponPos = evt.localPosition;
                _weaponPanelPos = evt.position;
                if (_aimWithStick) { ApplyAim(evt.localPosition); return; }
                TouchInput.AimScreen = PanelToScreen(evt.position);
                PlaceAim();
            }
        }

        private void OnUp(PointerUpEvent evt) => Release(evt.pointerId, evt.localPosition, evt.position);

        private void OnCancel(PointerCancelEvent evt) => Release(evt.pointerId, evt.localPosition, evt.position);

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _movePointer) Release(_movePointer, _movePos, Vector2.zero);
            else if (evt.pointerId == _weaponPointer) Release(_weaponPointer, _weaponPos, _weaponPanelPos);
        }

        private void Release(int pointerId, Vector2 pos, Vector2 panelPos)
        {
            if (pointerId == _movePointer)
            {
                _movePointer = -1;
                _releaseTime = Time.unscaledTime;
                _releasePos = pos;
                _releaseOffset = _offset;
                _releaseDir = TouchInput.Horizontal;
                TouchInput.Horizontal = 0f;
                TouchInput.Down = false;
                SetVisible(_stick, false);
                SetVisible(_knob, false);
            }
            else if (pointerId == _weaponPointer)
            {
                _weaponPointer = -1;
                if (_aimWithStick)
                {
                    // un toque sin arrastrar: un disparo hacia donde se mira. Si hubo dirección, se queda guardada para el
                    // disparo de esta suelta (armas de carga) y el siguiente toque la vuelve a elegir.
                    if (!_aimChosen) { TouchInput.AimDirection = null; TouchInput.QueueWeapon(); }
                    SetVisible(_aimStick, false);
                    SetVisible(_aimKnob, false);
                }
                else
                {
                    TouchInput.AimScreen = PanelToScreen(panelPos);
                    SetVisible(_aim, false);
                }
                TouchInput.WeaponHeld = false;
            }
            else return;

            if (_surface != null && _surface.HasPointerCapture(pointerId)) _surface.ReleasePointer(pointerId);
        }

        // ------------------------------------------------------------------ movimiento

        private void ApplyMove(Vector2 pos)
        {
            _movePos = pos;
            Vector2 d = pos - _origin;

            // Más allá del radio el origen sigue al dedo (cambiar de lado es inmediato).
            float maxY = Mathf.Max(stickRadius, dropThreshold);
            if (Mathf.Abs(d.x) > stickRadius) { _origin.x = pos.x - Mathf.Sign(d.x) * stickRadius; d.x = Mathf.Sign(d.x) * stickRadius; }
            if (Mathf.Abs(d.y) > maxY) { _origin.y = pos.y - Mathf.Sign(d.y) * maxY; d.y = Mathf.Sign(d.y) * maxY; }
            _offset = d;

            TouchInput.Horizontal = Mathf.Abs(d.x) < deadzone ? 0f : Mathf.Sign(d.x);
            TouchInput.Down = swipeDownDropsThrough && d.y >= dropThreshold;   // y del panel crece hacia abajo

            PlaceStick();
        }

        // ------------------------------------------------------------------ apuntar

        /// <summary>¿El arma equipada apunta al cursor? (modo MouseDirection, o el cometa con apuntar al cursor).</summary>
        private static bool AimsWithStick()
        {
            var user = RedMagic.Items.WeaponUser.Current;
            var weapon = user != null ? user.Weapon : null;
            if (weapon == null || weapon.Shot == null) return false;
            return weapon.IsComet
                ? RedMagic.Items.CometProjectile.AimsAtCursor(RedMagic.Items.CometRuntime.For(weapon), weapon.Shot.aimMode)
                : weapon.Shot.aimMode == RedMagic.Gameplay.ProjectileAimMode.MouseDirection;
        }

        private void ApplyAim(Vector2 pos)
        {
            Vector2 d = pos - _aimOrigin;
            if (d.magnitude > aimStickRadius) { _aimOrigin = pos - d.normalized * aimStickRadius; d = d.normalized * aimStickRadius; }
            if (d.magnitude >= aimDeadzone)
            {
                TouchInput.AimDirection = new Vector2(d.x, -d.y).normalized;   // y del panel crece hacia abajo
                if (!_aimChosen) { _aimChosen = true; TouchInput.WeaponHeld = true; }
            }
            PlaceAimStick(d);
        }

        private void PlaceAimStick(Vector2 offset)
        {
            if (_aimStick != null)
            {
                _aimStick.style.width = _aimStick.style.height = aimStickRadius * 2f;
                _aimStick.style.borderTopLeftRadius = _aimStick.style.borderTopRightRadius =
                    _aimStick.style.borderBottomLeftRadius = _aimStick.style.borderBottomRightRadius = aimStickRadius;
                _aimStick.style.left = _aimOrigin.x - aimStickRadius;
                _aimStick.style.top = _aimOrigin.y - aimStickRadius;
            }
            if (_aimKnob != null)
            {
                float half = HalfSize(_aimKnob, 90f);
                _aimKnob.style.left = _aimOrigin.x + offset.x - half;
                _aimKnob.style.top = _aimOrigin.y + offset.y - half;
                _aimKnob.EnableInClassList("gesture-knob--active", _aimChosen);
            }
        }

        private bool InMoveZone(Vector2 local)
        {
            if (_surface == null) return false;
            var rect = _surface.layout;
            return local.x <= rect.width * moveZoneWidth && local.y >= rect.height * (1f - moveZoneHeight);
        }

        // ------------------------------------------------------------------ visual

        private void PlaceStick()
        {
            if (_stick != null)
            {
                _stick.style.width = _stick.style.height = stickRadius * 2f;
                _stick.style.borderTopLeftRadius = _stick.style.borderTopRightRadius =
                    _stick.style.borderBottomLeftRadius = _stick.style.borderBottomRightRadius = stickRadius;
                _stick.style.left = _origin.x - stickRadius;
                _stick.style.top = _origin.y - stickRadius;
            }
            if (_knob != null)
            {
                float half = HalfSize(_knob, 110f);
                _knob.style.left = _origin.x + _offset.x - half;
                _knob.style.top = _origin.y + _offset.y - half;
                _knob.EnableInClassList("gesture-knob--active", TouchInput.Horizontal != 0f || TouchInput.Down);
            }
        }

        private void PlaceAim()
        {
            if (_aim == null) return;
            float half = HalfSize(_aim, 90f);
            _aim.style.left = _weaponPos.x - half;
            _aim.style.top = _weaponPos.y - half;
        }

        // Recién mostrado, el elemento aún no tiene layout: se usa el tamaño del USS.
        private static float HalfSize(VisualElement element, float fallback)
        {
            float w = element.resolvedStyle.width;
            return (w > 0f && !float.IsNaN(w) ? w : fallback) * 0.5f;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        /// <summary>Panel (origen arriba a la izquierda, escalado) → píxeles de pantalla (origen abajo a la izquierda).</summary>
        private Vector2 PanelToScreen(Vector2 panelPos)
        {
            var root = _surface?.panel?.visualTree;
            if (root == null || root.layout.width <= 0f || root.layout.height <= 0f) return panelPos;
            return new Vector2(panelPos.x / root.layout.width * Screen.width,
                               Screen.height - panelPos.y / root.layout.height * Screen.height);
        }
    }
}
