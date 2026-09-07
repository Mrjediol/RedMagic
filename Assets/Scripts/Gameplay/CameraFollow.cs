using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Seguimiento de cámara para scroll lateral con verticalidad.
    ///
    /// Los dos ejes se tratan distinto a propósito:
    ///  - <b>horizontal</b>: suavizado con <see cref="smoothTime"/>, que es lo que hace que correr
    ///    se sienta bien;
    ///  - <b>vertical</b>: <b>pegado al jugador, sin retraso</b>. Un suavizado vertical hace que la
    ///    cámara llegue tarde al salto y a la caída, y con eso no se puede jugar.
    ///    <see cref="verticalSmoothTime"/> queda como ajuste, pero su valor por defecto es 0 =
    ///    instantáneo; súbelo sólo si alguna escena concreta lo pide.
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
        [Tooltip("Suavizado del eje horizontal.")]
        [SerializeField] private float smoothTime = 0.18f;
        [SerializeField] private bool followVertical = true;

        [Tooltip("Suavizado del eje vertical. 0 = la cámara va pegada al jugador al subir y al " +
                 "bajar, sin retraso; es lo que hace jugable la verticalidad.")]
        [Min(0f)]
        [SerializeField] private float verticalSmoothTime;

        [Header("Límites (opcional)")]
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds = new Vector2(-20f, -5f);
        [SerializeField] private Vector2 maxBounds = new Vector2(20f, 10f);

        private float _velocityX;
        private float _velocityY;

        private float _shakeAmplitude;
        private float _shakeTimer;
        private float _shakeDuration;

        /// <summary>
        /// Cámaras vivas ahora mismo. Es lo que permite sacudir la cámara desde cualquier sitio
        /// (un pisotón de jefe, una explosión) sin que quien la sacude tenga una referencia ni
        /// tenga que buscarla en la escena cada vez.
        ///
        /// Domain Reload está desactivado, así que la lista se limpia al arrancar el runtime o
        /// arrastraría cámaras destruidas de la sesión de Play anterior.
        /// </summary>
        private static readonly List<CameraFollow> Live = new List<CameraFollow>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => Live.Clear();

        private void OnEnable() => Live.Add(this);

        private void OnDisable() => Live.Remove(this);

        private void Start()
        {
            if (target != null || string.IsNullOrEmpty(fallbackTargetTag)) return;

            var found = GameObject.FindGameObjectWithTag(fallbackTargetTag);
            if (found != null) target = found.transform;
        }

        /// <summary>Sacude esta cámara: amplitud en unidades de mundo, decayendo hasta cero.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;

            // Una sacudida nueva no corta la anterior: se queda con la más fuerte y la más larga,
            // para que dos impactos seguidos no bajen el temblor a la mitad.
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTimer = Mathf.Max(_shakeTimer, duration);
        }

        /// <summary>Sacude todas las cámaras activas. Es el punto de entrada normal.</summary>
        public static void ShakeAll(float amplitude, float duration)
        {
            for (int i = 0; i < Live.Count; i++) Live[i]?.Shake(amplitude, duration);
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

            float newX = Mathf.SmoothDamp(current.x, desiredX, ref _velocityX, smoothTime);

            // Vertical sin suavizado por defecto: la cámara se planta en la altura del jugador el
            // mismo frame. Cualquier retraso aquí hace que en un salto o una caída no veas dónde
            // vas a aterrizar hasta que ya has aterrizado.
            float newY = !followVertical
                ? current.y
                : verticalSmoothTime > 0f
                    ? Mathf.SmoothDamp(current.y, desiredY, ref _velocityY, verticalSmoothTime)
                    : desiredY;

            transform.position = new Vector3(newX, newY, current.z) + ConsumeShakeOffset();
        }

        /// <summary>
        /// Desplazamiento del temblor de este frame. Se aplica <b>después</b> del suavizado y no
        /// se acumula en la posición base, así que la cámara vuelve sola a su encuadre y el
        /// SmoothDamp no persigue el ruido.
        /// </summary>
        private Vector3 ConsumeShakeOffset()
        {
            if (_shakeTimer <= 0f) return Vector3.zero;

            _shakeTimer -= Time.unscaledDeltaTime;

            if (_shakeTimer <= 0f)
            {
                _shakeTimer = 0f;
                _shakeAmplitude = 0f;
                return Vector3.zero;
            }

            float falloff = _shakeDuration <= 0f ? 0f : _shakeTimer / _shakeDuration;
            float amount = _shakeAmplitude * falloff * falloff;

            return new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), 0f);
        }

        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
