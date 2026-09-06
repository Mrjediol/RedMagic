using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Seguimiento de cámara simple para scroll lateral. Por defecto sólo sigue en horizontal,
    /// que es lo habitual en un 2D side-scroller; activa <see cref="followVertical"/> si quieres
    /// que también acompañe en vertical.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] private Transform target;
        [Tooltip("Si no hay target asignado, busca un GameObject con esta etiqueta al arrancar.")]
        [SerializeField] private string fallbackTargetTag = "Player";

        [Header("Encuadre")]
        [SerializeField] private Vector2 offset = new Vector2(1.5f, 0.8f);
        [SerializeField] private float smoothTime = 0.18f;
        [SerializeField] private bool followVertical;

        [Header("Límites (opcional)")]
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds = new Vector2(-20f, -5f);
        [SerializeField] private Vector2 maxBounds = new Vector2(20f, 10f);

        private Vector3 _velocity;

        private void Start()
        {
            if (target != null || string.IsNullOrEmpty(fallbackTargetTag)) return;

            var found = GameObject.FindGameObjectWithTag(fallbackTargetTag);
            if (found != null) target = found.transform;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 current = transform.position;

            float desiredX = target.position.x + offset.x;
            float desiredY = followVertical ? target.position.y + offset.y : current.y;

            if (useBounds)
            {
                desiredX = Mathf.Clamp(desiredX, minBounds.x, maxBounds.x);
                if (followVertical) desiredY = Mathf.Clamp(desiredY, minBounds.y, maxBounds.y);
            }

            var desired = new Vector3(desiredX, desiredY, current.z);
            transform.position = Vector3.SmoothDamp(current, desired, ref _velocity, smoothTime);
        }

        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
