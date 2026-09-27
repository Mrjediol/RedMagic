using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// Todos los tiempos y tamaños de <see cref="PassiveDropCinematic"/>. Vive en
    /// <c>Assets/Resources/PassiveDropCinematicSettings.asset</c> (se lee con <c>Resources.Load</c>,
    /// igual que <c>CurrencyConfig</c>); sin asset se usan estos valores por defecto. Se puede
    /// editar en Play: cada reproducción lee los valores del momento.
    ///
    /// Tamaños en píxeles de la resolución de referencia 1600×900 (la de los menús).
    /// </summary>
    [CreateAssetMenu(fileName = "PassiveDropCinematicSettings", menuName = "RedMagic/UI/Passive Drop Cinematic Settings")]
    public class PassiveDropCinematicSettings : ScriptableObject
    {
        public const string ResourcePath = "PassiveDropCinematicSettings";

        [Header("Oscurecido")]
        public Color overlayColor = new(0.01f, 0.02f, 0.05f, 0.85f);
        [Min(0f)] public float overlayFadeIn = 0.18f;

        [Header("Caída del icono")]
        [Min(16f)] public float iconSize = 240f;
        [Tooltip("Espera desde el inicio hasta que empieza a caer.")]
        [Min(0f)] public float dropDelay = 0.1f;
        [Tooltip("Altura (px) sobre el centro desde la que cae.")]
        public float dropStartOffset = 650f;
        [Min(0.01f)] public float dropDuration = 0.42f;
        [Tooltip("Altura (px) del rebote al tocar el centro.")]
        [Min(0f)] public float bounceHeight = 55f;
        [Min(0.01f)] public float bounceDuration = 0.3f;

        [Header("Onda al aterrizar")]
        public Color shockColor = new(0.3f, 1f, 0.9f, 0.95f);
        [Min(0.01f)] public float shockDuration = 0.6f;
        [Tooltip("Diámetro final (px) del anillo.")]
        [Min(0f)] public float shockMaxSize = 720f;
        [Range(1, 4)] public int ringCount = 2;
        [Min(0f)] public float ringStagger = 0.09f;
        [Range(0, 48)] public int sparkCount = 20;
        [Min(0f)] public float sparkDistance = 300f;
        [Min(1f)] public float sparkSize = 16f;

        [Header("Zoom")]
        [Min(0.1f)] public float zoomScale = 1.6f;
        [Tooltip("Espera tras el rebote antes de hacer zoom.")]
        [Min(0f)] public float zoomDelay = 0.05f;
        [Min(0.01f)] public float zoomDuration = 0.6f;

        [Header("Brillo")]
        public Color glowColor = new(0.3f, 1f, 0.9f, 1f);
        [Tooltip("Diámetro del brillo relativo al icono.")]
        [Min(0.5f)] public float glowSize = 1.9f;
        [Min(0f)] public float glowPulseSpeed = 1.6f;
        [Range(0f, 1f)] public float glowMinAlpha = 0.3f;
        [Range(0f, 1f)] public float glowMaxAlpha = 0.85f;
        [Range(0f, 0.5f)] public float glowScalePulse = 0.08f;

        [Header("Textos")]
        [Tooltip("Clave de idioma del encabezado; si no está traducida se usa headerText.")]
        public string headerKey = "cinematic.header";
        public string headerText = "Has obtenido";
        [Min(8)] public int headerFontSize = 34;
        [Min(8)] public int nameFontSize = 76;
        public Color headerColor = new(0.75f, 0.95f, 0.95f, 1f);
        public Color nameColor = new(1f, 0.93f, 0.7f, 1f);
        [Tooltip("Separación (px) entre el icono (ya con zoom) y los textos.")]
        [Min(0f)] public float textMargin = 26f;
        [Tooltip("Espera tras empezar el zoom antes de mostrar el nombre.")]
        [Min(0f)] public float textDelay = 0.25f;
        [Min(0.01f)] public float textFadeDuration = 0.4f;

        [Tooltip("Clave de idioma de \"Toca para continuar\"; si no está traducida se usa continueText.")]
        public string continueKey = "cinematic.continue";
        public string continueText = "Toca para continuar";
        [Min(8)] public int continueFontSize = 30;
        public Color continueColor = new(0.85f, 0.95f, 1f, 1f);
        [Tooltip("Espera tras el nombre antes de mostrar \"Toca para continuar\". Antes no se acepta el toque.")]
        [Min(0f)] public float continueDelay = 0.5f;
        [Min(0f)] public float continuePulseSpeed = 1.2f;

        [Header("Salida")]
        [Min(0.01f)] public float outroDuration = 0.4f;

        [Header("Sonido")]
        [Tooltip("Cuando el icono aterriza. Al continuar suena el clic de UI del AudioManager.")]
        public SoundCue landSound = new SoundCue();
    }
}
