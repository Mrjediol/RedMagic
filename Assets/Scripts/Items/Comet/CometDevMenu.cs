using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Items
{
    /// <summary>
    /// <b>Menú DEV (sólo para pruebas; se quitará)</b>: cambia en pleno Play el arma Cometa equipada —
    /// comportamiento, tamaño de la cadena, perforación, impact frames y upgrade — sin tocar el asset
    /// (escribe en <see cref="CometRuntime"/>). Se abre con <b>F9</b> o con el botón «DEV» de la esquina, y
    /// mientras está abierto el juego queda en pausa (<see cref="GameStateManager.SetPaused"/>). Sólo existe
    /// en el editor y en builds de desarrollo. Textos sin localizar a propósito: no es UI de jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CometDevMenu : MonoBehaviour
    {
        private static CometDevMenu _instance;

        private bool _open;
        private GUIStyle _box, _label, _button, _on, _title;
        private float _builtFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || _instance != null) return;
            var go = new GameObject("[CometDevMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CometDevMenu>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) SetOpen(!_open);
        }

        private void OnDisable() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            GameStateManager.Instance?.SetPaused(open);
        }

        private void BuildStyles()
        {
            float k = Mathf.Max(0.75f, Screen.height / 1080f);
            if (_box != null && Mathf.Approximately(_builtFor, k)) return;
            _builtFor = k;
            int fs = Mathf.RoundToInt(26 * k);
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
            _title = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(30 * k), fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = fs, fixedHeight = 52 * k };
            _on = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
            _on.normal.textColor = _on.hover.textColor = new Color(0.4f, 0.9f, 1f);
        }

        private void OnGUI()
        {
            BuildStyles();
            float k = _builtFor;

            if (!_open)
            {
                if (GUI.Button(new Rect(12 * k, 12 * k, 110 * k, 52 * k), "DEV", _button)) SetOpen(true);
                return;
            }

            float w = 640 * k, h = Mathf.Min(Screen.height - 40 * k, 900 * k);
            GUILayout.BeginArea(new Rect(20 * k, 20 * k, w, h), _box);
            GUILayout.Label("DEV · Arma equipada (Cometa)", _title);

            var inventory = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            var weapon = inventory != null ? inventory.Weapon : null;
            GUILayout.Label(weapon != null ? $"Arma: {weapon.name}" : "Sin arma equipada.", _label);

            if (weapon == null || !weapon.IsComet)
            {
                GUILayout.Label("El arma equipada no es un Cometa.", _label);
                if (inventory != null && GUILayout.Button("Equipar Cometa", _button))
                {
                    var comet = FindCometWeapon();
                    if (comet != null) inventory.SetWeapon(comet);
                }
                Footer();
                GUILayout.EndArea();
                return;
            }

            var s = CometRuntime.Editable(weapon);

            GUILayout.Space(8 * k);
            GUILayout.Label("Comportamiento (sólo uno):", _label);
            GUILayout.BeginHorizontal();
            Choice(s, CometBehaviour.Base, "Base");
            Choice(s, CometBehaviour.Chain, "Cadena");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Choice(s, CometBehaviour.Pierce, "Perforación");
            Choice(s, CometBehaviour.Homing, "Homing");
            GUILayout.EndHorizontal();

            if (s.behaviour == CometBehaviour.Chain) s.chainCount = Counter("Saltos de la cadena", s.chainCount, 1, 20);
            if (s.behaviour == CometBehaviour.Pierce) s.pierceCount = Counter("Enemigos que atraviesa", s.pierceCount, 1, 20);

            GUILayout.Space(10 * k);
            GUILayout.Label("General:", _label);
            s.impactFrames = Toggle("Impact frames", s.impactFrames);
            s.upgraded = Toggle("Upgrade", s.upgraded);
            var st = s.Stats;
            GUILayout.Label($"Daño ×{st.damageMultiplier:0.##} · Velocidad ×{st.speedMultiplier:0.##} · Tamaño ×{st.sizeScale:0.##}", _label);

            GUILayout.Space(10 * k);
            if (GUILayout.Button("Volver a los valores del asset", _button)) CometRuntime.Clear();

            Footer();
            GUILayout.EndArea();
        }

        private void Footer()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cerrar (F9)", _button)) SetOpen(false);
        }

        private void Choice(CometSettings s, CometBehaviour value, string text)
        {
            if (GUILayout.Button((s.behaviour == value ? "(X) " : "( ) ") + text, s.behaviour == value ? _on : _button)) s.behaviour = value;
        }

        private bool Toggle(string text, bool value)
        {
            return GUILayout.Button((value ? "[X] " : "[  ] ") + text, value ? _on : _button) ? !value : value;
        }

        private int Counter(string text, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{text}: {value}", _label);
            if (GUILayout.Button("-", _button, GUILayout.Width(80 * _builtFor))) value--;
            if (GUILayout.Button("+", _button, GUILayout.Width(80 * _builtFor))) value++;
            GUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, max);
        }

        private static WeaponDefinition FindCometWeapon()
        {
            foreach (var w in Resources.LoadAll<WeaponDefinition>("Items/Weapons"))
                if (w != null && w.IsComet) return w;
            return null;
        }
    }
}
