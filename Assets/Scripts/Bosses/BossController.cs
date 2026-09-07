using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El cerebro de un jefe. Sustituye a <see cref="EnemyController"/>: un jefe no patrulla ni
    /// persigue, se planta en el centro de su arena y encadena patrones.
    ///
    /// Lo único que hace este componente es <b>llevar el combate</b>; qué ataca y con qué números
    /// vive todo en el <see cref="BossDefinition"/>, así que otro jefe es otro asset y otro prefab,
    /// sin tocar este archivo.
    ///
    /// El bucle es el clásico de jefe de acción 2D:
    /// <list type="number">
    ///   <item>espera a que el jugador entre en la arena;</item>
    ///   <item>presentación: invulnerable, rugido, barra de vida y música;</item>
    ///   <item>por ataque — <b>aviso</b> (nada hace daño) → <b>ataque</b> → <b>recuperación</b>
    ///         (ventana de daño del jugador) → pausa;</item>
    ///   <item>al cruzar el umbral de vida de la siguiente fase, corta lo que esté haciendo, se
    ///         vuelve invulnerable un momento y cambia de baraja;</item>
    ///   <item>al morir, se lleva por delante a los esbirros que hubiera invocado.</item>
    /// </list>
    ///
    /// El jefe no lleva <see cref="EnemyController"/> ni barra de vida flotante: su barra es la de
    /// pantalla (<see cref="BossHealthBar"/>), que se pide sola al empezar el combate.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class BossController : MonoBehaviour
    {
        [Header("Definición")]
        [Tooltip("Asset con el nombre, las fases y las barajas de ataques de este jefe.")]
        [SerializeField] private BossDefinition definition;

        [Header("Arena")]
        [Tooltip("Media anchura de la arena respecto al jefe. Marca hasta dónde barren las ondas y " +
                 "en qué franja cae la lluvia de proyectiles.")]
        [Min(1f)]
        [SerializeField] private float arenaHalfWidth = 16f;

        [Tooltip("Altura útil sobre el suelo. De aquí caen los proyectiles de lluvia.")]
        [Min(1f)]
        [SerializeField] private float arenaHeight = 12f;

        [Tooltip("Capas contra las que impactan los ataques del jefe. El filtrado real lo hace la " +
                 "etiqueta amiga, así que ~0 (todo) es lo normal.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Capas que cuentan como suelo al medir la altura de la arena.")]
        [SerializeField] private LayerMask groundLayers = 1 << 6;

        [Tooltip("Planta al jefe sobre el suelo medido al empezar. Su origen es la base del sprite, " +
                 "así que basta con dejarlo caído a ojo en la escena y él se coloca solo — igual " +
                 "que hace GroundSnap con la tienda y la recompensa.")]
        [SerializeField] private bool snapToGround = true;

        [Header("Objetivo")]
        [Tooltip("Etiqueta del objetivo. También es a quién daña al tocarlo.")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Distancia a la que despierta y empieza el combate.")]
        [Min(1f)]
        [SerializeField] private float activationRadius = 20f;

        [Tooltip("Gira el sprite hacia el jugador.")]
        [SerializeField] private bool faceTarget = true;

        [Header("Presentación")]
        [Min(0f)]
        [SerializeField] private float introSeconds = 2.2f;

        [Tooltip("id de música del AudioManager para el combate. Vacío = no cambia la música.")]
        [SerializeField] private string musicId = "Music_Boss";

        [Tooltip("id de sonido del rugido de entrada. Vacío = sin sonido.")]
        [SerializeField] private string roarSfxId;

        [Min(0f)]
        [SerializeField] private float introShake = 0.6f;

        [Header("Daño por contacto")]
        [Tooltip("Daño al tocar al jugador. 0 = el cuerpo del jefe no hace daño.")]
        [Min(0f)]
        [SerializeField] private float contactDamage = 12f;

        [Min(0.05f)]
        [SerializeField] private float contactDamageCooldown = 0.8f;

        [Min(0f)]
        [SerializeField] private float contactKnockbackMultiplier = 1.4f;

        [Header("Efectos")]
        [Tooltip("Sprite base de los efectos del jefe (avisos, ondas, proyectiles). Vacío = el " +
                 "cuadrado generado por AbilityFx.")]
        [SerializeField] private Sprite fxSprite;

        [Tooltip("Silueta del jefe que se enciende detrás de él durante el aviso de cada ataque. Se " +
                 "construye en código; hace legible que 'algo viene' sin pelearse con el HitFlash " +
                 "del cuerpo, que también escribe el color del sprite.")]
        [SerializeField] private bool showTelegraphAura = true;

        [Tooltip("Cuánto se agranda la silueta del aura respecto al cuerpo.")]
        [Min(1f)]
        [SerializeField] private float auraScale = 1.06f;

        [Tooltip("Balanceo lento del cuerpo mientras espera, en grados. 0 = quieto.")]
        [Min(0f)]
        [SerializeField] private float swayDegrees = 1.8f;

        [Min(0.01f)]
        [SerializeField] private float swaySpeed = 1.1f;

        // ------------------------------------------------------------------ estado

        private Health _health;
        private Knockback _knockback;
        private SpriteRenderer _body;
        private Transform _visual;
        private SpriteRenderer _aura;
        private Collider2D[] _colliders;
        private Rigidbody2D _rigidbody;

        private Transform _target;
        private float _retargetTimer;
        private float _contactTimer;
        private float _groundY;
        private Quaternion _visualBaseRotation;

        private int _phaseIndex;
        private bool _fighting;
        private bool _dead;
        private Coroutine _fightRoutine;
        private Coroutine _attackRoutine;
        private float _auraTarget;

        /// <summary>Esbirros invocados por los ataques. Se limpian al morir el jefe.</summary>
        private readonly List<GameObject> _adds = new List<GameObject>();

        /// <summary>Cuántos ataques han pasado desde que se usó cada patrón (para no repetir).</summary>
        private readonly Dictionary<BossAttack, int> _lastUsedAt = new Dictionary<BossAttack, int>();

        private int _attacksLaunched;

        private const float RetargetInterval = 0.4f;

        // ------------------------------------------------------------------ API pública

        public BossDefinition Definition => definition;
        public Health BossHealth => _health;
        public int PhaseIndex => _phaseIndex;
        public BossPhase CurrentPhase => definition != null ? definition.GetPhase(_phaseIndex) : null;
        public bool IsFighting => _fighting && !_dead;
        public float GroundY => _groundY;
        public float ArenaHalfWidth => arenaHalfWidth;

        /// <summary>Se dispara al empezar el combate (tras la presentación).</summary>
        public event Action FightStarted;

        /// <summary>Nuevo índice de fase. Lo escucha la barra de vida para marcar el corte.</summary>
        public event Action<int> PhaseChanged;

        /// <summary>Se dispara al morir el jefe.</summary>
        public event Action Defeated;

        /// <summary>
        /// Apunta un esbirro invocado para que muera con el jefe. Sin esto, matar al jefe dejaría
        /// bichos sueltos dando vueltas mientras el jugador recoge la recompensa.
        /// </summary>
        public void RegisterAdd(GameObject add)
        {
            if (add == null) return;
            _adds.Add(add);
        }

        /// <summary>Esbirros vivos ahora mismo (los ataques de invocación lo usan como tope).</summary>
        public int LiveAdds
        {
            get
            {
                int alive = 0;
                for (int i = _adds.Count - 1; i >= 0; i--)
                {
                    var add = _adds[i];
                    var health = add != null ? add.GetComponent<Health>() : null;

                    if (add == null || (health != null && health.IsDead)) _adds.RemoveAt(i);
                    else alive++;
                }
                return alive;
            }
        }

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake()
        {
            _health = GetComponent<Health>();
            _knockback = GetComponent<Knockback>();
            _body = GetComponentInChildren<SpriteRenderer>();
            _colliders = GetComponentsInChildren<Collider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();

            _visual = _body != null ? _body.transform : transform;
            _visualBaseRotation = _visual.localRotation;

            // Un jefe es inamovible: ni el retroceso de los golpes ni la física lo desplazan de su
            // arena, que es lo que hacen legibles sus patrones.
            if (_knockback != null) _knockback.Immune = true;
            if (_rigidbody != null)
            {
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody.useFullKinematicContacts = true;
                _rigidbody.freezeRotation = true;
            }

            if (showTelegraphAura) BuildAura();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
            _health.HealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            _health.HealthChanged -= OnHealthChanged;
        }

        private void Start()
        {
            _groundY = MeasureGroundY();

            // El origen del prefab es la base del jefe, así que plantarlo es igualar la Y.
            if (snapToGround)
                transform.position = new Vector3(transform.position.x, _groundY, transform.position.z);

            if (definition == null || definition.PhaseCount == 0)
            {
                Debug.LogWarning($"[Boss] '{name}' no tiene BossDefinition (o no tiene fases): se queda quieto.", this);
                return;
            }

            ApplyPhaseVisuals(definition.GetPhase(0));
        }

        private void Update()
        {
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;

            UpdateAura();

            if (_dead) return;

            UpdateFacing();
            UpdateSway();

            if (!_fighting && ShouldWakeUp()) BeginFight();
        }

        // ------------------------------------------------------------------ despertar / presentación

        private bool ShouldWakeUp()
        {
            if (definition == null || definition.PhaseCount == 0) return false;
            if (!GameStateManager.CanPlayerAct) return false;

            var target = ResolveTarget();
            if (target == null) return false;

            return Vector2.Distance(target.position, transform.position) <= activationRadius;
        }

        private void BeginFight()
        {
            _fighting = true;
            _fightRoutine = StartCoroutine(FightLoop(playIntro: true));
        }

        /// <summary>
        /// Bucle del combate. Se relanza tal cual tras cada transición de fase, por eso la
        /// presentación es un parámetro: sólo la primera vuelta la reproduce.
        /// </summary>
        private IEnumerator FightLoop(bool playIntro)
        {
            if (playIntro) yield return Intro();

            while (!_dead)
            {
                var phase = CurrentPhase;
                if (phase == null || phase.attacks == null || phase.attacks.Length == 0)
                {
                    yield return null;
                    continue;
                }

                var attack = PickAttack(phase);
                if (attack == null)
                {
                    yield return null;
                    continue;
                }

                yield return RunAttack(attack, phase);

                if (_dead) yield break;

                float pause = UnityEngine.Random.Range(phase.pauseBetweenAttacks.x, phase.pauseBetweenAttacks.y);
                yield return new WaitForSeconds(pause / PaceOf(phase));
            }
        }

        private IEnumerator Intro()
        {
            // Invulnerable durante la presentación: nadie debería poder abrir el combate
            // descargándole el arma entera antes de que el jefe pueda reaccionar.
            _health.Invulnerable = true;

            BossHealthBar.Show(this);

            if (!string.IsNullOrWhiteSpace(musicId) && AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(musicId);

            if (!string.IsNullOrWhiteSpace(roarSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(roarSfxId);

            if (introShake > 0f) CameraFollow.ShakeAll(introShake, Mathf.Max(0.3f, introSeconds * 0.5f));

            _auraTarget = 0.7f;
            yield return new WaitForSeconds(introSeconds);
            _auraTarget = 0f;

            _health.Invulnerable = false;
            FightStarted?.Invoke();
        }

        // ------------------------------------------------------------------ ataques

        /// <summary>
        /// Sorteo por peso dentro de la baraja de la fase, descartando los que todavía están en
        /// enfriamiento (<see cref="BossAttack.CooldownInAttacks"/>). Si el enfriamiento dejara la
        /// baraja vacía se ignora, para que el jefe nunca se quede parado por una mala
        /// configuración del asset.
        /// </summary>
        private BossAttack PickAttack(BossPhase phase)
        {
            BossAttack fallback = null;
            float total = 0f;

            for (int i = 0; i < phase.attacks.Length; i++)
            {
                var candidate = phase.attacks[i];
                if (candidate == null) continue;

                fallback ??= candidate;
                if (!IsReady(candidate)) continue;

                total += Mathf.Max(0.01f, candidate.Weight);
            }

            if (total <= 0f) return fallback;

            float roll = UnityEngine.Random.value * total;

            for (int i = 0; i < phase.attacks.Length; i++)
            {
                var candidate = phase.attacks[i];
                if (candidate == null || !IsReady(candidate)) continue;

                roll -= Mathf.Max(0.01f, candidate.Weight);
                if (roll <= 0f) return candidate;
            }

            return fallback;
        }

        private bool IsReady(BossAttack attack) =>
            !_lastUsedAt.TryGetValue(attack, out int last) ||
            _attacksLaunched - last > attack.CooldownInAttacks;

        /// <summary>Aviso → ataque → recuperación. Es el ciclo que hace legible a un jefe.</summary>
        private IEnumerator RunAttack(BossAttack attack, BossPhase phase)
        {
            _lastUsedAt[attack] = _attacksLaunched;
            _attacksLaunched++;

            var ctx = BuildContext(phase);
            float pace = PaceOf(phase);

            // --- aviso: el aura se enciende y el ataque pinta sus propias marcas. Nada daña aún.
            _auraTarget = 0.6f;
            attack.OnTelegraph(ctx);
            yield return new WaitForSeconds(attack.Telegraph / pace);
            _auraTarget = 0f;

            if (_dead) yield break;

            // --- ataque: se guarda la corrutina para poder cortarla en seco al cambiar de fase.
            _attackRoutine = StartCoroutine(attack.Run(BuildContext(phase)));
            yield return _attackRoutine;
            _attackRoutine = null;

            if (_dead) yield break;

            // --- recuperación: el jefe se queda quieto. Ésta es la ventana de daño del jugador.
            yield return new WaitForSeconds(attack.Recovery / pace);
        }

        private BossContext BuildContext(BossPhase phase)
        {
            var ability = new AbilityContext(
                caster: gameObject,
                runner: this,
                casterHealth: _health,
                hitLayers: hitLayers,
                facing: FacingSign(),
                aim: AimAtTarget(),
                friendlyTag: gameObject.CompareTag("Untagged") ? null : tag,
                damageScale: phase != null ? phase.damageScale : 1f);

            return new BossContext(this, ResolveTarget(), ability, _groundY, arenaHalfWidth,
                                   arenaHeight, PaceOf(phase), fxSprite,
                                   phase != null ? phase.accent : Color.white);
        }

        /// <summary>Ritmo efectivo de la fase, incluido el frenesí de vida baja.</summary>
        private float PaceOf(BossPhase phase)
        {
            if (phase == null) return 1f;

            float pace = phase.speedScale;
            if (phase.frenzyBelowHealth > 0f && _health.Normalized <= phase.frenzyBelowHealth)
                pace *= phase.frenzySpeedScale;

            return Mathf.Max(0.1f, pace);
        }

        // ------------------------------------------------------------------ fases

        private void OnHealthChanged(float current, float max)
        {
            if (_dead || definition == null || definition.PhaseCount == 0) return;

            int next = definition.PhaseIndexFor(max <= 0f ? 0f : current / max);

            // Las fases nunca van hacia atrás: curar al jefe (o el robo de vida de un arma) no
            // debería devolverlo a una fase anterior a mitad de combate.
            if (next <= _phaseIndex) return;

            _phaseIndex = next;
            StartCoroutine(EnterPhase(definition.GetPhase(next)));
        }

        /// <summary>
        /// Transición de fase: corta el ataque en curso, se vuelve invulnerable un momento y
        /// cambia de baraja. La invulnerabilidad evita el caso feo de saltarse la fase 2 entera
        /// con una ráfaga bien puesta justo en el umbral.
        /// </summary>
        private IEnumerator EnterPhase(BossPhase phase)
        {
            if (phase == null) yield break;

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_fightRoutine != null)
            {
                StopCoroutine(_fightRoutine);
                _fightRoutine = null;
            }

            _health.Invulnerable = true;
            ApplyPhaseVisuals(phase);
            PhaseChanged?.Invoke(_phaseIndex);

            if (!string.IsNullOrWhiteSpace(phase.transitionSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(phase.transitionSfxId);

            if (phase.transitionShake > 0f)
                CameraFollow.ShakeAll(phase.transitionShake, Mathf.Max(0.3f, phase.transitionSeconds * 0.6f));

            _auraTarget = 0.85f;
            yield return new WaitForSeconds(phase.transitionSeconds);
            _auraTarget = 0f;

            if (_dead) yield break;

            _health.Invulnerable = false;
            _fightRoutine = StartCoroutine(FightLoop(playIntro: false));
        }

        private void ApplyPhaseVisuals(BossPhase phase)
        {
            if (phase == null || _aura == null) return;

            var color = phase.accent;
            color.a = _aura.color.a;
            _aura.color = color;
        }

        // ------------------------------------------------------------------ muerte

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            _fighting = false;

            StopAllCoroutines();
            _attackRoutine = null;
            _fightRoutine = null;

            // Los esbirros mueren con su invocador: si no, el jugador se queda peleando contra los
            // restos mientras recoge la recompensa del jefe.
            KillAdds();

            if (_aura != null) _aura.gameObject.SetActive(false);

            CameraFollow.ShakeAll(0.9f, 0.8f);
            AbilityFx.Flash(fxSprite, transform.position + Vector3.up, Vector3.one * 5f,
                            new Color(1f, 0.95f, 0.7f, 0.85f), 0.6f, 0f, 2.4f, gameObject);

            // El aspecto del cadáver y su limpieza son cosa de Corpse; aquí sólo se apaga la
            // física, igual que hace EnemyController.
            foreach (var col in _colliders)
                if (col != null) col.enabled = false;

            if (_rigidbody != null) _rigidbody.simulated = false;

            BossHealthBar.Hide();
            Defeated?.Invoke();
        }

        private void KillAdds()
        {
            for (int i = 0; i < _adds.Count; i++)
            {
                var add = _adds[i];
                if (add == null) continue;

                var health = add.GetComponent<Health>();
                if (health != null && !health.IsDead) health.Die();
                else Destroy(add);
            }

            _adds.Clear();
        }

        // ------------------------------------------------------------------ contacto

        private void OnCollisionStay2D(Collision2D collision) => TryContactDamage(collision.collider);

        private void OnTriggerStay2D(Collider2D other) => TryContactDamage(other);

        private void TryContactDamage(Collider2D other)
        {
            if (_dead || contactDamage <= 0f || _contactTimer > 0f || other == null) return;
            if (!GameStateManager.CanPlayerAct) return;

            var otherHealth = other.GetComponentInParent<Health>();
            if (otherHealth == null || otherHealth == _health) return;
            if (!string.IsNullOrEmpty(targetTag) && !otherHealth.CompareTag(targetTag)) return;

            if (otherHealth.TakeDamage(contactDamage, transform.position, contactKnockbackMultiplier))
                _contactTimer = contactDamageCooldown;
        }

        // ------------------------------------------------------------------ objetivo y presencia

        private Transform ResolveTarget()
        {
            if (_target != null) return _target;

            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer > 0f) return null;

            _retargetTimer = RetargetInterval;

            if (string.IsNullOrEmpty(targetTag)) return null;

            var found = GameObject.FindGameObjectWithTag(targetTag);
            _target = found != null ? found.transform : null;
            return _target;
        }

        private int FacingSign()
        {
            var target = _target;
            if (target == null) return 1;
            return target.position.x < transform.position.x ? -1 : 1;
        }

        private Vector2 AimAtTarget()
        {
            var target = _target;
            if (target == null) return new Vector2(FacingSign(), 0f);

            Vector2 delta = (Vector2)target.position - (Vector2)transform.position;
            return delta.sqrMagnitude < 0.0001f ? new Vector2(FacingSign(), 0f) : delta.normalized;
        }

        private void UpdateFacing()
        {
            if (!faceTarget || _body == null) return;

            var target = ResolveTarget();
            if (target == null) return;

            _body.flipX = target.position.x < transform.position.x;
        }

        /// <summary>Balanceo lento del cuerpo: un jefe totalmente inmóvil parece un decorado.</summary>
        private void UpdateSway()
        {
            // Sólo si hay un hijo visual: girar la raíz movería también los colliders del jefe.
            if (swayDegrees <= 0f || _visual == null || _visual == transform) return;

            float angle = Mathf.Sin(Time.time * swaySpeed) * swayDegrees;
            _visual.localRotation = _visualBaseRotation * Quaternion.Euler(0f, 0f, angle);
        }

        // ------------------------------------------------------------------ aura de aviso

        /// <summary>
        /// El aura es una <b>copia del propio sprite del jefe</b> un pelín más grande y detrás de
        /// él, no un rectángulo de color: así se lee como un halo que rodea su silueta y no como
        /// una caja translúcida pegada encima. Cuelga del transform del cuerpo, de modo que hereda
        /// gratis su posición, su escala y el balanceo.
        /// </summary>
        private void BuildAura()
        {
            var parent = _body != null ? _body.transform : transform;

            var go = new GameObject("Boss Aura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * auraScale;

            _aura = go.AddComponent<SpriteRenderer>();
            _aura.color = new Color(1f, 1f, 1f, 0f);

            if (_body != null)
            {
                _aura.sprite = _body.sprite;
                _aura.sortingLayerID = _body.sortingLayerID;
                _aura.sortingOrder = _body.sortingOrder - 1;   // detrás del cuerpo
            }
            else
            {
                // Sin sprite de cuerpo no hay silueta que copiar: se cae al cuadrado genérico.
                _aura.sprite = fxSprite != null ? fxSprite : AbilityFx.DefaultSprite;
                _aura.sortingOrder = 49;
                AbilityFx.Resize(go.transform, _aura, Vector2.one * 4f);
            }
        }

        private void UpdateAura()
        {
            if (_aura == null) return;

            // El halo sigue al cuerpo si éste cambia de fotograma o se voltea.
            if (_body != null)
            {
                if (_aura.sprite != _body.sprite) _aura.sprite = _body.sprite;
                _aura.flipX = _body.flipX;
            }

            var color = _aura.color;
            // Durante el aviso late en vez de quedarse fijo: un aura constante se lee como decorado.
            float pulse = _auraTarget > 0.01f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 14f) : 0f;
            float target = _auraTarget * pulse;

            color.a = Mathf.MoveTowards(color.a, target, Time.deltaTime * 4f);
            _aura.color = color;
        }

        // ------------------------------------------------------------------ arena

        /// <summary>
        /// Mide el suelo bajo el jefe una sola vez, al empezar. Los ataques rasantes y la lluvia
        /// necesitan una altura de referencia, y sacarla de la escena evita tener que ajustarla a
        /// mano en cada arena.
        /// </summary>
        private float MeasureGroundY()
        {
            // El rayo sale muy por encima del jefe a propósito: si sale desde su base y lo han
            // dejado un pelo hundido en el terreno, empezaría dentro del collider y no habría
            // suelo que medir.
            var origin = (Vector2)transform.position + Vector2.up * (arenaHeight * 0.5f);
            var hit = Physics2D.Raycast(origin, Vector2.down, arenaHeight * 0.5f + arenaHeight + 10f, groundLayers);

            if (hit.collider != null) return hit.point.y;

            // Sin suelo debajo (arena flotante o capa mal puesta): la base del sprite es la mejor
            // aproximación disponible, y es mejor que dejar los ataques a la altura del centro.
            return _body != null ? _body.bounds.min.y : transform.position.y;
        }

        private void OnDrawGizmosSelected()
        {
            float groundY = Application.isPlaying ? _groundY : transform.position.y;

            // Arena: hasta dónde barren los ataques y de dónde cae la lluvia.
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(transform.position.x, groundY + arenaHeight * 0.5f, 0f),
                                new Vector3(arenaHalfWidth * 2f, arenaHeight, 0f));

            // Línea del suelo medido.
            Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawLine(new Vector3(transform.position.x - arenaHalfWidth, groundY, 0f),
                            new Vector3(transform.position.x + arenaHalfWidth, groundY, 0f));

            // Radio de despertar.
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
