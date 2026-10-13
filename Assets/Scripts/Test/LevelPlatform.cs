using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Plataforma con arte (una imagen). Mide SIEMPRE celdas enteras de ancho (<see cref="cellsWide"/>; 1 celda = 1
    /// unidad) y el alto sale de la proporción de la imagen: en vertical no importa, es sólo visual. La colisión es una
    /// línea fina de un solo sentido en la cara de arriba (se atraviesa desde abajo y se puede bajar de ella, capa
    /// Platform); lo que cuelga por debajo no tiene hitbox.
    /// El origen es el extremo IZQUIERDO de la cara de arriba: colocada en una esquina de la cuadrícula, la plataforma
    /// ocupa [x, x + cellsWide] y su superficie está en y.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LevelPlatform : MonoBehaviour
    {
        public const int MinWide = 1, MaxWide = 40;
        public const string ArtName = "Arte";

        [Tooltip("Imagen de la plataforma (pivote arriba-izquierda, 1 unidad de ancho: lo prepara el menú de pruebas).")]
        [SerializeField] private Sprite art;

        [Tooltip("Ancho en celdas enteras.")]
        [SerializeField, Range(MinWide, MaxWide)] private int cellsWide = 4;

        [Tooltip("Grosor INICIAL de la línea de colisión (unidades). Después se ajusta en el BoxCollider2D y no se pisa.")]
        [SerializeField, Min(0.1f)] private float colliderThickness = 0.12f;

        [Tooltip("Cuánto baja la línea de colisión desde el borde de arriba de la imagen (el contorno oscuro). Sólo al crearla.")]
        [SerializeField, Min(0f)] private float surfaceDrop = 0.04f;

        public int CellsWide => cellsWide;

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            cellsWide = Mathf.Clamp(cellsWide, MinWide, MaxWide);
            // OnValidate no puede tocar GameObjects: se pide el rebuild al siguiente tick del editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
#endif
        }

        /// <summary>Desde código (importador, menús).</summary>
        public void Configure(Sprite newArt, int wide)
        {
            art = newArt;
            cellsWide = Mathf.Clamp(wide, MinWide, MaxWide);
            Rebuild();
        }

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            BuildCollider();
            var child = transform.Find(ArtName);
            if (art == null)
            {
                if (child != null) child.gameObject.SetActive(false);
                return;
            }
            if (child == null)
            {
                var go = new GameObject(ArtName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            child.gameObject.SetActive(true);
            if (!child.TryGetComponent<SpriteRenderer>(out var sr)) sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sortingLayerName = LevelSorting.Layer;
            sr.sortingOrder = LevelSorting.OverTerrain;

            // anchura exacta en celdas; la misma escala en Y para no deformar los ladrillos
            float scale = cellsWide / Mathf.Max(0.0001f, art.bounds.size.x);
            child.localPosition = Vector3.zero;
            child.localScale = new Vector3(scale, scale, 1f);
        }

        private void BuildCollider()
        {
            int layer = LayerMask.NameToLayer("Platform");
            if (layer >= 0) gameObject.layer = layer;
            if (!TryGetComponent<BoxCollider2D>(out var box)) box = gameObject.AddComponent<BoxCollider2D>();
            // Sólo el ANCHO lo manda el componente. El alto y la posición vertical se respetan si ya existen: así lo que se
            // ajuste a mano en el BoxCollider2D no se pisa al dar a Play. "colliderThickness" sólo vale al crearlo.
            bool created = box.size == new Vector2(1f, 1f) && box.offset == Vector2.zero;
            float thickness = created ? colliderThickness : box.size.y;
            float offsetY = created ? -surfaceDrop - colliderThickness * 0.5f : box.offset.y;
            box.size = new Vector2(cellsWide, thickness);
            box.offset = new Vector2(cellsWide * 0.5f, offsetY);
            box.usedByEffector = true;
            if (!TryGetComponent<PlatformEffector2D>(out var effector)) effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // la línea de colisión, en celdas enteras
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(cellsWide, 0f));
        }
#endif
    }
}
