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

        private SpriteRenderer _renderer;
        private FxPlaceholderStyle _style;
        private bool _cached;

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
            _cached = true;
        }

        private void Begin(Vector2 size, Color color, float duration, float growTo, GameObject sortingRef)
        {
            Cache();

            if (_style != null) _style.Apply(color, size, sortingRef);
            else if (_renderer != null) _renderer.color = color;

            _startColor = _renderer != null ? _renderer.color : color;
            _startScale = transform.localScale;
            _duration = duration;
            _growTo = growTo;
            _timer = 0f;
            _running = true;
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
