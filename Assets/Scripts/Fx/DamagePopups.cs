using RedMagic.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.Fx
{
    /// <summary>Qué representa un número flotante, para colorearlo.</summary>
    public enum DamagePopupKind
    {
        /// <summary>Daño que el jugador (o sus disparos) reparten a un enemigo / al maniquí.</summary>
        DealtToEnemy,
        /// <summary>Daño que el jugador recibe.</summary>
        PlayerHurt,
    }

    /// <summary>
    /// Números de daño flotantes. Se auto-crea antes de la primera escena (no hay que ponerlo en
    /// ninguna) y sobrevive a los cambios de escena.
    ///
    /// Se suscribe una sola vez al evento global <see cref="Health.AnyDamaged"/> y saca un número
    /// por golpe encima del objetivo: cálido si el daño lo recibe un enemigo o el maniquí de
    /// pruebas, rojo si lo recibe el jugador (se distingue por la etiqueta <c>Player</c>). Cubre a
    /// todos los personajes —incluidos los que aparecen a mitad de sección— sin escanear la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamagePopups : MonoBehaviour
    {
        public static DamagePopups Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[DamagePopups]").AddComponent<DamagePopups>();
        }

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
        }

        private void OnEnable() => Health.AnyDamaged += OnDamaged;

        private void OnDisable() => Health.AnyDamaged -= OnDamaged;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDamaged(Health health, float amount)
        {
            if (health == null || amount <= 0f) return;

            var kind = health.CompareTag("Player") ? DamagePopupKind.PlayerHurt : DamagePopupKind.DealtToEnemy;

            Vector3 anchor = health.transform.position + Vector3.up * 0.9f;
            Show(anchor, amount, kind);
        }

        /// <summary>Saca un número flotante a mano (además del enganche automático a los Health).</summary>
        public static void Show(Vector3 worldPosition, float amount, DamagePopupKind kind)
        {
            float jitterX = Random.Range(-0.25f, 0.25f);
            DamagePopup.Spawn(worldPosition + new Vector3(jitterX, 0f, 0f), amount, kind);
        }
    }

    /// <summary>
    /// Un número de daño: sube, se desplaza un poco al azar, hace un "pop" de escala y se
    /// desvanece. Se construye entero en código (Canvas en world space + <see cref="Text"/> con la
    /// fuente incorporada <c>LegacyRuntime.ttf</c>), sin prefab ni asset de fuente, y va <b>pooled</b>
    /// (<see cref="Core.Pool{T}"/>): el GameObject se construye una vez y se reutiliza.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        private const float Lifetime = 0.85f;
        private const float RiseSpeed = 2.2f;
        private const float UnitsPerWorldUnit = 100f;

        private static Font _font;
        private static Core.Pool<DamagePopup> _pool;

        private Text _text;
        private Color _baseColor;
        private float _age;
        private Vector3 _velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        public static DamagePopup Spawn(Vector3 worldPosition, float amount, DamagePopupKind kind)
        {
            _pool ??= new Core.Pool<DamagePopup>(Build, prewarm: 16);

            var popup = _pool.Get();
            popup.Begin(worldPosition, amount, kind);
            return popup;
        }

        /// <summary>Construcción cara: se hace una vez por instancia del pool.</summary>
        private static DamagePopup Build()
        {
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("DamagePopup", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one / UnitsPerWorldUnit;
            rt.sizeDelta = new Vector2(400f, 120f);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = _font;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var popup = go.AddComponent<DamagePopup>();
            popup._text = text;
            return popup;
        }

        /// <summary>Datos por golpe: se reaplica cada vez que sale del pool.</summary>
        private void Begin(Vector3 worldPosition, float amount, DamagePopupKind kind)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one / UnitsPerWorldUnit * 0.6f;
            _age = 0f;

            bool hurt = kind == DamagePopupKind.PlayerHurt;
            _baseColor = hurt
                ? new Color(1f, 0.4f, 0.35f)
                : Color.Lerp(new Color(1f, 0.96f, 0.78f), new Color(1f, 0.7f, 0.3f), Mathf.InverseLerp(8f, 40f, amount));

            _text.text = (hurt ? "-" : "") + Format(amount);
            _text.color = _baseColor;
            _text.fontSize = Mathf.RoundToInt(Mathf.Lerp(34f, 64f, Mathf.InverseLerp(4f, 45f, amount)));

            _velocity = new Vector3(Random.Range(-0.6f, 0.6f), RiseSpeed, 0f);
        }

        private static string Format(float amount) =>
            amount >= 10f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#");

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Lifetime;
            if (t >= 1f)
            {
                _pool.Release(this);
                return;
            }

            _velocity.y = Mathf.Lerp(_velocity.y, 0f, Time.deltaTime * 3f);
            _velocity.x = Mathf.Lerp(_velocity.x, 0f, Time.deltaTime * 4f);
            transform.position += _velocity * Time.deltaTime;

            // "Pop" de escala: 0.6 -> 1.1 en el primer 12 %, luego asienta a 1.
            float scale = t < 0.12f
                ? Mathf.Lerp(0.6f, 1.1f, t / 0.12f)
                : Mathf.Lerp(1.1f, 1f, Mathf.InverseLerp(0.12f, 0.3f, t));
            transform.localScale = Vector3.one / UnitsPerWorldUnit * scale;

            // Se desvanece en el último 45 %.
            float alpha = t < 0.55f ? 1f : 1f - Mathf.InverseLerp(0.55f, 1f, t);
            var c = _baseColor;
            c.a = alpha;
            _text.color = c;
        }
    }
}
