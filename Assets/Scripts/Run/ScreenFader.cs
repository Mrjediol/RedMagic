using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RedMagic.Run
{
    /// <summary>
    /// Fundido a color de pantalla completa que tapa las transiciones de escena. Sin él, cargar
    /// una sección se veía como un congelado seco (el mundo se para, tirón de carga, el mundo
    /// vuelve de golpe); con él, la pantalla se funde, la carga ocurre a oscuras y la escena nueva
    /// aparece fundiéndose, ya asentada.
    ///
    /// Se auto-crea la primera vez que se usa y sobrevive a las cargas (DontDestroyOnLoad): un
    /// único canvas construido una vez y reutilizado, nunca uno por transición. Va en tiempo real
    /// (unscaled) porque el <see cref="RunManager"/> congela el juego durante la carga.
    ///
    /// Red de seguridad: si una carga <see cref="LoadSceneMode.Single"/> llega con la pantalla
    /// tapada (volver al hub, al menú, una transición cortada a medias), se funde de vuelta sola, así
    /// que la pantalla nunca se puede quedar en negro.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        // Por encima de todo el HUD y los menús; justo debajo de la cinemática de pasiva (32767).
        private const int SortingOrder = 32000;

        // Un frame de carga puede durar cientos de ms: sin tope, el fundido se saltaría entero ahí.
        private const float MaxStep = 1f / 30f;

        private static ScreenFader _instance;

        private Image _image;
        private float _alpha;
        private Coroutine _animation;
        private bool _animating; // no el handle: una corrutina de duración 0 termina antes de devolverlo
        private float _pendingFadeIn = 0.35f;

        /// <summary>True mientras la pantalla está (o se está poniendo) tapada.</summary>
        public static bool IsCovering => _instance != null && _instance._alpha > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private static ScreenFader Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[ScreenFader]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScreenFader>();
            return _instance;
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            gameObject.AddComponent<GraphicRaycaster>(); // tapa los clics de uGUI mientras está opaco

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _image = imageGo.AddComponent<Image>();
            SetAlpha(0f);
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Funde la pantalla a <paramref name="color"/> en <paramref name="duration"/> segundos
        /// reales. Devuelve algo que se puede esperar con <c>yield return</c> hasta que está opaca.
        /// <paramref name="fadeInDuration"/> es lo que durará el fundido de vuelta si lo hace la red
        /// de seguridad (una carga Single).
        /// </summary>
        public static CustomYieldInstruction FadeOut(float duration, float fadeInDuration, Color color)
        {
            var fader = Ensure();
            fader._pendingFadeIn = fadeInDuration;
            fader._image.color = new Color(color.r, color.g, color.b, fader._alpha);
            fader.Animate(1f, duration, 0);
            return new WaitWhile(() => fader != null && fader._animating);
        }

        /// <summary>Destapa la pantalla. Esperar <paramref name="delayFrames"/> frames antes deja que
        /// los primeros frames caros de la escena nueva (Start, warm-up) pasen todavía a oscuras.</summary>
        public static void FadeIn(float duration, int delayFrames = 0)
        {
            if (_instance == null || _instance._alpha <= 0f) return;
            _instance.Animate(0f, duration, delayFrames);
        }

        // ------------------------------------------------------------------ interno

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && _alpha > 0f) Animate(0f, _pendingFadeIn, 2);
        }

        private void Animate(float target, float duration, int delayFrames)
        {
            if (_animation != null) StopCoroutine(_animation);
            _animating = true;
            _animation = StartCoroutine(AnimateRoutine(target, duration, delayFrames));
        }

        private IEnumerator AnimateRoutine(float target, float duration, int delayFrames)
        {
            for (int i = 0; i < delayFrames; i++) yield return null;

            float start = _alpha;
            float t = 0f;
            while (t < 1f && duration > 0f)
            {
                t = Mathf.Min(1f, t + Mathf.Min(Time.unscaledDeltaTime, MaxStep) / duration);
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }

            SetAlpha(target);
            _animating = false;
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, alpha);
            _image.enabled = alpha > 0f;
            _image.raycastTarget = alpha > 0f;
        }
    }
}
