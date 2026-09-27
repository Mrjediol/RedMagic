using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RedMagic.Settings
{
    /// <summary>
    /// Modo de pantalla elegido en Opciones (<see cref="GameSettingsConfig.resolutionPresets"/>):
    /// se guarda en <c>PlayerPrefs</c> (como el volumen) y se aplica antes de cargar la primera escena.
    ///
    /// Aplicar un modo:
    ///  - <b>PC, ventana</b>: <c>Screen.SetResolution</c> al tamaño del modo (encogido si no cabe en el
    ///    monitor, sin cambiar la proporción).
    ///  - <b>PC, pantalla completa</b>: resolución nativa del monitor + bandas: forzar un tamaño de otra
    ///    proporción en pantalla completa lo estira.
    ///  - <b>Móvil</b>: la superficie se queda nativa (<c>SetResolution</c> con otra proporción la
    ///    deforma) y se ponen bandas si la proporción del teléfono no es la del modo.
    ///  - <b>Editor</b>: la ventana Game manda en el tamaño; bandas para ver el encuadre del modo.
    ///
    /// Las bandas las pone <see cref="DisplayLetterbox"/>: recorta el <c>Camera.rect</c> de las cámaras
    /// al rectángulo del modo (así <c>CameraFollow</c>, que lee <c>camera.aspect</c> cada frame,
    /// encuadra con la proporción buena) y, si <see cref="GameSettingsConfig.letterboxUi"/>, mete los
    /// paneles de UI Toolkit en ese mismo rectángulo. También bloquea la orientación en horizontal.
    /// </summary>
    public static class DisplaySettings
    {
        public const string PrefPreset = "settings.resolutionPreset";

        private static int _preset = -1;

        /// <summary>Tras cada cambio de modo (y cuando cambia el tamaño de la pantalla).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _preset = -1;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            LockLandscape();
            ApplyResolution();
            DisplayLetterbox.Ensure();
        }

        public static IReadOnlyList<GameSettingsConfig.ResolutionPreset> Presets =>
            GameSettingsConfig.Instance.resolutionPresets;

        public static int PresetIndex
        {
            get
            {
                if (_preset < 0)
                {
                    int saved = PlayerPrefs.GetInt(PrefPreset, -1);
                    _preset = saved >= 0 && saved < Presets.Count ? saved : GameSettingsConfig.Instance.DefaultPreset;
                }
                return _preset;
            }
        }

        public static GameSettingsConfig.ResolutionPreset Current =>
            Presets.Count > 0 ? Presets[Mathf.Clamp(PresetIndex, 0, Presets.Count - 1)] : null;

        /// <summary>Proporción del modo activo (ancho / alto); 16:9 si no hay modos.</summary>
        public static float TargetAspect => Current?.AspectRatio ?? 16f / 9f;

        /// <summary>Elige, guarda y aplica un modo.</summary>
        public static void SetPreset(int index)
        {
            if (index < 0 || index >= Presets.Count) return;

            PlayerPrefs.SetInt(PrefPreset, index);
            PlayerPrefs.Save();
            if (index == PresetIndex) return;

            _preset = index;
            ApplyResolution();
            Changed?.Invoke();
        }

        internal static void RaiseChanged() => Changed?.Invoke();

        // ------------------------------------------------------------------ aplicar

        private static void ApplyResolution()
        {
#if !UNITY_EDITOR
            var preset = Current;
            if (preset == null || Application.isMobilePlatform) return;

            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                // Que la ventana quepa en el monitor (con margen para la barra de tareas).
                var display = Screen.mainWindowDisplayInfo;
                float fit = Mathf.Min(1f, display.width * 0.92f / preset.width, display.height * 0.92f / preset.height);
                Screen.SetResolution(Mathf.RoundToInt(preset.width * fit), Mathf.RoundToInt(preset.height * fit),
                                     FullScreenMode.Windowed);
            }
            else
            {
                var native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, Screen.fullScreenMode);
            }
