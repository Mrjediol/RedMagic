using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Fábrica de los objetos visuales de las habilidades.
    ///
    /// Todo se construye en código a partir de un sprite y un color porque el objetivo de esta
    /// primera tanda es <b>probar mecánicas, no arte</b>: una habilidad nueva es un asset con
    /// números y ya se ve algo en pantalla. Cuando haya arte de verdad basta con asignar el sprite
    /// (o un prefab) en el asset y estas formas dejan de usarse.
    /// </summary>
    public static class AbilityFx
    {
        private static Sprite _defaultSprite;
        private static Core.Pool<AbilityVfx> _flashPool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _flashPool = null;

        /// <summary>
        /// Cuadrado blanco generado en memoria, para no depender de ningún asset de arte. Se crea
        /// una vez y se reutiliza.
        /// </summary>
        public static Sprite DefaultSprite
        {
            get
            {
                if (_defaultSprite != null) return _defaultSprite;

                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
                {
                    name = "AbilityFxDefault",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var pixels = new Color32[64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();

                _defaultSprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
                _defaultSprite.name = "AbilityFxDefault";
                _defaultSprite.hideFlags = HideFlags.HideAndDontSave;
                return _defaultSprite;
            }
        }

        /// <summary>
        /// Crea un GameObject con un SpriteRenderer ya escalado a <paramref name="size"/> en
        /// unidades del mundo. Sin sprite usa el cuadrado por defecto.
        /// </summary>
        public static GameObject SpawnSprite(string objectName, Sprite sprite, Vector3 position,
                                             Vector2 size, Color color, float rotationDegrees = 0f,
                                             GameObject sortingReference = null)
        {
            var go = new GameObject(objectName);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : DefaultSprite;
            renderer.color = color;

            CopySorting(renderer, sortingReference);
            Resize(go.transform, renderer, size);

            return go;
        }

        /// <summary>
        /// Efecto de un solo uso: aparece, se desvanece y vuelve al pool. Es el efecto más spammeado
        /// del juego (un flash por impacto), así que va <b>pooled</b> (<see cref="Core.Pool{T}"/>):
        /// el GameObject + SpriteRenderer se crean una vez y se reutilizan.
        /// </summary>
        public static GameObject Flash(Sprite sprite, Vector3 position, Vector2 size, Color color,
                                       float duration, float rotationDegrees = 0f,
                                       float growTo = 1f, GameObject sortingReference = null)
        {
            _flashPool ??= new Core.Pool<AbilityVfx>(BuildFlash, prewarm: 16);

            var vfx = _flashPool.Get();
            vfx.Begin(sprite, position, size, color, rotationDegrees, growTo, sortingReference, duration);
            return vfx.gameObject;
        }

        private static AbilityVfx BuildFlash()
        {
            var go = new GameObject("Ability FX");
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<AbilityVfx>();
        }

        internal static void ReleaseFlash(AbilityVfx vfx) => _flashPool?.Release(vfx);

        /// <summary>
        /// Escala el transform para que el sprite mida <paramref name="size"/> unidades. Los
        /// sprites tienen tamaños y PPU distintos, así que sin esto cada efecto saldría de un
        /// tamaño y los números del asset no significarían nada.
        /// </summary>
        public static void Resize(Transform transform, SpriteRenderer renderer, Vector2 size)
        {
            var bounds = renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.one;
            if (bounds.x <= 0.0001f || bounds.y <= 0.0001f) return;

            transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
        }

        /// <summary>
        /// Copia la capa de ordenación del lanzador para que el efecto salga delante de él y no
        /// detrás del escenario. Sin esto, un efecto creado en código va a "Default / 0" y en un
        /// juego 2D con capas eso suele quedar tapado.
        /// </summary>
        public static void CopySorting(SpriteRenderer renderer, GameObject reference, int orderOffset = 1)
        {
            if (reference == null)
            {
                renderer.sortingOrder = 50;
                return;
            }

            var source = reference.GetComponentInChildren<SpriteRenderer>();
            if (source == null)
            {
                renderer.sortingOrder = 50;
                return;
            }

            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + orderOffset;
        }
    }

    /// <summary>
    /// Desvanece y opcionalmente agranda un sprite durante unos segundos y vuelve al pool. Es el
    /// "impacto" genérico de las habilidades sin arte propio. Lo saca y lo reaprovecha
    /// <see cref="AbilityFx.Flash"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityVfx : MonoBehaviour, Core.IPooled
    {
        private SpriteRenderer _renderer;
        private Color _startColor;
        private Vector3 _startScale;
        private float _duration = 0.2f;
        private float _timer;
        private float _growTo = 1f;

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>Prepara el efecto desde el pool: sprite, color, tamaño, orientación y duración.</summary>
        internal void Begin(Sprite sprite, Vector3 position, Vector2 size, Color color,
                            float rotationDegrees, float growTo, GameObject sortingReference, float duration)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            _renderer.sprite = sprite != null ? sprite : AbilityFx.DefaultSprite;
            _renderer.color = color;
            AbilityFx.CopySorting(_renderer, sortingReference);
            AbilityFx.Resize(transform, _renderer, size);

            _startColor = _renderer.color;
            _startScale = transform.localScale;
            _duration = Mathf.Max(0.01f, duration);
            _growTo = Mathf.Max(0.01f, growTo);
            _timer = 0f;
        }

        void Core.IPooled.OnReturnedToPool()
        {
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            if (_renderer != null)
            {
                var color = _startColor;
                color.a = _startColor.a * (1f - t);
                _renderer.color = color;
            }

            if (!Mathf.Approximately(_growTo, 1f))
                transform.localScale = _startScale * Mathf.Lerp(1f, _growTo, t);

            if (t >= 1f) AbilityFx.ReleaseFlash(this);
        }
    }
}
