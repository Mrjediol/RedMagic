using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Aviso / telegrafiado a partir de un <b>prefab</b>, pooled — el equivalente de
    /// <see cref="Abilities.AbilityFx.Flash"/> pero con un prefab editable en vez del cuadrado
    /// generado en código. Aparece en su sitio, (opcionalmente) crece y se desvanece durante
    /// <c>duration</c>, y vuelve al <see cref="Core.PrefabPool"/>.
    ///
    /// En modo placeholder (el prefab lleva <see cref="FxPlaceholderStyle"/>) se le aplica tinte y
    /// tamaño por instancia y hace el fundido. Un aviso con arte propio pone
    /// <see cref="fadeOut"/> a false y anima solo; este componente sólo lo coloca y lo recoge al
    /// acabar el tiempo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FxTelegraph : MonoBehaviour
    {
        [Tooltip("Desvanece el sprite a lo largo de la duración. Apágalo si el prefab anima su " +
                 "propia salida (Animator / partículas).")]
        [SerializeField] private bool fadeOut = true;

        /// <summary>Cómo cuadra un aviso con arte propio (sin <see cref="FxPlaceholderStyle"/>) con el tamaño pedido.</summary>
        public enum FitMode
        {
            /// <summary>La escala del prefab tal cual (lo de siempre).</summary>
            None,
            /// <summary>Escala uniforme para que el dibujo mida el ancho pedido: el círculo del radio, la flecha del recorrido.</summary>
            FitWidth,
        }

        [Tooltip("Sólo para arte propio (sin FxPlaceholderStyle). FitWidth = el dibujo se escala en " +
                 "uniforme hasta medir el ancho del aviso, así el círculo coincide con el radio que " +
                 "duele y la flecha con el recorrido del ataque.")]
        [SerializeField] private FitMode fit = FitMode.None;

        [Tooltip("Sólo para arte propio: tiñe el sprite con el color del aviso (el de la fase). " +
                 "Apágalo si el dibujo ya trae sus colores.")]
        [SerializeField] private bool applyColor = true;

        private SpriteRenderer _renderer;
        private FxPlaceholderStyle _style;
        private bool _cached;
        private Vector3 _baseScale = Vector3.one;
        private Color _baseColor = Color.white;

        private Color _startColor = Color.white;
        private Vector3 _startScale = Vector3.one;
        private float _duration = 0.2f;
        private float _growTo = 1f;
        private float _timer;
        private bool _running;

        /// <summary>
        /// Saca un aviso del pool. <paramref name="size"/> en unidades del mundo, <paramref name="growTo"/>
        /// = 1 no crece. Devuelve null si no hay prefab (se puede llamar sin comprobar).
        /// </summary>
        public static FxTelegraph Spawn(GameObject prefab, Vector3 position, Vector2 size, Color color,
                                        float duration, float rotationDegrees, float growTo,
                                        GameObject sortingRef)
        {
            if (prefab == null) return null;

            var go = Core.PrefabPool.Spawn(prefab, position, Quaternion.Euler(0f, 0f, rotationDegrees));
            if (go == null) return null;

            var fx = go.GetComponent<FxTelegraph>();
            if (fx == null) fx = go.AddComponent<FxTelegraph>();

            fx.Begin(size, color, Mathf.Max(0.02f, duration), Mathf.Max(0.01f, growTo), sortingRef);
            return fx;
        }

        private void Cache()
        {
            if (_cached) return;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _style = GetComponent<FxPlaceholderStyle>();
            // La primera vez que sale del pool la escala es la del prefab: es la base a la que se
            // vuelve en cada uso (el pool reutiliza instancias ya crecidas).
            _baseScale = transform.localScale;
            if (_renderer != null) _baseColor = _renderer.color;
            _cached = true;
        }

        private void Begin(Vector2 size, Color color, float duration, float growTo, GameObject sortingRef)
        {
            Cache();

            if (_style != null)
            {
                _style.Apply(color, size, sortingRef);
            }
            else
            {
                transform.localScale = _baseScale;
                // Sin teñir se vuelve al color del prefab: el fundido del uso anterior lo dejó a alfa 0.
                if (_renderer != null) _renderer.color = applyColor ? color : _baseColor;
                if (fit == FitMode.FitWidth) FitToWidth(size.x);
            }

            _startColor = _renderer != null ? _renderer.color : color;
            _startScale = transform.localScale;
            _duration = duration;
            _growTo = growTo;
            _timer = 0f;
            _running = true;
        }

        private void FitToWidth(float width)
        {
            if (_renderer == null || _renderer.sprite == null || width <= 0f) return;

            float child = _renderer.transform == transform ? 1f : Mathf.Abs(_renderer.transform.localScale.x);
            float natural = _renderer.sprite.bounds.size.x * child * Mathf.Abs(_baseScale.x);
            if (natural > 0.0001f) transform.localScale = _baseScale * (width / natural);
        }

        private void Update()
        {
            if (!_running) return;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            if (fadeOut && _renderer != null)
            {
                var c = _startColor;
                c.a = _startColor.a * (1f - t);
                _renderer.color = c;
            }

            if (!Mathf.Approximately(_growTo, 1f))
                transform.localScale = _startScale * Mathf.Lerp(1f, _growTo, t);

            if (t >= 1f)
            {
                _running = false;
                Core.PrefabPool.Despawn(gameObject);
            }
        }

        private void OnDisable() => _running = false;
    }
}
