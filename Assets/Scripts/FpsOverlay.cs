using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Core
{
    /// <summary>
    /// Contador de FPS en pantalla, arriba a la izquierda. Se auto-crea antes de la primera escena
    /// (no hay que ponerlo en ninguna escena) y sobrevive a los cambios de escena, así que se ve
    /// igual en el menú, el hub, las secciones y el jefe.
    ///
    /// Sólo existe en el editor y en <b>Development Build</b>: un build de release no lo incluye,
    /// así que no hay que acordarse de quitarlo. Para forzarlo en un build normal, añade el define
    /// <c>REDMAGIC_FPS</c> en Player Settings.
    ///
    /// Se dibuja con IMGUI (<see cref="OnGUI"/>) a propósito: no depende de UI Toolkit ni de un
    /// Canvas, así que funciona aunque la escena todavía no haya montado su UI. El tamaño de letra
    /// escala con la altura de pantalla y respeta el área segura (notch), para que se lea en el móvil.
    ///
    /// Alternar visibilidad: tecla <b>F1</b>, o tocar la pantalla con <b>tres dedos</b> a la vez en
    /// el móvil. El estado se recuerda en PlayerPrefs.
    /// </summary>
    [DisallowMultipleComponent]
    public class FpsOverlay : MonoBehaviour
    {
        private const string VisiblePref = "debug.fps.visible";

        public static FpsOverlay Instance { get; private set; }

        /// <summary>True si el contador se está dibujando ahora mismo.</summary>
        public static bool Visible { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && !REDMAGIC_FPS
            return;
#else
            if (Instance != null) return;
            var go = new GameObject("[FpsOverlay]");
            go.AddComponent<FpsOverlay>();
#endif
        }

        // --- medición -------------------------------------------------------

        // Suavizado exponencial del delta time para que el número no baile. Va en tiempo real
        // (unscaled) para que pausar el juego o tocar timeScale no falsee la lectura.
        private float _smoothDt = 0.0166f;

        // Peor frame de la ventana de 1 s en curso, y el de la ventana anterior ya cerrada (que es
        // el que se muestra, para que "mín" no parpadee).
        private float _windowMaxDt;
        private float _windowTimer;
        private float _reportedMinFps;

        private float Fps => 1f / Mathf.Max(_smoothDt, 0.00001f);

        // --- toggle --------------------------------------------------------

        private int _prevTouchCount;

        // --- pintado ------------------------------------------------------

        private GUIStyle _style;
        private Texture2D _bg;

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

            Visible = PlayerPrefs.GetInt(VisiblePref, 1) == 1;

            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            _bg.Apply();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (_bg != null) Destroy(_bg);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _smoothDt = Mathf.Lerp(_smoothDt, dt, 0.1f);

            _windowMaxDt = Mathf.Max(_windowMaxDt, dt);
            _windowTimer += dt;
            if (_windowTimer >= 1f)
            {
                _reportedMinFps = 1f / Mathf.Max(_windowMaxDt, 0.00001f);
                _windowTimer = 0f;
                _windowMaxDt = 0f;
            }

            HandleToggle();
        }

        private void HandleToggle()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                SetVisible(!Visible);

            // Móvil: tres dedos a la vez. Se dispara sólo en el frame en que se llega a 3.
            int touches = Touchscreen.current != null ? CountActiveTouches() : 0;
            if (touches >= 3 && _prevTouchCount < 3)
                SetVisible(!Visible);
            _prevTouchCount = touches;
        }

        private static int CountActiveTouches()
        {
            int n = 0;
            foreach (var t in Touchscreen.current.touches)
            {
                var phase = t.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                    phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                    phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    n++;
            }
            return n;
        }

        private static void SetVisible(bool visible)
        {
            Visible = visible;
            PlayerPrefs.SetInt(VisiblePref, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            if (!Visible) return;

            int fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.024f));

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Bold,
                    richText = false
                };
            }
            _style.fontSize = fontSize;

            float fps = Fps;
            _style.normal.textColor =
                fps >= 55f ? new Color(0.5f, 1f, 0.5f) :
                fps >= 28f ? new Color(1f, 0.85f, 0.35f) :
                             new Color(1f, 0.45f, 0.45f);

            string text =
                $"{fps,3:0} FPS  ({_smoothDt * 1000f:0.0} ms)\n" +
                $"min {_reportedMinFps:0}\n" +
                $"{Screen.width}x{Screen.height}";

            // Arriba a la izquierda, dentro del área segura para esquivar el notch.
            Rect safe = Screen.safeArea;
            float x = safe.xMin + fontSize * 0.4f;
            // safeArea.y se mide desde abajo; en coordenadas de GUI el margen de arriba es (Screen.height - yMax).
            float y = (Screen.height - safe.yMax) + fontSize * 0.4f;

            var content = new GUIContent(text);

            // CalcSize no reparte bien los saltos de línea: se mide línea a línea y se coge la más ancha.
            float width = 0f;
            foreach (var line in text.Split('\n'))
                width = Mathf.Max(width, _style.CalcSize(new GUIContent(line)).x);

            float pad = fontSize * 0.35f;
            var box = new Rect(x, y, width + pad * 2f, fontSize * 3.6f + pad * 2f);

            var prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(box, _bg);
            GUI.color = prev;

            GUI.Label(new Rect(box.x + pad, box.y + pad, box.width, box.height), content, _style);
        }
    }
}
