using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Marca un prefab como <b>placeholder</b>: una forma geométrica blanca que el spawner debe
    /// seguir tiñendo, redimensionando y ordenando por instancia — exactamente como hacía con el
    /// objeto que antes construía en código.
    ///
    /// El arte final <b>no</b> lleva este componente (o lo lleva con los flags a false): entonces
    /// el spawner sólo coloca la instancia y no toca nada más — su sprite, su color y su tamaño
    /// mandan.
    ///
    /// Es el único sitio donde vive "cómo se estiliza un visual placeholder", para que los ~10
    /// spawners (proyectiles de arma, balas de jefe, avisos, ondas, hazards, plataformas…) no lo
    /// repitan cada uno.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FxPlaceholderStyle : MonoBehaviour
    {
        [Tooltip("Tiñe el SpriteRenderer con el color del disparo / de la fase del jefe.")]
        [SerializeField] private bool tint = true;

        [Tooltip("Escala el transform para que el sprite mida el tamaño en unidades que pide el asset.")]
        [SerializeField] private bool resize = true;

        [Tooltip("Copia la capa de ordenación del lanzador para salir por delante del escenario.")]
        [SerializeField] private bool matchSorting = true;

        [Tooltip("Ajusta el collider (círculo o caja) al tamaño del sprite tras redimensionar. " +
                 "Igual que hacía ShotProjectile.Build: el radio/size crudo del sprite, y la escala " +
                 "del transform hace el resto.")]
        [SerializeField] private bool scaleColliderToSprite = true;

        private SpriteRenderer _renderer;
        private CircleCollider2D _circle;
        private BoxCollider2D _box;
        private bool _cached;

        public SpriteRenderer Renderer { get { Cache(); return _renderer; } }

        private void Awake() => Cache();

        private void Cache()
        {
            if (_cached) return;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _circle = GetComponent<CircleCollider2D>();
            _box = GetComponent<BoxCollider2D>();
            _cached = true;
        }

        /// <summary>
        /// Aplica el estilo de esta instancia: color, tamaño en unidades del mundo y capa de
        /// ordenación (respecto a <paramref name="sortingRef"/>, el lanzador). Cada paso respeta
        /// su flag.
        /// </summary>
        public void Apply(Color color, Vector2 worldSize, GameObject sortingRef)
        {
            Cache();
            if (_renderer == null) return;

            if (tint) _renderer.color = color;
            if (matchSorting) AbilityFx.CopySorting(_renderer, sortingRef);

            if (resize)
            {
                AbilityFx.Resize(_renderer.transform, _renderer, worldSize);
                if (scaleColliderToSprite) ScaleColliderToSprite();
            }
        }

        /// <summary>
        /// Deja el collider a las dimensiones <b>crudas</b> del sprite (sin escala): la escala del
        /// transform, que <see cref="AbilityFx.Resize"/> acaba de fijar, lo lleva al tamaño real.
        /// </summary>
        private void ScaleColliderToSprite()
        {
            if (_renderer.sprite == null) return;
            var bounds = _renderer.sprite.bounds.size;

            if (_circle != null) _circle.radius = Mathf.Max(bounds.x, bounds.y) * 0.5f;
            if (_box != null) _box.size = new Vector2(bounds.x, bounds.y);
        }
    }
}
