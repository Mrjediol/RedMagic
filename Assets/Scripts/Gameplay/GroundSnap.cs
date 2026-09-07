using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Al aparecer (y con el botón del Inspector) lanza un rayo hacia abajo hasta el terreno —la
    /// capa <b>Ground</b>, la que se pinta con el Tile Painter— y baja el objeto para que se apoye
    /// en el suelo en vez de quedarse flotando.
    ///
    /// Pensado para props que <see cref="Run.RunManager"/> instancia a mitad de run (la tienda, la
    /// recompensa del jefe) en un punto que no está alineado con el suelo, o para colocar
    /// decoración a mano sin cuadrar la Y. Si no hay suelo debajo, no hace nada (deja el objeto
    /// donde estaba).
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundSnap : MonoBehaviour
    {
        [Tooltip("Capa(s) que cuentan como suelo. Por defecto 'Ground' (capa 6).")]
        [SerializeField] private LayerMask groundMask = 1 << 6;

        [Tooltip("Desde cuánto por encima del objeto empieza a buscar suelo.")]
        [Min(0f)]
        [SerializeField] private float castUp = 3f;

        [Tooltip("Cuánto por debajo busca suelo.")]
        [Min(0f)]
        [SerializeField] private float castDown = 40f;

        [Tooltip("Separación final respecto al suelo. Negativo lo clava un poco.")]
        [SerializeField] private float padding = 0f;

        [Tooltip("Apoya la BASE del sprite (bounds de los renderers). Off = apoya el pivote.")]
        [SerializeField] private bool useRendererBounds = true;

        [SerializeField] private bool snapOnStart = true;

        private void Start()
        {
            if (snapOnStart) Snap();
        }

        [ContextMenu("Apoyar en el suelo ahora")]
        public void Snap()
        {
            Vector2 origin = new Vector2(transform.position.x, transform.position.y + castUp);
            var hit = Physics2D.Raycast(origin, Vector2.down, castUp + castDown, groundMask);
            if (hit.collider == null) return;

            float footY = transform.position.y;
            if (useRendererBounds && TryGetWorldBounds(out var bounds))
                footY = bounds.min.y;

            float delta = hit.point.y + padding - footY;
            transform.position += new Vector3(0f, delta, 0f);
        }

        private bool TryGetWorldBounds(out Bounds bounds)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            bounds = default;
            bool any = false;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return any;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 top = transform.position + Vector3.up * castUp;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(top, top + Vector3.down * (castUp + castDown));
        }
    }
}