#endif
        }

        /// <summary>El juego es sólo horizontal: sin retrato aunque el dispositivo gire.</summary>
        private static void LockLandscape()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }

    /// <summary>
    /// Bandas negras cuando la pantalla no tiene la proporción del modo elegido. Persistente
    /// (DontDestroyOnLoad), se crea sola. Recalcula al cargar escena, al cambiar de modo y cuando cambia
    /// el tamaño de la pantalla — nunca busca objetos en cada frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplayLetterbox : MonoBehaviour
    {
        private const float AspectTolerance = 0.01f;

        private static DisplayLetterbox _instance;

        private Camera _backdrop;
        private int _width, _height;
        private Rect _rect = new(0f, 0f, 1f, 1f);
        private bool _panelsPending;

        /// <summary>El rectángulo del juego en la pantalla (0-1), el mismo que llevan las cámaras.</summary>
        public static Rect GameRect => _instance != null ? _instance._rect : new Rect(0f, 0f, 1f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[DisplaySettings]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DisplayLetterbox>();
        }

        private void Awake()
        {
            var go = new GameObject("LetterboxBackdrop");
            go.transform.SetParent(transform, false);
            _backdrop = go.AddComponent<Camera>();
            _backdrop.cullingMask = 0;
            _backdrop.clearFlags = CameraClearFlags.SolidColor;
            _backdrop.depth = -100f; // antes que cualquier cámara del juego: limpia la pantalla entera
            _backdrop.orthographic = true;
            _backdrop.enabled = false;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            DisplaySettings.Changed += Recompute;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DisplaySettings.Changed -= Recompute;
        }

        // Los documentos de la escena nueva aún no tienen layout: ApplyToPanels los deja pendientes.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Recompute();

        private void Update()
        {
            if (_panelsPending) ApplyToPanels(GameSettingsConfig.Instance.letterboxUi ? _rect : new Rect(0f, 0f, 1f, 1f));
            if (Screen.width == _width && Screen.height == _height) return;
            _width = Screen.width;
            _height = Screen.height;
            DisplaySettings.RaiseChanged(); // → Recompute (y quien quiera saber que cambió la pantalla)
        }

        /// <summary>Vuelve a aplicar el rectángulo a cámaras y paneles (p. ej. tras crear una UI nueva).</summary>
        public static void Refresh()
        {
            if (_instance != null) _instance.Recompute();
        }

        private void Recompute()
        {
            _rect = ComputeRect(DisplaySettings.TargetAspect, Screen.width, Screen.height);
            bool boxed = _rect.width < 1f || _rect.height < 1f;

            _backdrop.backgroundColor = GameSettingsConfig.Instance.letterboxColor;
            _backdrop.enabled = boxed;

            foreach (var cam in Camera.allCameras)
                if (cam != _backdrop && cam.targetTexture == null) cam.rect = _rect;

            ApplyToPanels(GameSettingsConfig.Instance.letterboxUi ? _rect : new Rect(0f, 0f, 1f, 1f));
        }

        private static Rect ComputeRect(float target, int width, int height)
        {
            if (width <= 0 || height <= 0 || target <= 0f) return new Rect(0f, 0f, 1f, 1f);

            float screen = (float)width / height;
            if (screen > target * (1f + AspectTolerance))
            {
                float w = target / screen; // pantalla más ancha que el modo: bandas a los lados
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            if (screen < target * (1f - AspectTolerance))
            {
                float h = screen / target; // más estrecha: bandas arriba y abajo
                return new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
            return new Rect(0f, 0f, 1f, 1f);
        }

        // Cada documento de UI Toolkit recibe de margen la parte de las bandas, en unidades de su panel:
        // su raíz es absoluta (0 a los cuatro lados), así que el margen la mete en el rectángulo del
        // juego sin tocar nada de lo que el documento haya puesto dentro. Un panel que aún no tiene
        // tamaño (primer frame tras cargar) se reintenta en el siguiente Update.
        private void ApplyToPanels(Rect rect)
        {
            _panelsPending = false;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var root = doc.rootVisualElement;
                var panel = root?.panel;
                if (panel == null) continue;

                var size = panel.visualTree.layout.size;
                if (float.IsNaN(size.x) || size.x <= 0f || float.IsNaN(size.y) || size.y <= 0f)
                {
                    _panelsPending = true;
                    continue;
                }

                root.style.marginLeft = rect.x * size.x;
                root.style.marginRight = (1f - rect.xMax) * size.x;
                root.style.marginBottom = rect.y * size.y;
                root.style.marginTop = (1f - rect.yMax) * size.y;
            }
        }
    }
}
