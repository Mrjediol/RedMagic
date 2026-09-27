using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Settings
{
    /// <summary>
    /// Lo configurable de los ajustes de pantalla e idioma, en <c>Assets/Resources/GameSettings.asset</c>
    /// (lo lee <see cref="Instance"/> con <c>Resources.Load</c>, como <c>ShopConfig</c>). Lo que el
    /// jugador elige se guarda aparte, en <c>PlayerPrefs</c> (<see cref="DisplaySettings"/>,
    /// <see cref="Localization.Loc"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "RedMagic/Game Settings")]
    public class GameSettingsConfig : ScriptableObject
    {
        public const string ResourcePath = "GameSettings";

        /// <summary>Un modo de pantalla del selector de Opciones.</summary>
        [Serializable]
        public class ResolutionPreset
        {
            [Tooltip("Clave del nombre en los ficheros de idioma (p. ej. settings.resolution.pc).")]
            public string labelKey;
            [Min(1)] public int width = 1920;
            [Min(1)] public int height = 1080;
            [Tooltip("Proporción tal como se enseña (\"16:9\"). La real sale de width / height.")]
            public string aspect = "16:9";

            public ResolutionPreset() { }

            public ResolutionPreset(string labelKey, int width, int height, string aspect)
            {
                this.labelKey = labelKey;
                this.width = width;
                this.height = height;
                this.aspect = aspect;
            }

            public float AspectRatio => (float)width / Mathf.Max(1, height);
        }

        [Header("Pantalla")]
        [Tooltip("Modos del selector de Opciones, en orden. Añadir uno = otra fila aquí (+ su clave).")]
        public List<ResolutionPreset> resolutionPresets = new()
        {
            new ResolutionPreset("settings.resolution.pc", 1920, 1080, "16:9"),
            new ResolutionPreset("settings.resolution.mobile", 2340, 1080, "20:9"),
        };

        [Tooltip("Índice del modo sin nada guardado, en PC / editor.")]
        [Min(0)] public int defaultPresetDesktop;
        [Tooltip("Índice del modo sin nada guardado, en móvil.")]
        [Min(0)] public int defaultPresetMobile = 1;

        [Tooltip("Color de las bandas cuando la pantalla no tiene la proporción del modo elegido.")]
        public Color letterboxColor = Color.black;

        [Tooltip("Meter también la UI dentro del rectángulo del juego (si no, los HUD van de borde a " +
                 "borde de la pantalla, encima de las bandas).")]
        public bool letterboxUi = true;

        [Header("Idioma")]
        [Tooltip("Código del idioma por defecto (= nombre del fichero en Resources/Localization). " +
                 "También es el de reserva cuando a otro idioma le falta una clave.")]
        public string defaultLanguage = "es";

        [Tooltip("En el primer arranque, usar el idioma del dispositivo si hay fichero para él.")]
        public bool useDeviceLanguageOnFirstLaunch = true;

        private static GameSettingsConfig _instance;

        /// <summary>El asset de Resources, o uno con los valores por defecto si falta.</summary>
        public static GameSettingsConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameSettingsConfig>(ResourcePath);
                if (_instance == null) _instance = CreateInstance<GameSettingsConfig>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public int DefaultPreset => Mathf.Clamp(Application.isMobilePlatform ? defaultPresetMobile : defaultPresetDesktop,
                                                0, Mathf.Max(0, resolutionPresets.Count - 1));
    }
}
