using RedMagic.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.UI
{
    /// <summary>
    /// Barra de vida en world space que sigue al personaje por encima de su cabeza.
    /// Construye su propio Canvas (Render Mode: World Space) al arrancar, así que sirve tal cual
    /// para cualquier personaje sin prefabs ni cableado.
    ///
    /// Ajusta <see cref="headHeightOffset"/> por personaje/modelo para colocar la barra a la altura correcta.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Posición")]
        [Tooltip("Altura, en unidades de mundo sobre el origen del personaje, a la que se sitúa la barra. " +
                 "Súbelo para modelos altos, bájalo para modelos bajos.")]
        public float headHeightOffset = 1.2f;

        [Tooltip("Desplazamiento horizontal de la barra respecto al personaje.")]
        public float horizontalOffset;

        [Header("Tamaño (unidades de mundo)")]
        [SerializeField] private float barWidth = 1f;
        [SerializeField] private float barHeight = 0.14f;
        [SerializeField] private float borderThickness = 0.02f;

        [Header("Colores")]
        [SerializeField] private Color borderColor = new Color(0.05f, 0.03f, 0.05f, 0.9f);
        [SerializeField] private Color backgroundColor = new Color(0.18f, 0.06f, 0.08f, 0.9f);
        [SerializeField] private Color fillColor = new Color(0.83f, 0.22f, 0.26f, 1f);

        [Header("Comportamiento")]
        [Tooltip("Health del que lee. Si se deja vacío se busca en este GameObject o en sus padres.")]
        [SerializeField] private Health health;
        [Tooltip("Ocultar la barra mientras la vida esté al máximo.")]
        [SerializeField] private bool hideWhenFull;
        [Tooltip("Ocultar la barra al morir.")]
        [SerializeField] private bool hideOnDeath = true;
        [Tooltip("Orden de dibujado del canvas; súbelo si la barra queda tapada por los sprites.")]
        [SerializeField] private int sortingOrder = 100;

        // 100 unidades de UI = 1 unidad de mundo. Mantiene los tamaños del RectTransform manejables.
        private const float UnitsPerWorldUnit = 100f;

        private RectTransform _barRoot;
        private RectTransform _fill;
        private GameObject _visualRoot;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            BuildBar();
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.HealthChanged += OnHealthChanged;
            health.Died += OnDied;
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnDied;
        }

        private void LateUpdate()
        {
            if (_barRoot == null) return;

            // La barra es hija del personaje, así que basta con la posición local.
            _barRoot.localPosition = new Vector3(horizontalOffset, headHeightOffset, 0f);
            _barRoot.localRotation = Quaternion.identity;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_fill != null)
            {
                float normalized = max <= 0f ? 0f : Mathf.Clamp01(current / max);
                _fill.localScale = new Vector3(normalized, 1f, 1f);
            }

            if (_visualRoot != null && hideWhenFull)
                _visualRoot.SetActive(current < max - 0.0001f);
        }

        private void OnDied()
        {
            if (hideOnDeath && _visualRoot != null) _visualRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ construcción

        private void BuildBar()
        {
            float w = barWidth * UnitsPerWorldUnit;
            float h = barHeight * UnitsPerWorldUnit;
            float border = borderThickness * UnitsPerWorldUnit;

            var canvasGo = new GameObject("HealthBarCanvas", typeof(RectTransform), typeof(Canvas));
            _visualRoot = canvasGo;
            _barRoot = (RectTransform)canvasGo.transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(horizontalOffset, headHeightOffset, 0f);
            _barRoot.localRotation = Quaternion.identity;
            _barRoot.localScale = Vector3.one / UnitsPerWorldUnit;
            _barRoot.sizeDelta = new Vector2(w, h);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;

            CreateImage("Border", _barRoot, borderColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateImage("Background", _barRoot, backgroundColor,
                Vector2.zero, Vector2.one, new Vector2(border, border), new Vector2(-border, -border));

            var fill = CreateImage("Fill", _barRoot, fillColor,
                Vector2.zero, Vector2.one, new Vector2(border, border), new Vector2(-border, -border));

            // Pivote a la izquierda: escalar en X vacía la barra de derecha a izquierda.
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(border, border);
            fill.offsetMax = new Vector2(border, -border);
            fill.sizeDelta = new Vector2(w - border * 2f, fill.sizeDelta.y);
            _fill = fill;
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.localScale = Vector3.one;

            // Un Image sin sprite dibuja un rectángulo sólido: perfecto como placeholder.
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rt;
        }
    }
}
