using System;
using System.Collections.Generic;
using System.Globalization;
using RedMagic.Settings;
using UnityEngine;

namespace RedMagic.Localization
{
    /// <summary>Un idioma disponible: su código (= nombre del fichero) y cómo se llama a sí mismo.</summary>
    public readonly struct LanguageInfo
    {
        public readonly string Code;
        public readonly string DisplayName;

        public LanguageInfo(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Localización del juego: clave → texto en el idioma activo.
    ///
    /// <b>Un fichero por idioma</b> en <c>Assets/Resources/Localization/&lt;código&gt;.txt</c>
    /// (<c>es.txt</c>, <c>en.txt</c>…): añadir un idioma es soltar otro fichero, sin tocar código.
    /// Formato, una entrada por línea:
    /// <code>
    /// # comentario
    /// @name = English            ← nombre del idioma en el selector de Opciones
    /// @system = English          ← SystemLanguage que lo elige en el primer arranque (opcional)
    /// @culture = en-US           ← formato de números en los huecos {0:0.#} (opcional)
    /// options.title = OPTIONS
    /// shop.gold_short = Not enough gold ({0} / {1})   ← huecos de string.Format
    /// </code>
    /// <c>\n</c> dentro de un valor es un salto de línea.
    ///
    /// Clave que falta en el idioma activo → la del idioma por defecto
    /// (<see cref="GameSettingsConfig.defaultLanguage"/>) → la propia clave (y un aviso una vez),
    /// así que un olvido se ve en pantalla en vez de quedar en blanco.
    ///
    /// El idioma elegido se guarda en <c>PlayerPrefs</c> (como el volumen en <c>AudioManager</c>);
    /// sin nada guardado se usa el idioma del dispositivo si hay fichero para él y, si no, el de por
    /// defecto. <see cref="Changed"/> avisa a todo lo que pinta texto (<see cref="LocalizedText"/>,
    /// <see cref="LocalizedUi"/>, los menús hechos en código) para que se repinte al momento.
    /// </summary>
    public static class Loc
    {
        public const string ResourceFolder = "Localization";
        public const string PrefLanguage = "settings.language";

        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new();
        private static readonly List<LanguageInfo> LanguageList = new();
        private static readonly Dictionary<string, SystemLanguage> SystemLanguages = new();
        private static readonly Dictionary<string, CultureInfo> Cultures = new();
        private static readonly HashSet<string> Warned = new();
        private static bool _loaded;
        private static string _language;

        /// <summary>Tras cada cambio de idioma. Quien pinta texto se repinta aquí.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tables.Clear();
            LanguageList.Clear();
            SystemLanguages.Clear();
            Cultures.Clear();
            Warned.Clear();
            _loaded = false;
            _language = null;
            Changed = null;
        }

        // Antes de la primera escena: el primer layout de UI ya sale en el idioma bueno.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureLoaded();

        // ------------------------------------------------------------------ API

        /// <summary>Código del idioma activo ("es", "en"…).</summary>
        public static string Language
        {
            get
            {
                EnsureLoaded();
                return _language;
            }
        }

        public static IReadOnlyList<LanguageInfo> Languages
        {
            get
            {
                EnsureLoaded();
                return LanguageList;
            }
        }

        public static string DefaultLanguage => GameSettingsConfig.Instance.defaultLanguage;

        /// <summary>Cultura de números del idioma activo (<c>@culture</c> del fichero): 1,5 en español, 1.5 en inglés.</summary>
        public static CultureInfo Culture
        {
            get
            {
                EnsureLoaded();
                return _language != null && Cultures.TryGetValue(_language, out var culture) ? culture : CultureInfo.CurrentCulture;
            }
        }

        /// <summary>Texto de <paramref name="key"/> en el idioma activo.</summary>
        public static string Get(string key)
        {
            if (TryGet(key, out var text)) return text;
            WarnMissing(key);
            return key;
        }

        /// <summary><see cref="Get(string)"/> + <c>string.Format</c> con <paramref name="args"/>.</summary>
        public static string Get(string key, params object[] args)
        {
            var format = Get(key);
            try { return string.Format(Culture, format, args); }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] Formato inválido en '{key}' ({_language}): \"{format}\"");
                return format;
            }
        }

        /// <summary>
        /// Texto de <paramref name="key"/>, o <paramref name="fallback"/> si ningún idioma la tiene.
        /// Para textos que viven en un asset: el asset guarda el original y el fichero lo traduce.
        /// </summary>
        public static string GetOr(string key, string fallback) =>
            !string.IsNullOrEmpty(key) && TryGet(key, out var text) ? text : fallback;

        public static bool Has(string key) => TryGet(key, out _);

        /// <summary>
        /// Texto de un campo de un asset de contenido (item, arma, pasiva, jefe): la clave es
        /// <c>&lt;textKey&gt;.&lt;field&gt;</c> y, si no está traducida (o el asset aún no tiene
        /// <paramref name="textKey"/>), el texto que guarda el propio asset.
        /// </summary>
        public static string ForAsset(string textKey, string field, string original) =>
            string.IsNullOrEmpty(textKey) ? original : GetOr(textKey + "." + field, original);

        /// <summary>Cambia de idioma, lo guarda y avisa. False si no hay fichero para ese código.</summary>
        public static bool SetLanguage(string code)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(code) || !Tables.ContainsKey(code)) return false;

            PlayerPrefs.SetString(PrefLanguage, code);
            PlayerPrefs.Save();
            if (code == _language) return true;

            _language = code;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Vuelve a leer los ficheros (herramientas de editor tras escribirlos) y repinta.</summary>
        public static void Reload()
        {
            _loaded = false;
            Tables.Clear();
            LanguageList.Clear();
            SystemLanguages.Clear();
            Cultures.Clear();
            Warned.Clear();
            EnsureLoaded();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ carga

        private static bool TryGet(string key, out string text)
        {
            EnsureLoaded();
            text = null;
            if (string.IsNullOrEmpty(key)) return false;

            if (_language != null && Tables.TryGetValue(_language, out var table) && table.TryGetValue(key, out text))
                return true;

            var fallback = DefaultLanguage;
            return fallback != _language && Tables.TryGetValue(fallback, out var defaults) &&
                   defaults.TryGetValue(key, out text);
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (var asset in Resources.LoadAll<TextAsset>(ResourceFolder))
                Parse(asset.name, asset.text);

            LanguageList.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
            _language ??= PickStartLanguage();
        }

        private static string PickStartLanguage()
        {
            var saved = PlayerPrefs.GetString(PrefLanguage, "");
            if (Tables.ContainsKey(saved)) return saved;

            // Primer arranque: el idioma del dispositivo si hay fichero para él (sin guardarlo, para
            // que la elección del jugador sea lo único que se persiste).
            if (GameSettingsConfig.Instance.useDeviceLanguageOnFirstLaunch)
            {
                foreach (var pair in SystemLanguages)
                    if (pair.Value == Application.systemLanguage) return pair.Key;
            }

            var fallback = DefaultLanguage;
            if (Tables.ContainsKey(fallback)) return fallback;
            return LanguageList.Count > 0 ? LanguageList[0].Code : fallback;
        }

        private static void Parse(string code, string source)
        {
            var table = new Dictionary<string, string>();
            string displayName = code;

            foreach (var raw in source.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");

                if (key == "@name") displayName = value;
                else if (key == "@culture")
                {
                    try { Cultures[code] = CultureInfo.GetCultureInfo(value); }
                    catch (CultureNotFoundException) { Debug.LogWarning($"[Loc] Cultura desconocida '{value}' en {code}.txt"); }
                }
                else if (key == "@system")
                {
                    if (Enum.TryParse(value, true, out SystemLanguage system)) SystemLanguages[code] = system;
                }
                else table[key] = value;
            }

            Tables[code] = table;
            LanguageList.Add(new LanguageInfo(code, displayName));
        }

        private static void WarnMissing(string key)
        {
            if (string.IsNullOrEmpty(key) || !Warned.Add(key)) return;
            Debug.LogWarning($"[Loc] Falta la clave '{key}' (idioma '{_language}' y por defecto). " +
                             "Tools ▸ RedMagic ▸ Localización ▸ Auditar claves.");
        }
    }
}
