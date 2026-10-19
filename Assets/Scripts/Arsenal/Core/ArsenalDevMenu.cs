using System.Collections.Generic;
using RedMagic.Core;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>Menú DEV de las armas del arsenal (sólo editor / dev build)</b>, el equivalente al panel de la web: arma de cada hueco
    /// (activa y de reserva), evolución y mejoras de cada una, elemento forzado, Duplicador y reacciones. Edita copias en
    /// memoria (<see cref="ArsenalRuntime"/>), nunca los assets. <b>F10</b> o el botón «ARMAS»; pausa el juego mientras está
    /// abierto. Textos sin localizar a propósito: no es UI de jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArsenalDevMenu : MonoBehaviour
    {
        private static ArsenalDevMenu _instance;

        private bool _open;
        private Vector2 _scroll;
        private GUIStyle _box, _label, _small, _button, _on, _title;
        private float _k = -1f;
        private List<WeaponDefinition> _weapons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || _instance != null) return;
            var go = new GameObject("[ArsenalDevMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ArsenalDevMenu>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame) SetOpen(!_open);
        }

        private void OnDisable() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            GameStateManager.Instance?.SetPaused(open);
        }

        private void Styles()
        {
            float k = Mathf.Max(0.7f, Screen.height / 1080f);
            if (_box != null && Mathf.Approximately(_k, k)) return;
            _k = k;
            int fs = Mathf.RoundToInt(22 * k);
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(17 * k) };
            _small.normal.textColor = new Color(0.75f, 0.8f, 0.9f);
            _title = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(27 * k), fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = fs, fixedHeight = 44 * k, wordWrap = true };
            _on = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
            _on.normal.textColor = _on.hover.textColor = new Color(0.45f, 0.92f, 1f);
        }

        private void OnGUI()
        {
            Styles();
            float k = _k;
            if (!_open)
            {
                if (GUI.Button(new Rect(12 * k, 70 * k, 140 * k, 44 * k), "ARMAS", _button)) SetOpen(true);
                return;
            }

            var inv = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            float w = Mathf.Min(Screen.width - 40 * k, 1180 * k), h = Screen.height - 40 * k;
            GUILayout.BeginArea(new Rect(20 * k, 20 * k, w, h), _box);
            GUILayout.Label("DEV · Armas del arsenal (F10)", _title);
            if (inv == null) { GUILayout.Label("No hay WeaponLoadout en esta escena.", _label); Footer(); GUILayout.EndArea(); return; }

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(w * 0.48f));
            Slot(inv, true);
            GUILayout.EndVertical();
            GUILayout.Space(14 * k);
            GUILayout.BeginVertical(GUILayout.Width(w * 0.48f));
            Slot(inv, false);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(12 * k);
            GUILayout.Label("Build (vía 3):", _label);
            GUILayout.BeginHorizontal();
            ElemChoice(ArsenalBuild.ElementMode.Auto, "Elemento: build");
            ElemChoice(ArsenalBuild.ElementMode.None, "Ninguno");
            ElemChoice(ArsenalBuild.ElementMode.Ice, "Hielo");
            ElemChoice(ArsenalBuild.ElementMode.Fire, "Fuego");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ArsenalBuild.Duplicator = Toggle("Duplicador", ArsenalBuild.Duplicator);
            ArsenalBuild.Reactions = Toggle("Reacciones (añicos, deshielo, vapor)", ArsenalBuild.Reactions);
            GUILayout.EndHorizontal();
            GUILayout.Label("Tab (o mando ▲) cambia de arma · Q dispara (mantener = repetir / canalizar / cargar).", _small);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Volver a los valores de los assets", _button)) ArsenalRuntime.Clear();
            if (GUILayout.Button("Cerrar (F10)", _button)) SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Footer()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cerrar (F10)", _button)) SetOpen(false);
        }

        private void Slot(WeaponInventory inv, bool active)
        {
            var weapon = active ? inv.Weapon : inv.SecondWeapon;
            GUILayout.Label(active ? "Hueco 1 · arma activa" : "Hueco 2 · reserva (Tab)", _title);
            _weapons ??= LoadWeapons();
            int cols = 3, i = 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(weapon == null ? "(X) Nada" : "( ) Nada", weapon == null ? _on : _button)) Set(inv, active, null);
            i++;
            foreach (var wd in _weapons)
            {
                if (i % cols == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                bool on = wd == weapon;
                if (GUILayout.Button((on ? "(X) " : "( ) ") + wd.DisplayName, on ? _on : _button)) Set(inv, active, wd);
                i++;
            }
            GUILayout.EndHorizontal();

            if (weapon == null || !weapon.IsArsenal) return;
            var kit = ArsenalRuntime.Editable(weapon);
            var tree = kit.Tree;
            GUILayout.Space(6 * _k);
            GUILayout.Label("Evolución (nivel 2):", _label);
            GUILayout.BeginHorizontal();
            for (int e = 0; e < tree.evolutions.Count; e++)
            {
                bool on = kit.evolution == e;
                if (GUILayout.Button((on ? "(X) " : "( ) ") + tree.evolutions[e].name, on ? _on : _button))
                {
                    if (kit.evolution != e) { kit.evolution = e; kit.upgrade1 = kit.upgrade2 = kit.upgrade3 = false; ArsenalRuntime.Touch(); }
                }
            }
            GUILayout.EndHorizontal();
            var evo = tree.evolutions[Mathf.Clamp(kit.evolution, 0, tree.evolutions.Count - 1)];
            GUILayout.Label(evo.desc, _small);
            if (kit.evolution <= 0) return;
            GUILayout.Label("Mejoras (niveles 3-5):", _label);
            for (int u = 0; u < 3; u++)
            {
                bool v = kit.GetUpgrade(u), nv = Toggle(evo.ups[u], v);
                if (nv != v) { kit.SetUpgrade(u, nv); ArsenalRuntime.Touch(); }
                GUILayout.Label(evo.upDescs[u], _small);
            }
        }

        private static void Set(WeaponInventory inv, bool active, WeaponDefinition wd)
        {
            if (active) { if (wd != null && wd == inv.SecondWeapon) inv.SetSecondWeapon(null); inv.SetWeapon(wd); }
            else { if (wd != null && wd == inv.Weapon) inv.SetWeapon(null); inv.SetSecondWeapon(wd); }
        }

        private void ElemChoice(ArsenalBuild.ElementMode m, string text)
        {
            bool on = ArsenalBuild.ElementOverride == m;
            if (GUILayout.Button((on ? "(X) " : "( ) ") + text, on ? _on : _button)) ArsenalBuild.ElementOverride = m;
        }

        private bool Toggle(string text, bool value) =>
            GUILayout.Button((value ? "[X] " : "[  ] ") + text, value ? _on : _button) ? !value : value;

        /// <summary>Las armas del arsenal de la escena de prueba (las que haya registrado el kit) y las de Resources.</summary>
        private static List<WeaponDefinition> LoadWeapons()
        {
            var list = new List<WeaponDefinition>();
            foreach (var w in ArsenalCatalog.Weapons) if (w != null && !list.Contains(w)) list.Add(w);
            foreach (var w in Resources.LoadAll<WeaponDefinition>("Items/Weapons")) if (w != null && w.IsArsenal && !list.Contains(w)) list.Add(w);
            return list;
        }
    }

    /// <summary>Armas del arsenal conocidas en esta sesión (las registra la escena de prueba; el menú DEV las ofrece).</summary>
    public static class ArsenalCatalog
    {
        public static readonly List<WeaponDefinition> Weapons = new List<WeaponDefinition>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Weapons.Clear();

        public static void Register(IEnumerable<WeaponDefinition> weapons)
        {
            foreach (var w in weapons) if (w != null && !Weapons.Contains(w)) Weapons.Add(w);
        }
    }
}
