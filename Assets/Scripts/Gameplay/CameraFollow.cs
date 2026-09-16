using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>De dónde salen los límites de encuadre de la cámara.</summary>
    public enum CameraBoundsSource
    {
        /// <summary>Sin límites: la cámara sigue al jugador hasta donde haga falta.</summary>
        None,

        /// <summary>
        /// Del fondo de la escena, por convención el objeto llamado <c>BG</c>. Es el modo normal:
        /// el arte de fondo de cada nivel no tiene margen sobrante, así que sus bordes <b>son</b>
        /// los límites del encuadre y no hay nada que teclear por escena.
        /// </summary>
        Background,

        /// <summary>Rectángulo escrito a mano en el inspector, para una escena sin fondo.</summary>
        Manual
    }

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
    ///
    /// <b>Límites</b>: lo que se acota es el <b>rectángulo que se ve</b>, no el centro de la
    /// cámara. El borde de la pantalla se para justo en el borde del fondo, así que nunca asoma
    /// el vacío de más allá del arte.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>Nombre por convención del fondo de una escena.</summary>
        public const string DefaultBackgroundName = "BG";

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

        [Header("Límites")]
        [Tooltip("De dónde salen los límites. Background = del objeto 'BG' de esta misma escena.")]
        [SerializeField] private CameraBoundsSource boundsSource = CameraBoundsSource.Background;

        [Tooltip("Nombre del objeto de fondo dentro de esta escena. Se admiten hijos.")]
        [SerializeField] private string backgroundName = DefaultBackgroundName;

        [Tooltip("Margen hacia dentro del fondo, en unidades de mundo. Súbelo si el arte trae " +
                 "unos píxeles de basura en el borde.")]
        [Min(0f)]
        [SerializeField] private float boundsPadding;

        [Tooltip("Sólo en modo Manual: esquina inferior izquierda de la zona visible.")]
        [SerializeField] private Vector2 minBounds = new Vector2(-20f, -5f);

        [Tooltip("Sólo en modo Manual: esquina superior derecha de la zona visible.")]
        [SerializeField] private Vector2 maxBounds = new Vector2(20f, 10f);

        private float _velocityX;
        private float _velocityY;

        private float _shakeAmplitude;
        private float _shakeTimer;
        private float _shakeDuration;

        private Camera _camera;
        private Bounds _limit;
        private bool _hasLimit;
        private bool _resolved;

        /// <summary>Rectángulo de mundo fuera del cual la cámara no debe enseñar nada.</summary>
        public Bounds Limit => _limit;

        /// <summary>Si hay límites resueltos ahora mismo.</summary>
        public bool HasLimit => _hasLimit;

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

        private void Awake() => _camera = GetComponent<Camera>();

        private void Start()
        {
            RefreshBounds();

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
            if (!_resolved) RefreshBounds();

            Vector3 current = transform.position;

            float desiredX = target.position.x + offset.x;
            float desiredY = followVertical ? target.position.y + offset.y : current.y;

            // Se acota el destino ANTES de suavizar: si se acotara sólo el resultado, el
            // SmoothDamp seguiría acelerando hacia un punto prohibido y al separarse del borde la
            // cámara daría un tirón con toda la velocidad acumulada contra la pared.
            ClampCenter(ref desiredX, ref desiredY);

            float newX = Mathf.SmoothDamp(current.x, desiredX, ref _velocityX, smoothTime);

            // Vertical sin suavizado por defecto: la cámara se planta en la altura del jugador el
            // mismo frame. Cualquier retraso aquí hace que en un salto o una caída no veas dónde
            // vas a aterrizar hasta que ya has aterrizado.
            float newY = !followVertical
                ? current.y
                : verticalSmoothTime > 0f
                    ? Mathf.SmoothDamp(current.y, desiredY, ref _velocityY, verticalSmoothTime)
                    : desiredY;

            Vector3 next = new Vector3(newX, newY, current.z) + ConsumeShakeOffset();

            // Y se vuelve a acotar el resultado, porque el temblor se suma después del suavizado y
            // por sí solo bastaría para asomar medio metro de vacío en el borde del nivel.
            float clampedX = next.x, clampedY = next.y;
            ClampCenter(ref clampedX, ref clampedY);

            transform.position = new Vector3(clampedX, clampedY, next.z);
        }

        /// <summary>
        /// Lleva un centro de cámara al rectángulo en el que la <b>pantalla entera</b> cabe dentro
        /// de <see cref="Limit"/>. Es el cálculo entero del sistema: al rectángulo del fondo se le
        /// resta media pantalla por cada lado, y lo que queda es dónde puede estar el centro.
        ///
        /// Si el fondo es más pequeño que la pantalla en un eje no hay posición válida — no se
        /// puede tapar lo que no existe — así que en ese eje la cámara se queda <b>centrada en el
        /// fondo</b>, que reparte el sobrante a partes iguales en vez de enseñarlo todo de un lado.
        /// </summary>
        private void ClampCenter(ref float x, ref float y)
        {
            if (!_hasLimit) return;

            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null || !_camera.orthographic) return;

            // Se leen cada frame a propósito: RunManager fuerza el orthographicSize después de
            // cada carga, y la relación de aspecto cambia con el tamaño de la ventana.
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            Vector3 min = _limit.min, max = _limit.max;

            float minX = min.x + halfWidth, maxX = max.x - halfWidth;
            x = minX > maxX ? _limit.center.x : Mathf.Clamp(x, minX, maxX);

            float minY = min.y + halfHeight, maxY = max.y - halfHeight;
            y = minY > maxY ? _limit.center.y : Mathf.Clamp(y, minY, maxY);
        }

        /// <summary>
        /// Vuelve a leer los límites. Se llama solo en <c>Start</c>; <c>RunManager</c> lo repite
        /// tras cargar una sección, porque ahí el orden entre su retargeting y el <c>Start</c> de
        /// la cámara de la escena recién cargada no está garantizado.
        /// </summary>
        [ContextMenu("Releer límites")]
        public void RefreshBounds()
        {
            _resolved = true;
            _hasLimit = TryResolveBounds(out _limit);
        }

        private bool TryResolveBounds(out Bounds result)
        {
            result = default;

            switch (boundsSource)
            {
                case CameraBoundsSource.None:
                    return false;

                case CameraBoundsSource.Manual:
                    result = FromCorners(minBounds, maxBounds);
                    break;

                default:
                    // No encontrar fondo no es un error: una escena de prueba puede no tenerlo
                    // todavía, y sin límites la cámara se comporta exactamente como antes.
                    if (!TryMeasureBackground(out result)) return false;
                    break;
            }

            if (boundsPadding > 0f)
            {
                result.size = new Vector3(
                    Mathf.Max(0f, result.size.x - boundsPadding * 2f),
                    Mathf.Max(0f, result.size.y - boundsPadding * 2f),
                    result.size.z);
            }

            return result.size.x > 0f && result.size.y > 0f;
        }

        /// <summary>
        /// Mide el fondo de <b>esta</b> escena. Buscar por escena y no con un <c>Find</c> global
        /// es obligatorio: las secciones se cargan en aditivo sobre la escena raíz de la run, así
        /// que durante una transición hay más de un <c>BG</c> cargado y el de otra escena daría
        /// los límites de un nivel que no se está jugando.
        /// </summary>
        private bool TryMeasureBackground(out Bounds result)
        {
            result = default;

            string wanted = string.IsNullOrEmpty(backgroundName) ? DefaultBackgroundName : backgroundName;
            var scene = gameObject.scene;
            if (!scene.IsValid()) return false;

            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindByName(root.transform, wanted);
                if (found == null) continue;
                if (TryMeasureRenderers(found, out result)) return true;
            }

            return false;
        }

        private static Transform FindByName(Transform root, string wanted)
        {
            if (root.name == wanted) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindByName(root.GetChild(i), wanted);
                if (hit != null) return hit;
            }

            return null;
        }

        /// <summary>
        /// Mide el fondo. <b>Si el propio objeto <c>BG</c> tiene <c>Renderer</c>, manda él solo</b>
        /// y sus hijos se ignoran.
        ///
        /// Esto no es un detalle: del <c>BG</c> cuelga decorado del nivel (plataformas, marcos,
        /// enredaderas) que sobresale unos centímetros del dibujo de fondo. Unir todo eso agranda
        /// el límite <i>más allá del arte</i> — medido en <c>World1_Boss</c>: la imagen acaba en
        /// x 22.82 y la unión llegaba a 22.95, o sea 13 cm de nada asomando por la derecha, que es
        /// justo lo que este sistema existe para impedir. El fondo es la imagen de fondo; lo que
        /// cuelga de ella es contenido.
        ///
        /// Sólo cuando <c>BG</c> es un contenedor vacío se unen sus hijos: ése es el caso del
        /// fondo partido en capas de parallax, donde ninguna capa por sí sola es el fondo.
        /// </summary>
        private static bool TryMeasureRenderers(Transform root, out Bounds result)
        {
            result = default;

            if (root.TryGetComponent<Renderer>(out var own) && IsMeasurable(own.bounds))
            {
                result = own.bounds;
                return true;
            }

            bool any = false;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !IsMeasurable(renderer.bounds)) continue;

                if (!any)
                {
                    result = renderer.bounds;
                    any = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        private static bool IsMeasurable(Bounds bounds) => bounds.size.x > 0f && bounds.size.y > 0f;

        private static Bounds FromCorners(Vector2 min, Vector2 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), -1f),
                             new Vector3(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), 1f));
            return bounds;
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

#if UNITY_EDITOR
        /// <summary>
        /// Dibuja los dos rectángulos que hay que ver para encuadrar un nivel: en verde el fondo
        /// (el borde que no se puede pasar) y en ámbar dónde puede estar el centro de la cámara.
        /// Si el ámbar no cabe, se pinta en rojo la pantalla completa sobre el fondo: ahí se ve de
        /// un vistazo cuánto le falta al arte para cubrirla.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) RefreshBounds();
            if (!_hasLimit) return;

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(_limit.center, new Vector3(_limit.size.x, _limit.size.y, 0f));

            var cam = _camera != null ? _camera : GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            var span = new Vector3(_limit.size.x - halfWidth * 2f, _limit.size.y - halfHeight * 2f, 0f);

            if (span.x <= 0f || span.y <= 0f)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                Gizmos.DrawWireCube(_limit.center, new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));
                return;
            }

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(_limit.center, span);
        }
#endif
    }
}
