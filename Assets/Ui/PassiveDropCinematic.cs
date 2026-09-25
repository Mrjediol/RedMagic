using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RedMagic.UI
{
    /// <summary>
    /// Cinemática a pantalla completa al conseguir una pasiva legendaria: pausa, oscurece, el icono
    /// cae al centro con rebote, onda cian al aterrizar, zoom, brillo pulsante, "Has obtenido" +
    /// nombre y "Toca para continuar"; un toque/clic/tecla lo deshace y reanuda el juego.
    ///
    /// <c>PassiveDropCinematic.Show(passive)</c> desde cualquier sitio. Singleton persistente que se
    /// crea solo la primera vez y construye su Canvas una única vez (se reutiliza en cada
    /// reproducción). Varias llamadas seguidas se encolan. Canvas uGUI en
    /// <see cref="SortingOrder"/> (el máximo), por encima de todo. Tiempos y tamaños en
    /// <see cref="PassiveDropCinematicSettings"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PassiveDropCinematic : MonoBehaviour
    {
        public const int SortingOrder = short.MaxValue;
        private static readonly Vector2 ReferenceResolution = new(1600f, 900f);

        private static PassiveDropCinematic _instance;

        /// <summary>True mientras se reproduce (o hay alguna en cola).</summary>
        public static bool IsPlaying => _instance != null && _instance._running;

        /// <summary>Al terminar la última cinemática en cola, con el juego ya reanudado.</summary>
        public static event Action Finished;

        private readonly Queue<(string name, Sprite icon)> _queue = new();
        private bool _running;
        private PassiveDropCinematicSettings _settings;

        private CanvasGroup _group;
        private Image _overlay, _icon, _glow;
        private RectTransform _iconRoot;
        private Image[] _rings;
        private Image[] _sparks;
        private Vector2[] _sparkDirs;
        private Text _header, _name, _continue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Finished = null;
        }

        // ------------------------------------------------------------------ API

        public static void Show(LegendaryPassive passive)
        {
            if (passive == null) return;
            Show(passive.displayName, passive.icon);
        }

        public static void Show(string passiveName, Sprite icon)
        {
            if (_instance == null)
            {
                var go = new GameObject("[PassiveDropCinematic]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PassiveDropCinematic>();
            }

            _instance._queue.Enqueue((passiveName, icon));
            if (!_instance._running) _instance.StartCoroutine(_instance.PlayQueue());
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            if (_running) SetPaused(false);
            _instance = null;
        }

        // ------------------------------------------------------------------ secuencia

        private IEnumerator PlayQueue()
        {
            _running = true;
            SetPaused(true);

            while (_queue.Count > 0)
            {
                var (passiveName, icon) = _queue.Dequeue();
                yield return Play(passiveName, icon);
            }

            SetPaused(false);
            _running = false;
            Finished?.Invoke();
        }

        private IEnumerator Play(string passiveName, Sprite icon)
        {
            _settings = LoadSettings();
            var s = _settings;
            Build();
            Prepare(passiveName, icon);

            float landAt = s.dropDelay + s.dropDuration;
            float zoomAt = landAt + s.bounceDuration + s.zoomDelay;
            float textAt = zoomAt + s.textDelay;
            float continueAt = textAt + s.textFadeDuration + s.continueDelay;
            bool landed = false;
            float zoom = 1f;

            // Todo depende del tiempo transcurrido: una sola pasada por frame que coloca cada pieza.
            for (float t = 0f; ; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                _overlay.color = WithAlpha(s.overlayColor, s.overlayColor.a * Clamp01(t, 0f, s.overlayFadeIn));

                _iconRoot.anchoredPosition = new Vector2(0f, IconHeight(t, s));
                _icon.enabled = t >= s.dropDelay;

                if (!landed && t >= landAt)
                {
                    landed = true;
                    PlaySfx(s.landSfxId);
                }

                if (landed) UpdateShockwave(t - landAt, s);

                zoom = Mathf.LerpUnclamped(1f, s.zoomScale, EaseInOutCubic(Clamp01(t, zoomAt, s.zoomDuration)));
                _iconRoot.localScale = Vector3.one * zoom;

                UpdateGlow(t, landAt, zoom, s);
                UpdateTexts(t, textAt, continueAt, zoom, s, alphaScale: 1f);

                if (t >= continueAt && AnyPress()) break;
                yield return null;
            }

            PlaySfx(s.continueSfxId);

            // Salida: el zoom se deshace y todo se funde a la vez.
            float fromZoom = zoom;
            for (float t = 0f; t < s.outroDuration; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                float p = EaseInOutCubic(t / s.outroDuration);
                zoom = Mathf.Lerp(fromZoom, 1f, p);
                _iconRoot.localScale = Vector3.one * zoom;
                _group.alpha = 1f - p;
                UpdateGlow(float.MaxValue, 0f, zoom, s);
                UpdateTexts(float.MaxValue, 0f, 0f, zoom, s, alphaScale: 1f);
                yield return null;
            }

            _group.gameObject.SetActive(false);
        }

        /// <summary>Altura del icono sobre el centro: caída acelerada y un rebote en arco.</summary>
        private static float IconHeight(float t, PassiveDropCinematicSettings s)
        {
            if (t < s.dropDelay) return s.dropStartOffset;

            float fall = (t - s.dropDelay) / s.dropDuration;
            if (fall < 1f) return s.dropStartOffset * (1f - fall * fall);

            float bounce = (t - s.dropDelay - s.dropDuration) / s.bounceDuration;
            return bounce < 1f ? s.bounceHeight * Mathf.Sin(Mathf.PI * bounce) : 0f;
        }

        private void UpdateShockwave(float since, PassiveDropCinematicSettings s)
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                bool on = i < s.ringCount;
                float p = (since - i * s.ringStagger) / s.shockDuration;
                on &= p >= 0f && p < 1f;
                _rings[i].enabled = on;
                if (!on) continue;

                float size = Mathf.Lerp(s.iconSize * 0.6f, s.shockMaxSize, EaseOutCubic(p));
                _rings[i].rectTransform.sizeDelta = new Vector2(size, size);
                _rings[i].color = WithAlpha(s.shockColor, s.shockColor.a * (1f - p));
            }

            float sp = since / s.shockDuration;
            for (int i = 0; i < _sparks.Length; i++)
            {
                bool on = i < s.sparkCount && sp < 1f;
                _sparks[i].enabled = on;
                if (!on) continue;

                var rt = _sparks[i].rectTransform;
                rt.anchoredPosition = _sparkDirs[i] * (s.iconSize * 0.35f + s.sparkDistance * EaseOutCubic(sp));
                float size = s.sparkSize * (1f - sp * 0.7f);
                rt.sizeDelta = new Vector2(size, size);
                _sparks[i].color = WithAlpha(s.shockColor, 1f - sp);
            }
        }

        private void UpdateGlow(float t, float landAt, float zoom, PassiveDropCinematicSettings s)
        {
            float appear = Clamp01(t, landAt, s.zoomDuration);
            float wave = 0.5f + 0.5f * Mathf.Sin((t - landAt) * s.glowPulseSpeed * Mathf.PI * 2f);
            if (t == float.MaxValue) wave = 0.5f;

            float alpha = Mathf.Lerp(s.glowMinAlpha, s.glowMaxAlpha, wave) * appear;
            float size = s.iconSize * s.glowSize * zoom * (1f + s.glowScalePulse * (wave - 0.5f) * 2f);

            _glow.enabled = appear > 0f;
            _glow.rectTransform.sizeDelta = new Vector2(size, size);
            _glow.color = WithAlpha(s.glowColor, s.glowColor.a * alpha);
        }

        private void UpdateTexts(float t, float textAt, float continueAt, float zoom,
                                 PassiveDropCinematicSettings s, float alphaScale)
        {
            float half = s.iconSize * zoom * 0.5f;

            float a = Clamp01(t, textAt, s.textFadeDuration) * alphaScale;
            _name.rectTransform.anchoredPosition = new Vector2(0f, half + s.textMargin + s.nameFontSize * 0.6f);
            _header.rectTransform.anchoredPosition =
                new Vector2(0f, half + s.textMargin + s.nameFontSize * 1.2f + s.headerFontSize * 0.6f);
            _name.color = WithAlpha(s.nameColor, s.nameColor.a * a);
            _header.color = WithAlpha(s.headerColor, s.headerColor.a * a);

            float c = Clamp01(t, continueAt - s.textFadeDuration, s.textFadeDuration);
            if (t != float.MaxValue && t > continueAt)
                c *= Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Cos((t - continueAt) * s.continuePulseSpeed * Mathf.PI * 2f));
            _continue.rectTransform.anchoredPosition = new Vector2(0f, -(half + s.textMargin + s.continueFontSize * 0.6f));
            _continue.color = WithAlpha(s.continueColor, s.continueColor.a * c * alphaScale);
        }

        // ------------------------------------------------------------------ estado

        private void Prepare(string passiveName, Sprite icon)
        {
            var s = _settings;
            _group.gameObject.SetActive(true);
            _group.alpha = 1f;

            _icon.sprite = icon;
            _icon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            _icon.preserveAspect = true;
            _icon.rectTransform.sizeDelta = new Vector2(s.iconSize, s.iconSize);
            _iconRoot.localScale = Vector3.one;
            _icon.enabled = false;

            _header.text = s.headerText;
            _header.fontSize = s.headerFontSize;
            _name.text = passiveName;
            _name.fontSize = s.nameFontSize;
            _continue.text = s.continueText;
            _continue.fontSize = s.continueFontSize;

            foreach (var r in _rings) r.enabled = false;
            foreach (var sp in _sparks) sp.enabled = false;
            _glow.enabled = false;

            // Direcciones repartidas con un poco de azar, nuevas en cada reproducción.
            for (int i = 0; i < _sparkDirs.Length; i++)
            {
                int n = Mathf.Max(1, s.sparkCount);
                float angle = (i + UnityEngine.Random.Range(-0.35f, 0.35f)) / n * Mathf.PI * 2f;
                _sparkDirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(0.75f, 1.1f);
            }
        }

        private static void SetPaused(bool paused)
        {
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(paused);
            else Time.timeScale = paused ? 0f : 1f;
        }

        private static bool AnyPress()
        {
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private static void PlaySfx(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(id);
        }

        private static PassiveDropCinematicSettings LoadSettings()
        {
            var settings = Resources.Load<PassiveDropCinematicSettings>(PassiveDropCinematicSettings.ResourcePath);
            return settings != null ? settings : ScriptableObject.CreateInstance<PassiveDropCinematicSettings>();
        }

        // ------------------------------------------------------------------ construcción (una vez)

        private void Build()
        {
            if (_group != null) return;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var root = (RectTransform)canvasGo.transform;

            _overlay = NewImage("Overlay", root, null);
            Stretch(_overlay.rectTransform);

            var fx = NewRect("Fx", root);
            _glow = NewImage("Glow", fx, Fx.ProceduralSprites.Glow);

            _rings = new Image[4];
            for (int i = 0; i < _rings.Length; i++) _rings[i] = NewImage($"Ring{i}", fx, Fx.ProceduralSprites.Ring);

            _sparks = new Image[48];
            _sparkDirs = new Vector2[_sparks.Length];
            for (int i = 0; i < _sparks.Length; i++) _sparks[i] = NewImage($"Spark{i}", fx, Fx.ProceduralSprites.Glow);

            _iconRoot = NewRect("IconRoot", root);
            _icon = NewImage("Icon", _iconRoot, null);

            _header = NewText("Header", root, FontStyle.Normal);
            _name = NewText("Name", root, FontStyle.Bold);
            _continue = NewText("Continue", root, FontStyle.Italic);

            var glow = _name.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(0.1f, 0.6f, 0.6f, 0.8f);
            glow.effectDistance = new Vector2(2f, -2f);

            canvasGo.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, 100f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ easing

        private static float Clamp01(float t, float start, float duration) =>
            duration <= 0f ? (t >= start ? 1f : 0f) : Mathf.Clamp01((t - start) / duration);

        private static float EaseOutCubic(float p) { p = 1f - Mathf.Clamp01(p); return 1f - p * p * p; }

        private static float EaseInOutCubic(float p)
        {
            p = Mathf.Clamp01(p);
            return p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) * 0.5f;
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
