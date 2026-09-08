using RedMagic.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.Bosses
{
    /// <summary>
    /// La barra de vida de pantalla del jefe: nombre, epíteto, barra ancha con "fantasma" de daño
    /// y marcas de fase.
    ///
    /// Se construye entera en código (Canvas en overlay + <see cref="Image"/> + <see cref="Text"/>
    /// con la fuente incorporada <c>LegacyRuntime.ttf</c>), igual que
    /// <c>DamagePopups</c> y <c>HealthBarUI</c>: sin prefab, sin UXML y sin PanelSettings que
    /// mantener. Un jefe nuevo no necesita tocar nada de UI — sale su nombre y sus fases solos.
    ///
    /// La pide el propio <see cref="BossController"/> al empezar el combate
    /// (<see cref="Show"/>) y se va sola al morir el jefe (<see cref="Hide"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealthBar : MonoBehaviour
    {
        private static BossHealthBar _instance;

        // Domain Reload está desactivado: la referencia estática sobreviviría a la sesión de Play
        // anterior apuntando a un objeto ya destruido.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _instance = null;

        // Resolución de referencia, la misma que usan los menús del proyecto.
        private static readonly Vector2 ReferenceResolution = new Vector2(1600f, 900f);

        private const float BarWidth = 1040f;
        private const float BarHeight = 38f;
        private const float BorderThickness = 4f;

        private static readonly Color BorderColor = new Color(0.04f, 0.03f, 0.05f, 0.95f);
        private static readonly Color BackgroundColor = new Color(0.16f, 0.05f, 0.07f, 0.95f);
        // El fantasma va en ámbar y el relleno en carmesí: son colores lo bastante distintos como
        // para que el trozo que se queda atrás tras un golpe fuerte se lea de un vistazo.
        private static readonly Color GhostColor = new Color(0.98f, 0.76f, 0.32f, 1f);
        private static readonly Color FillColor = new Color(0.76f, 0.11f, 0.16f, 1f);

        /// <summary>Barra dorada mientras el jefe está expuesto: "pégale AHORA".</summary>
        private static readonly Color VulnerableFillColor = new Color(1f, 0.82f, 0.3f, 1f);

        /// <summary>Barra de acero mientras se cubre: "no le pegues AHORA".</summary>
        private static readonly Color GuardFillColor = new Color(0.5f, 0.72f, 0.95f, 1f);
        private static readonly Color TickColor = new Color(0.05f, 0.03f, 0.05f, 0.9f);

        private BossController _boss;
        private Health _health;

        private CanvasGroup _group;
        private RectTransform _fill;
        private Image _fillImage;
        private RectTransform _ghost;
        private RectTransform _ticks;
        private Text _nameLabel;
        private Text _titleLabel;

        /// <summary>Segundos que el fantasma se queda quieto tras un golpe antes de empezar a bajar.</summary>
        private const float GhostHoldSeconds = 0.45f;

        /// <summary>Fracción de barra por segundo a la que baja el fantasma una vez arranca.</summary>
        private const float GhostDecayPerSecond = 0.16f;

        private float _shown;
        private float _target = 1f;
        private float _ghostValue = 1f;
        private float _ghostHold;
        private bool _hiding;

        // ------------------------------------------------------------------ API

        /// <summary>Muestra la barra del jefe indicado. Reemplaza a la que hubiera.</summary>
        public static void Show(BossController boss)
        {
            if (boss == null) return;

            if (_instance == null)
                _instance = new GameObject("[BossHealthBar]").AddComponent<BossHealthBar>();

            _instance.Bind(boss);
        }

        /// <summary>Desvanece y destruye la barra.</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._hiding = true;
        }

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake() => Build();

        private void OnDestroy()
        {
            Unbind();
            if (_instance == this) _instance = null;
        }

        private void Bind(BossController boss)
        {
            Unbind();

            _boss = boss;
            _health = boss.BossHealth;
            _hiding = false;

            boss.VulnerabilityChanged += OnVulnerabilityChanged;
            boss.GuardChanged += OnGuardChanged;
            OnVulnerabilityChanged(boss.IsVulnerable);

            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                _target = _health.Normalized;
            }
            else
            {
                _target = 1f;
            }

            _ghostValue = _target;

            var definition = boss.Definition;
            _nameLabel.text = definition != null ? definition.DisplayName.ToUpperInvariant() : boss.name;
            _titleLabel.text = definition != null ? definition.Title : string.Empty;
            _titleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_titleLabel.text));

            BuildPhaseTicks(definition);
            ApplyFill();
        }

        private void Unbind()
        {
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
            if (_boss != null)
            {
                _boss.VulnerabilityChanged -= OnVulnerabilityChanged;
                _boss.GuardChanged -= OnGuardChanged;
            }
            _health = null;
            _boss = null;
        }

        /// <summary>
        /// Lo contrario del aviso de exposición: la barra se vuelve acero y el subtítulo dice que
        /// pegar ahora sale caro. Las dos señales viven en el mismo sitio a propósito — el jugador
        /// mira la barra, así que ahí tienen que estar tanto el "ahora sí" como el "ahora no".
        /// </summary>
        private void OnGuardChanged(bool guarding)
        {
            if (_fillImage == null) return;

            _fillImage.color = guarding ? GuardFillColor : FillColor;
            SetSubtitle(guarding ? "SE CUBRE" : null, GuardFillColor);
        }

        /// <summary>
        /// La barra se vuelve dorada mientras el jefe está expuesto. La señal principal es su aura,
        /// pero el jugador que está castigando mira los números y la barra, no al jefe — así que
        /// la ventana también tiene que verse aquí.
        /// </summary>
        private void OnVulnerabilityChanged(bool vulnerable)
        {
            if (_fillImage == null) return;

            _fillImage.color = vulnerable ? VulnerableFillColor : FillColor;
            SetSubtitle(vulnerable ? "¡EXPUESTO!" : null, VulnerableFillColor);
        }

        /// <summary>
        /// Escribe un aviso de estado bajo el nombre, o lo quita (null) devolviendo el epíteto del
        /// jefe. Centralizado para que exposición y guardia no se pisen el subtítulo.
        /// </summary>
        private void SetSubtitle(string text, Color color)
        {
            if (_titleLabel == null) return;

            if (string.IsNullOrEmpty(text))
            {
                var definition = _boss != null ? _boss.Definition : null;
                _titleLabel.text = definition != null ? definition.Title : string.Empty;
                _titleLabel.color = new Color(0.85f, 0.72f, 0.58f, 0.95f);
            }
            else
            {
                _titleLabel.text = text;
                _titleLabel.color = color;
            }

            _titleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_titleLabel.text));
        }

        private void OnHealthChanged(float current, float max)
        {
            float previous = _target;
            _target = max <= 0f ? 0f : Mathf.Clamp01(current / max);

            // Un golpe congela el fantasma un momento antes de que empiece a bajar: ese trozo
            // ámbar parado es lo que hace que un mordisco grande a la barra se lea como grande.
            if (_target < previous) _ghostHold = GhostHoldSeconds;

            ApplyFill();
        }

        private void Update()
        {
            // El jefe destruido (cambio de sección con la barra abierta) también la cierra.
            if (!_hiding && _boss == null) _hiding = true;

            float fade = _hiding ? 0f : 1f;
            _shown = Mathf.MoveTowards(_shown, fade, Time.unscaledDeltaTime * 2.2f);
            _group.alpha = _shown;

            if (_hiding && _shown <= 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            // El "fantasma" persigue a la vida con retraso: el trozo ámbar que se queda atrás es lo
            // que hace que un golpe fuerte se lea como fuerte.
            if (_ghostHold > 0f)
            {
                _ghostHold -= Time.deltaTime;
            }
            else if (_ghostValue > _target)
            {
                _ghostValue = Mathf.MoveTowards(_ghostValue, _target, Time.deltaTime * GhostDecayPerSecond);
                ApplyFill();
            }
            else if (_ghostValue < _target)
            {
                // Curarse no deja un fantasma "negativo": el ámbar se pone al día de golpe.
                _ghostValue = _target;
                ApplyFill();
            }
        }

        private void ApplyFill()
        {
            if (_fill != null) _fill.localScale = new Vector3(_target, 1f, 1f);
            if (_ghost != null) _ghost.localScale = new Vector3(Mathf.Max(_ghostValue, _target), 1f, 1f);
        }

        // ------------------------------------------------------------------ construcción

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Por debajo de los menús (20+) para que la pausa siga tapándola.
            canvas.sortingOrder = 18;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Raíz anclada arriba y centrada: la barra siempre cae bajo el borde superior.
            var root = NewRect("BossBar", canvasGo.transform);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -28f);
            root.sizeDelta = new Vector2(BarWidth, 130f);

            _nameLabel = NewText("Name", root, 46, FontStyle.Bold, TextAnchor.MiddleCenter,
                                 new Color(0.97f, 0.93f, 0.88f, 1f));
            var nameRect = (RectTransform)_nameLabel.transform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 54f);

            _titleLabel = NewText("Title", root, 24, FontStyle.Italic, TextAnchor.MiddleCenter,
                                  new Color(0.85f, 0.72f, 0.58f, 0.95f));
            var titleRect = (RectTransform)_titleLabel.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -52f);
            titleRect.sizeDelta = new Vector2(0f, 28f);

            var bar = NewRect("Bar", root);
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -86f);
            bar.sizeDelta = new Vector2(BarWidth, BarHeight);

            NewImage("Border", bar, BorderColor, Vector2.zero, Vector2.zero);
            NewImage("Background", bar, BackgroundColor,
                     new Vector2(BorderThickness, BorderThickness),
                     new Vector2(-BorderThickness, -BorderThickness));

            _ghost = StretchFill(NewImage("Ghost", bar, GhostColor,
                                          new Vector2(BorderThickness, BorderThickness),
                                          new Vector2(-BorderThickness, -BorderThickness)));

            _fill = StretchFill(NewImage("Fill", bar, FillColor,
                                         new Vector2(BorderThickness, BorderThickness),
                                         new Vector2(-BorderThickness, -BorderThickness)));
            _fillImage = _fill.GetComponent<Image>();

            _ticks = NewRect("PhaseTicks", bar);
            _ticks.anchorMin = Vector2.zero;
            _ticks.anchorMax = Vector2.one;
            _ticks.offsetMin = Vector2.zero;
            _ticks.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Marcas verticales en los umbrales de fase. Que se vean es lo que convierte "le queda
        /// media vida" en "queda un cambio de fase": el jugador sabe cuándo va a cambiar el ritmo.
        /// </summary>
        private void BuildPhaseTicks(BossDefinition definition)
        {
            for (int i = _ticks.childCount - 1; i >= 0; i--) Destroy(_ticks.GetChild(i).gameObject);

            if (definition == null) return;

            var phases = definition.Phases;
            if (phases == null) return;

            float inner = BarWidth - BorderThickness * 2f;

            for (int i = 1; i < phases.Length; i++)   // la fase 0 empieza en 1: no se marca
            {
                float t = Mathf.Clamp01(phases[i].startsAtHealth);

                var tick = NewImage($"Tick{i}", _ticks, TickColor, Vector2.zero, Vector2.zero);
                tick.anchorMin = new Vector2(0f, 0f);
                tick.anchorMax = new Vector2(0f, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.offsetMin = Vector2.zero;
                tick.offsetMax = Vector2.zero;
                tick.sizeDelta = new Vector2(4f, -BorderThickness * 2f);
                tick.anchoredPosition = new Vector2(BorderThickness + inner * t, 0f);
            }
        }

        // ------------------------------------------------------------------ helpers uGUI

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform NewImage(string name, Transform parent, Color color,
                                              Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            // Un Image sin sprite dibuja un rectángulo sólido: suficiente para una barra.
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        /// <summary>Pivote a la izquierda: escalar en X vacía la barra de derecha a izquierda.</summary>
        private static RectTransform StretchFill(RectTransform rect)
        {
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            rect.offsetMax = new Vector2(BorderThickness, -BorderThickness);
            rect.sizeDelta = new Vector2(BarWidth - BorderThickness * 2f, rect.sizeDelta.y);
            return rect;
        }

        private static Text NewText(string name, Transform parent, int size, FontStyle style,
                                    TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }
    }
}
