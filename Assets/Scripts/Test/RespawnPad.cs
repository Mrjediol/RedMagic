using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Plataforma de reaparición: donde vuelve el jugador al clavarse en unos pinchos. Cada sala de parkour
    /// (<see cref="Run.ParkourRoom"/>) empieza con una; la sala la enciende mientras el jugador está dentro sin haberla
    /// terminado. Es decoración apoyada en el suelo: no tiene colisión.
    /// El origen del objeto es el punto del suelo bajo su centro; el jugador aparece <see cref="spawnHeight"/> por encima.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RespawnPad : MonoBehaviour
    {
        [Header("Arte")]
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;
        [Tooltip("Ancho en celdas.")]
        [Min(0.5f)] [SerializeField] private float width = 2.2f;
        [Tooltip("Aplasta o estira el arte en vertical (1 = como está).")]
        [Range(0.2f, 1.5f)] [SerializeField] private float heightScale = 1f;
        [Tooltip("Cuánto se hunde en el suelo (celdas), para que no flote sobre el borde del terreno.")]
        [Range(0f, 0.5f)] [SerializeField] private float sink = 0.1f;
        [SerializeField] private int sortingOrder = LevelSorting.OverTerrain + 2;

        [Header("Reaparición")]
        [Tooltip("Altura sobre el origen donde aparece el jugador (su centro).")]
        [Min(0f)] [SerializeField] private float spawnHeight = 1.1f;

        [Header("Al reaparecer")]
        [Tooltip("Pulso de tamaño al volver el jugador (1 = sin pulso).")]
        [Range(1f, 1.5f)] [SerializeField] private float pulseScale = 1.15f;
        [Min(0.01f)] [SerializeField] private float pulseDuration = 0.35f;

        /// <summary>La última que se encendió: a ella vuelve el jugador si se clava fuera de una sala de parkour.</summary>
        public static RespawnPad Last { get; private set; }

        public Vector3 SpawnPosition => transform.position + Vector3.up * spawnHeight;
        public bool Lit { get; private set; }

        private SpriteRenderer _sprite;
        private float _pulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Last = null;

        private void OnEnable()
        {
            EnsureSprite();
            Apply();
        }

        private void OnDisable()
        {
            if (Last == this) Last = null;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        /// <summary>Enciende o apaga el círculo de runas. Encender la hace la de reaparición por defecto (<see cref="Last"/>).</summary>
        public void SetLit(bool lit)
        {
            if (lit) Last = this;
            if (Lit == lit) return;
            Lit = lit;
            Apply();
        }

        /// <summary>El jugador acaba de volver aquí.</summary>
        public void Pulse() => _pulse = pulseDuration;

        private void Update()
        {
            if (_pulse <= 0f || _sprite == null) return;
            _pulse -= Time.deltaTime;
            float t = Mathf.Clamp01(_pulse / pulseDuration);
            float k = 1f + (pulseScale - 1f) * Mathf.Sin(t * Mathf.PI);
            _sprite.transform.localScale = BaseScale() * k;
        }

        private void EnsureSprite()
        {
            if (_sprite != null) return;
            var child = transform.Find("Arte");
            if (child == null)
            {
                var go = new GameObject("Arte");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            if (!child.TryGetComponent(out _sprite)) _sprite = child.gameObject.AddComponent<SpriteRenderer>();
        }

        private Vector3 BaseScale()
        {
            var s = _sprite != null ? _sprite.sprite : null;
            if (s == null) return Vector3.one;
            float k = width / Mathf.Max(0.01f, s.bounds.size.x);
            return new Vector3(k, k * heightScale, 1f);
        }

        private void Apply()
        {
            EnsureSprite();
            _sprite.sprite = Lit && onSprite != null ? onSprite : offSprite != null ? offSprite : onSprite;
            _sprite.sortingLayerName = LevelSorting.Layer;
            _sprite.sortingOrder = sortingOrder;
            var scale = BaseScale();
            _sprite.transform.localScale = scale;
            // apoyado en el origen: la base del sprite (sea cual sea su pivote) en y = -sink
            var s = _sprite.sprite;
            float bottom = s != null ? s.bounds.min.y * scale.y : 0f;
            _sprite.transform.localPosition = new Vector3(0f, -bottom - sink, 0f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.9f, 0.9f);
            Gizmos.DrawWireSphere(SpawnPosition, 0.3f);
            Gizmos.DrawLine(transform.position, SpawnPosition);
        }
    }
}
