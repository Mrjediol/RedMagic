using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Mantiene el objeto pegado a la cámara principal con un desplazamiento. Para emisores de partículas de
    /// ambiente (nieve): el emisor va con la cámara y las partículas, que viven en espacio de mundo, se quedan
    /// donde caen.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public class FollowCameraView : MonoBehaviour
    {
        [Tooltip("Desplazamiento respecto al centro de la cámara, en unidades.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 11f);

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            transform.position = new Vector3(c.x + offset.x, c.y + offset.y, transform.position.z);
        }
    }
}
