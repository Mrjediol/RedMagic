using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// "Elige tu arma": N cartas (icono + nombre + daño/cooldown), un toque elige. Lo abre el cofre
    /// del hub con la pasiva Grimorio del Umbral (<c>Hub.ChestLootContainer</c>).
    ///
    /// No se puede cerrar sin elegir: el cofre ya se ha abierto y el arma es obligatoria para
    /// salir del hub. Teclas 1..N también eligen. Mismo montaje que los demás menús construidos en
    /// código: se auto-crea, es persistente, saca su <see cref="PanelSettings"/> de Resources y
    /// pausa el juego mientras está abierto.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponChoiceMenuController : MonoBehaviour
    {
        public static WeaponChoiceMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "WeaponChoiceMenuPanelSettings";
        private const float CardWidth = 300f;
        private const float CardHeight = 380f;

        private UIDocument _document;
        private VisualElement _overlay, _row;
        private Label _subtitle, _hint;
        private bool _open, _built;

        private readonly List<WeaponDefinition> _options = new();
        private Action<WeaponDefinition> _onPicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[WeaponChoiceMenu]").AddComponent<WeaponChoiceMenuController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            if (_document.panelSettings == null)
                Debug.LogWarning("[WeaponChoiceMenu] Falta '" + PanelSettingsResourcePath + "' en Resources.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_open) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < _options.Count && i < 9; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Pick(i);
                    return;
                }
        }

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Muestra las opciones y llama a <paramref name="onPicked"/> con la elegida. Devuelve false
        /// (sin abrir nada) si el menú no se puede mostrar; quien llama debe entonces elegir por su
        /// cuenta. <paramref name="subtitle"/> va bajo el título (p. ej. el nombre de la pasiva).
        /// </summary>
        public bool Open(IReadOnlyList<WeaponDefinition> options, Action<WeaponDefinition> onPicked,
                         string subtitle = "")
        {
            if (_open || options == null || options.Count == 0 || onPicked == null) return false;
            if (!EnsureBuilt()) return false;

            _options.Clear();
            _options.AddRange(options);
            _onPicked = onPicked;
            _subtitle.text = subtitle ?? "";
            _subtitle.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;

            _hint.text = $"Toca una carta para elegirla · teclas 1-{Mathf.Min(9, _options.Count)}";

            _row.Clear();
            for (int i = 0; i < _options.Count; i++) _row.Add(BuildCard(_options[i], i));

            _open = true;
            _overlay.style.display = DisplayStyle.Flex;
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            _row[0].Focus();
            return true;
        }

        private void Pick(int index)
        {
            if (!_open || index < 0 || index >= _options.Count) return;

            var weapon = _options[index];
            var callback = _onPicked;

            _open = false;
            _onPicked = null;
            _overlay.style.display = DisplayStyle.None;
            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);

            callback(weapon);
        }

        // ------------------------------------------------------------------ construcción

        private bool EnsureBuilt()
        {
            if (_built) return true;
            if (_document == null || _document.panelSettings == null) return false;

            var root = _document.rootVisualElement;
            if (root == null) return false;

            MenuStyle.FillParent(root);

            _overlay = new VisualElement { name = "weapon-choice-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            panel.style.alignItems = Align.Center;
            _overlay.Add(panel);

            var title = MenuStyle.Title("ELIGE TU ARMA");
            title.style.marginBottom = 4;
            panel.Add(title);

            _subtitle = MenuStyle.CardDescription("");
            _subtitle.style.fontSize = MenuStyle.BodyFontSize;
            _subtitle.style.marginBottom = 14;
            panel.Add(_subtitle);

            _row = new VisualElement();
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.justifyContent = Justify.Center;
            panel.Add(_row);

            _hint = MenuStyle.Hint("");
            panel.Add(_hint);

            _built = true;
            return true;
        }

        private VisualElement BuildCard(WeaponDefinition weapon, int index)
        {
            var card = new Button(() => Pick(index));
            MenuStyle.Card(card);
            card.style.width = CardWidth;
            card.style.height = CardHeight;
            card.style.justifyContent = Justify.Center;
            card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 150;
            icon.style.height = 150;
            icon.style.marginBottom = 18;
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            if (weapon.Icon != null) icon.style.backgroundImage = new StyleBackground(weapon.Icon);
            else icon.style.backgroundColor = weapon.Accent;
            card.Add(icon);

            var name = MenuStyle.CardTitle(weapon.DisplayName);
            name.style.fontSize = 28;
            name.style.color = weapon.Accent;
            name.style.marginBottom = 8;
            name.pickingMode = PickingMode.Ignore;
            card.Add(name);

            var stats = MenuStyle.CardDescription($"Daño {weapon.BaseDamage:0} · {weapon.BaseCooldown:0.00}s");
            stats.style.fontSize = MenuStyle.BodyFontSize;
            stats.pickingMode = PickingMode.Ignore;
            card.Add(stats);

            return card;
        }
    }
}
