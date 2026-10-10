using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Capa de fondo con parallax para un nivel de cualquier tamaño. La capa es UN sprite repetido
    /// (Tiled) lo bastante grande para que la cámara nunca vea su borde mientras se mueva por el
    /// nivel; lo que se mueve es la capa, siguiendo a la cámara a una fracción de su velocidad.
    ///
    /// Con factor 0 la capa es del nivel (no se mueve); con factor 1 va pegada a la cámara (no
    /// da sensación de profundidad). El punto de anclaje es el centro del nivel: con la cámara ahí,
    /// la capa está centrada. Se ejecuta después de <see cref="CameraFollow"/> para no ir un frame
    /// por detrás de la cámara.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Fracción del movimiento de la cámara que sigue la capa. Más cerca de 0 = más lejana.")]
        [SerializeField] private Vector2 factor = new Vector2(0.15f, 0.08f);

        [Tooltip("Centro del nivel en coordenadas locales del padre (el nivel).")]
        [SerializeField] private Vector2 localAnchor;

        private Camera _camera;

        public void Configure(Vector2 newFactor, Vector2 newLocalAnchor)
        {
            factor = newFactor;
            localAnchor = newLocalAnchor;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector2 anchor = transform.parent != null
                ? (Vector2)transform.parent.TransformPoint(localAnchor)
                : localAnchor;
            Vector3 cam = _camera.transform.position;

            transform.position = new Vector3(
                cam.x * factor.x + anchor.x * (1f - factor.x),
                cam.y * factor.y + anchor.y * (1f - factor.y),
                transform.position.z);
        }
    }
}
