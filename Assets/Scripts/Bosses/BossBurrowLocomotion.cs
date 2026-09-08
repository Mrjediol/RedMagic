using System.Collections;
using RedMagic.Abilities;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Locomoción de un jefe <b>excavador</b> — la Reina Escarabajo. El resto de jefes se plantan
    /// en su arena; un bicho no puede: quieto no se lee como vivo.
    ///
    /// El ciclo, encima del bucle normal de <see cref="BossController"/>:
    /// <list type="number">
    ///   <item><b>en superficie</b>: patrulla en horizontal por la arena (rebota en los bordes,
    ///         sesga hacia el jugador) mientras el <see cref="BossController"/> encadena su
    ///         baraja con normalidad;</item>
    ///   <item><b>excava</b>: mete la cabeza en la superficie más cercana (suelo o techo), se
    ///         vuelve invisible e invulnerable y llama a <see cref="BossController.SuspendAttacks"/>;</item>
    ///   <item><b>en tránsito</b> (bajo tierra, sin verla): túnela hacia el punto de salida y cada
    ///         <see cref="tremorInterval"/> lanza el <see cref="tremorAttack"/> — un temblor a ras
    ///         de suelo que obliga a saltar: el "ya viene" de una pelea que ahora no puedes mirar;</item>
    ///   <item><b>emerge</b>: sale por la superficie <b>opuesta</b>, desplazada 1–5 m a un lado,
    ///         tras un aviso de polvo para que el jugador despeje el sitio, y llama a
    ///         <see cref="BossController.ResumeAttacks"/>.</item>
    /// </list>
    ///
    /// El sprite del escarabajo mira "hacia arriba" (es un bicho cenital): este componente lo gira
    /// para que la cabeza apunte a donde se mueve, lo cuelga bocabajo en el techo, y lo pica de
    /// cabeza al excavar y de cabeza al salir.
    /// </summary>
    [RequireComponent(typeof(BossController))]
    [DisallowMultipleComponent]
    public class BossBurrowLocomotion : MonoBehaviour
    {
        [Header("En superficie")]
        [Tooltip("Velocidad de patrulla horizontal, unidades/segundo.")]
        [Min(0f)]
        [SerializeField] private float surfaceSpeed = 4.5f;

        [Tooltip("Margen que deja con los bordes de la arena al patrullar y al emerger.")]
        [Min(0f)]
        [SerializeField] private float patrolMargin = 2f;

        [Tooltip("Segundos (mín/máx) en superficie antes de volver a excavar.")]
        [SerializeField] private Vector2 surfacedSeconds = new Vector2(6f, 9f);

        [Header("Excavar / emerger")]
        [Tooltip("Segundos que tarda en hundirse del todo en la superficie.")]
        [Min(0.05f)]
        [SerializeField] private float digSeconds = 0.5f;

        [Tooltip("Segundos que tarda en salir del todo por la superficie opuesta.")]
        [Min(0.05f)]
        [SerializeField] private float emergeSeconds = 0.5f;

        [Tooltip("Aviso de polvo en el punto de salida antes de asomar. Da tiempo a despejar el sitio.")]
        [Min(0f)]
        [SerializeField] private float emergeWarnSeconds = 0.9f;

        [Tooltip("Desplazamiento horizontal (mín/máx) del punto de salida respecto al de entrada.")]
        [SerializeField] private Vector2 emergeOffsetRange = new Vector2(1f, 5f);

        [Header("En tránsito (bajo tierra)")]
        [Tooltip("Segundos (mín/máx) túnelando entre una superficie y la otra.")]
        [SerializeField] private Vector2 transitSeconds = new Vector2(2.5f, 3.5f);

        [Tooltip("Segundos entre temblores mientras túnela.")]
        [Min(0.2f)]
        [SerializeField] private float tremorInterval = 1.1f;

        [Tooltip("El ataque que se lanza como temblor mientras está bajo tierra. Un ShockwaveAttack " +
                 "de franja baja (obliga a saltar). Si está vacío, no hay temblor.")]
        [SerializeField] private BossAttack tremorAttack;

        [Header("Superficies")]
        [Tooltip("Capas que cuentan como suelo/techo para excavar (normalmente sólo Ground).")]
        [SerializeField] private LayerMask surfaceLayers = 1 << 6;

        [Tooltip("Hasta qué altura busca un techo por encima del jefe.")]
        [Min(1f)]
        [SerializeField] private float ceilingProbeHeight = 20f;

        [Header("Orientación del sprite")]
        [Tooltip("Giro en Z que deja la cabeza del sprite apuntando a la derecha. El escarabajo de " +
                 "Brackeys mira hacia arriba, así que -90.")]
        [SerializeField] private float spriteUprightDegrees = -90f;

        [Header("Polvo")]
        [SerializeField] private Sprite dustSprite;
        [SerializeField] private Color dustColor = new Color(0.52f, 0.4f, 0.28f, 0.9f);
        [Min(0.1f)]
        [SerializeField] private float dustSize = 3f;

        // ------------------------------------------------------------------ estado

        private BossController _boss;
        private Health _health;
        private SpriteRenderer _renderer;
        private Transform _visual;
        private Collider2D[] _colliders;

        private float _arenaCenterX;
        private float _halfWidth;
        private float _bodyHeight;
        private float _groundY;
        private float _ceilingY;

        private bool _hasCeiling;
        private bool _onCeiling;
        private bool _burrowed;
        private int _dir = 1;
        private Coroutine _cycle;

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake()
        {
            _boss = GetComponent<BossController>();
            _health = GetComponent<Health>();
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _visual = _renderer != null ? _renderer.transform : transform;
            _colliders = GetComponentsInChildren<Collider2D>();
        }

        private void OnEnable()
        {
            _boss.FightStarted += OnFightStarted;
            _boss.Defeated += OnDefeated;
        }

        private void OnDisable()
        {
            _boss.FightStarted -= OnFightStarted;
            _boss.Defeated -= OnDefeated;
        }

        private void Start()
        {
            _arenaCenterX = transform.position.x;
            _halfWidth = _boss.ArenaHalfWidth;
            // El sprite viene ya tumbado -90° desde el prefab, así que bounds.size.y es su alto en
            // orientación de marcha. El origen del jefe es el centro del sprite (ver el pack).
            _bodyHeight = _renderer != null ? Mathf.Max(1f, _renderer.bounds.size.y) : 2f;

            _groundY = Probe(Vector2.down, ceilingProbeHeight * 2f, transform.position.y + 1f,
                             fallback: transform.position.y, out _);

            // Sólo hay "techo" si de verdad hay un collider de suelo encima y a una altura de
            // arena razonable. Las arenas de prueba no tienen techo: entonces siempre excava por
            // el suelo (baja, túnela, vuelve a salir del suelo desplazada). El caso techo se
            // activa solo cuando alguien monta una arena cerrada.
            _ceilingY = Probe(Vector2.up, ceilingProbeHeight, transform.position.y + _bodyHeight * 0.5f,
                              fallback: 0f, out bool ceilingHit);

            float ceilingGap = _ceilingY - _groundY;
            _hasCeiling = ceilingHit && ceilingGap >= _bodyHeight * 2f && ceilingGap <= _boss.ArenaHeight * 1.2f;

            _onCeiling = false;
            transform.position = new Vector3(transform.position.x, SurfaceY(), transform.position.z);
            FaceTravel();
        }

        private void Update()
        {
            if (_burrowed || !_boss.IsFighting) return;

            float min = _arenaCenterX - _halfWidth + patrolMargin;
            float max = _arenaCenterX + _halfWidth - patrolMargin;

            // Sesga la dirección hacia el jugador, pero rebota seco en los bordes.
            var player = ResolvePlayer();
            if (player != null)
            {
                float dx = player.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 1.5f) _dir = dx < 0f ? -1 : 1;
            }

            float x = transform.position.x + _dir * surfaceSpeed * Time.deltaTime;
            if (x <= min) { x = min; _dir = 1; }
            else if (x >= max) { x = max; _dir = -1; }

            transform.position = new Vector3(x, SurfaceY(), transform.position.z);
            FaceTravel();
        }

        // ------------------------------------------------------------------ eventos del jefe

        private void OnFightStarted()
        {
            if (_cycle != null) StopCoroutine(_cycle);
            _cycle = StartCoroutine(Cycle());
        }

        private void OnDefeated()
        {
            if (_cycle != null)
            {
                StopCoroutine(_cycle);
                _cycle = null;
            }

            // No puede morir enterrada (es invulnerable bajo tierra), pero si acaso: que Corpse
            // tenga un cuerpo visible del que tirar.
            _burrowed = false;
            _health.Invulnerable = false;
            _boss.SetBodyVisible(true);
            SetCollidersEnabled(true);
        }

        // ------------------------------------------------------------------ el ciclo

        private IEnumerator Cycle()
        {
            while (_boss.IsFighting)
            {
                yield return new WaitForSeconds(Random.Range(surfacedSeconds.x, surfacedSeconds.y));
                if (!_boss.IsFighting) yield break;

                yield return Burrow();
            }
        }

        private IEnumerator Burrow()
        {
            _boss.SuspendAttacks();

            // --- pica de cabeza en la superficie donde está ahora
            float entryX = transform.position.x;
            float entrySurfaceY = _onCeiling ? _ceilingY : _groundY;
            Dust(new Vector2(entryX, entrySurfaceY));

            yield return Dive(into: _onCeiling ? Vector2.up : Vector2.down);

            _burrowed = true;
            _health.Invulnerable = true;
            _boss.SetBodyVisible(false);
            SetCollidersEnabled(false);

            // --- elige la salida: superficie opuesta (o el mismo suelo si la arena no tiene techo),
            //     1–5 m a un lado
            bool exitCeiling = _hasCeiling && !_onCeiling;
            float side = Random.value < 0.5f ? -1f : 1f;
            float offset = Random.Range(emergeOffsetRange.x, emergeOffsetRange.y) * side;
            float exitX = Mathf.Clamp(entryX + offset,
                                      _arenaCenterX - _halfWidth + patrolMargin,
                                      _arenaCenterX + _halfWidth - patrolMargin);

            // --- tránsito: túnela hacia la salida soltando temblores. Escondida muy por debajo del
            //     suelo para que ningún fotograma la asome mientras cruza.
            float hiddenY = _groundY - _bodyHeight - 1f;
            float transit = Random.Range(transitSeconds.x, transitSeconds.y);
            float t = 0f;
            float nextTremor = 0.35f;

            while (t < transit)
            {
                if (!_boss.IsFighting) yield break;

                float k = transit > 0.01f ? t / transit : 1f;
                transform.position = new Vector3(Mathf.Lerp(entryX, exitX, k), hiddenY, transform.position.z);

                if (t >= nextTremor && tremorAttack != null)
                {
                    _boss.RunScriptedAttack(tremorAttack);
                    nextTremor = t + tremorInterval;
                }

                t += Time.deltaTime;
                yield return null;
            }

            // --- aviso de polvo donde va a salir
            _onCeiling = exitCeiling;
            float exitSurfaceY = exitCeiling ? _ceilingY : _groundY;
            transform.position = new Vector3(exitX, BuriedY(exitCeiling), transform.position.z);

            for (float w = 0f; w < emergeWarnSeconds; w += 0.18f)
            {
                if (!_boss.IsFighting) yield break;
                Dust(new Vector2(exitX, exitSurfaceY));
                yield return new WaitForSeconds(0.18f);
            }

            // --- emerge de cabeza
            _boss.SetBodyVisible(true);
            yield return Surface(exitCeiling);

            SetCollidersEnabled(true);
            _health.Invulnerable = false;
            _burrowed = false;
            _boss.ResumeAttacks();
        }

        // ------------------------------------------------------------------ animación de entrada/salida

        /// <summary>Baja (o sube) el cuerpo hasta esconderlo en la superficie, cabeza primero.</summary>
        private IEnumerator Dive(Vector2 into)
        {
            float startY = transform.position.y;
            float endY = into == Vector2.up ? _ceilingY + _bodyHeight * 0.5f
                                            : _groundY - _bodyHeight * 0.5f;

            // Cabeza hacia la superficie: el sprite mira "arriba" de fábrica, así que 0° = cabeza
            // arriba (excava el techo), 180° = cabeza abajo (excava el suelo).
            Quaternion from = _visual.localRotation;
            Quaternion to = Quaternion.Euler(0f, 0f, into == Vector2.up ? 0f : 180f);

            for (float e = 0f; e < digSeconds; e += Time.deltaTime)
            {
                float k = e / digSeconds;
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(startY, endY, k),
                                                 transform.position.z);
                _visual.localRotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }

            transform.position = new Vector3(transform.position.x, endY, transform.position.z);
        }

        /// <summary>Sube (o baja) el cuerpo desde escondido hasta apoyado en la superficie.</summary>
        private IEnumerator Surface(bool fromCeiling)
        {
            float startY = BuriedY(fromCeiling);
            float endY = SurfaceY();

            // Sale cabeza primero: desde el suelo la cabeza asoma hacia arriba (0°), desde el techo
            // hacia abajo (180°). Al final gira a la orientación de marcha.
            Quaternion from = Quaternion.Euler(0f, 0f, fromCeiling ? 180f : 0f);
            Quaternion to = TravelRotation();

            for (float e = 0f; e < emergeSeconds; e += Time.deltaTime)
            {
                float k = e / emergeSeconds;
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(startY, endY, k),
                                                 transform.position.z);
                _visual.localRotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }

            transform.position = new Vector3(transform.position.x, endY, transform.position.z);
            FaceTravel();
        }

        private float BuriedY(bool ceiling) =>
            ceiling ? _ceilingY + _bodyHeight * 0.5f + 0.3f : _groundY - _bodyHeight * 0.5f - 0.3f;

        // ------------------------------------------------------------------ orientación

        private float SurfaceY() =>
            _onCeiling ? _ceilingY - _bodyHeight * 0.5f : _groundY + _bodyHeight * 0.5f;

        private Quaternion TravelRotation()
        {
            // -90 deja la cabeza a la derecha; el signo la manda a la izquierda. En el techo,
            // 180 en X la cuelga bocabajo (las patas hacia el techo).
            float z = _dir >= 0 ? spriteUprightDegrees : -spriteUprightDegrees;
            float x = _onCeiling ? 180f : 0f;
            return Quaternion.Euler(x, 0f, z);
        }

        private void FaceTravel() => _visual.localRotation = TravelRotation();

        // ------------------------------------------------------------------ utilidades

        private void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = enabled;
        }

        private Transform ResolvePlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
        }

        private float Probe(Vector2 dir, float distance, float fromY, float fallback, out bool hitSurface)
        {
            var hit = Physics2D.Raycast(new Vector2(transform.position.x, fromY), dir, distance, surfaceLayers);
            hitSurface = hit.collider != null;
            return hitSurface ? hit.point.y : fallback;
        }

        private void Dust(Vector2 at)
        {
            AbilityFx.Flash(dustSprite, at, Vector2.one * dustSize, dustColor, 0.35f, 0f, 1.4f, gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = new Color(0.6f, 0.45f, 0.3f, 0.9f);
            float x0 = _arenaCenterX - _halfWidth + patrolMargin;
            float x1 = _arenaCenterX + _halfWidth - patrolMargin;
            Gizmos.DrawLine(new Vector3(x0, _groundY, 0f), new Vector3(x1, _groundY, 0f));
            if (_hasCeiling)
                Gizmos.DrawLine(new Vector3(x0, _ceilingY, 0f), new Vector3(x1, _ceilingY, 0f));
        }
    }
}
