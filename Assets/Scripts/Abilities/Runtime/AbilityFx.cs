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

        /// <summary>Efecto de un solo uso: aparece, se desvanece y se destruye solo.</summary>
        public static GameObject Flash(Sprite sprite, Vector3 position, Vector2 size, Color color,
                                       float duration, float rotationDegrees = 0f,
                                       float growTo = 1f, GameObject sortingReference = null)
        {
            var go = SpawnSprite("Ability FX", sprite, position, size, color, rotationDegrees, sortingReference);
            var fade = go.AddComponent<AbilityVfx>();
            fade.Play(duration, growTo);
            return go;
        }

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
    /// Desvanece y opcionalmente agranda un sprite durante unos segundos y se destruye. Es el
    /// "impacto" genérico de las habilidades sin arte propio.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityVfx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _startColor;
        private Vector3 _startScale;
        private float _duration = 0.2f;
        private float _timer;
        private float _growTo = 1f;

        /// <param name="duration">Segundos hasta desaparecer.</param>
        /// <param name="growTo">Escala final relativa (1 = no crece, 2 = duplica su tamaño).</param>
        public void Play(float duration, float growTo = 1f)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _startColor = _renderer != null ? _renderer.color : Color.white;
            _startScale = transform.localScale;
            _duration = Mathf.Max(0.01f, duration);
            _growTo = Mathf.Max(0.01f, growTo);
            _timer = 0f;
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

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
