using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Bosses;
using RedMagic.Combat;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Resuelve el golpe del enemigo <b>en el instante que le avisa la animación</b>, no cuando le
    /// toca a un temporizador. Sirve para los dos tipos: si el arquetipo es a distancia lanza el
    /// proyectil, y si es de melé aplica una caja de daño por delante.
    ///
    /// No decide <i>cuándo</i> se ataca — eso es del cerebro — ni <i>en qué frame</i> — eso es del
    /// clip. Sólo ejecuta. Por eso un enemigo nuevo no necesita un script de ataque propio:
    /// cambian los números de <see cref="EnemyStats"/>, no el código.
    ///
    /// El daño entra por <see cref="AbilityHit"/> y el proyectil por
    /// <see cref="ProjectileFactory"/>, que son los mismos caminos que usan jefes y armas: filtrado
    /// de objetivos, robo de vida y pooling ya resueltos en un solo sitio.
    ///
    /// <b>Varios ataques.</b> Además del ataque propio (melé / disparo / explosión) un enemigo
    /// puede llevar una <b>baraja</b> de ataques del catálogo (<see cref="EnemyTuning.attacks"/>, los
    /// mismos assets que los jefes). El cerebro sigue decidiendo <i>cuándo</i>; aquí se sortea
    /// <i>cuál</i> (<see cref="Choose"/>) y, si sale una carta, la lanza el
    /// <see cref="EnemyAttackHost"/>. Sin baraja nada cambia: sale siempre el propio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyAttack : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnAttack en el frame en que sale el golpe/disparo. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into) { into.Add(SoundTrigger.OnAttack); }

        /// <summary>
        /// Nombre del hijo opcional que marca dónde nace el disparo — la boca de un dragón, la
        /// punta de un bastón. Colócalo en el Prefab Editor arrastrándolo a la posición exacta con
        /// el enemigo mirando a la DERECHA (facing=1): al mirar a la izquierda su X se refleja
        /// solo, igual que ya hacía <see cref="EnemyTuning.projectile"/>'s <c>muzzleOffset</c> — no
        /// hace falta un segundo valor para el otro lado. <see cref="EnemyFactory"/> lo crea solo
        /// (semillado en la posición de <c>muzzleOffset</c>) la primera vez que genera un enemigo a
        /// distancia; a partir de ahí es un GameObject normal del prefab, se mueve a mano y no se
        /// vuelve a tocar. Sin este hijo, <see cref="Shoot"/> sigue usando el número de
        /// <c>muzzleOffset</c> tal cual — ningún enemigo existente cambia de comportamiento.
        /// </summary>
        public const string MuzzleChildName = "Muzzle";

        private EnemyStats _stats;
        private EnemyAnimation _animation;
        private Health _health;
        private Collider2D _collider;
        private Transform _muzzle;
        private EnemyAttackHost _host;

        /// <summary>La carta que salió en el último <see cref="Choose"/>; null = el ataque propio.</summary>
        private EnemyDeckEntry _chosen;

        /// <summary>El gesto con el que sale el ataque propio esta vez (variante de la baraja), o vacío.</summary>
        public string BuiltInGesture { get; private set; }

        /// <summary>Ataques lanzados (propios y de la baraja), para 'cooldownInAttacks'.</summary>
        private int _launched;

        private readonly Dictionary<BossAttack, int> _lastUsedAt = new Dictionary<BossAttack, int>();
        private readonly Dictionary<BossAttack, float> _lastUsedTime = new Dictionary<BossAttack, float>();

        /// <summary>Un ataque de la baraja ha terminado (aviso + golpe + recuperación). Lo escucha el cerebro.</summary>
        public event System.Action DeckAttackFinished;

        /// <summary>
        /// <b>Punto</b> al que va el golpe, no dirección. Lo fija el cerebro al empezar el ataque y
        /// la dirección se calcula al soltar, desde la boca del disparo.
        ///
        /// Guardar el punto y no el vector es lo que impide el fallo clásico: la boca está a la
        /// altura de la mano (medio cuerpo por encima del origen), así que un vector calculado de
        /// pivote a pivote sale plano desde ahí arriba y pasa por encima de la cabeza del objetivo.
        /// </summary>
        private Vector2 _aimPoint;

        private bool _hasAim;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _animation = GetComponent<EnemyAnimation>();
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider2D>();
            _muzzle = FindMuzzle(transform);

            // Con baraja hace falta quien lance sus ataques; sin el componente en el prefab se
            // añade aquí, con los avisos por defecto en código.
            _host = GetComponent<EnemyAttackHost>();
            if (_host == null && _stats.Tuning.HasDeck) _host = gameObject.AddComponent<EnemyAttackHost>();
        }

        /// <summary>
        /// El "Muzzle" directo de la raíz o, si no hay, uno anidado — el de un cuerpo por partes
        /// cuelga del brazo-arma y se mueve con él (ver <c>ProceduralRig</c>).
        /// </summary>
        private static Transform FindMuzzle(Transform root)
        {
            var direct = root.Find(MuzzleChildName);
            if (direct != null) return direct;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == MuzzleChildName) return t;

            return null;
        }

        private void OnEnable()
        {
            _chosen = null;
            _launched = 0;
            _lastUsedAt.Clear();
            _lastUsedTime.Clear();

            if (_animation != null) _animation.AttackReleased += Execute;
        }

        private void OnDisable()
        {
            if (_animation != null) _animation.AttackReleased -= Execute;
        }

        /// <summary>
        /// Apunta el próximo golpe a un punto del mundo. El cerebro la llama al entrar en estado de
        /// ataque, con el centro del objetivo, y el punto se congela ahí: el golpe no persigue al
        /// jugador a mitad del gesto, que es lo que hace que se pueda esquivar.
        /// </summary>
        public void AimAt(Vector2 worldPoint)
        {
            _aimPoint = worldPoint;
            _hasAim = true;
        }

        /// <summary>El punto congelado del próximo golpe, o null si aún no hay. Lo lee el rig para apuntar el brazo.</summary>
        public Vector2? AimPoint => _hasAim ? _aimPoint : null;

        // ============================================================ baraja

        /// <summary>
        /// ¿Hay algún ataque que lanzar ahora? Sortea por peso entre el ataque propio (si el
        /// objetivo está a <see cref="EnemyTuning.attackRange"/> y a su altura) y las cartas de la
        /// baraja que están a su distancia y fuera de enfriamiento, y se queda con el que sale
        /// hasta <see cref="BeginChosen"/>. False = ahora no hay nada que lanzar.
        /// </summary>
        /// <param name="distance">Distancia al objetivo, medida como la mide el cerebro.</param>
        /// <param name="heightDelta">Altura del objetivo menos la del enemigo.</param>
        /// <param name="reachable">El objetivo está al alcance vertical del ataque propio.</param>
        public bool Choose(float distance, float heightDelta, bool reachable)
        {
            var tuning = _stats.Tuning;
            _chosen = null;

            bool builtIn = reachable && distance <= tuning.attackRange;
            if (!tuning.HasDeck || _host == null) return builtIn;

            float builtInWeight = builtIn && tuning.builtInAttack ? Mathf.Max(0.01f, tuning.builtInWeight) : 0f;
            float total = builtInWeight;

            var deck = tuning.attacks;
            for (int i = 0; i < deck.Count; i++)
                if (IsReady(deck[i], distance, heightDelta, tuning)) total += WeightOf(deck[i]);

            if (total <= 0f) return false;

            float roll = Random.value * total - builtInWeight;
            if (builtInWeight > 0f && roll <= 0f) return true;

            for (int i = 0; i < deck.Count; i++)
            {
                if (!IsReady(deck[i], distance, heightDelta, tuning)) continue;

                _chosen = deck[i];
                roll -= WeightOf(deck[i]);
                if (roll <= 0f) break;
            }

            return _chosen != null || builtInWeight > 0f;
        }

        private static float WeightOf(EnemyDeckEntry entry) =>
            Mathf.Max(0.01f, entry.builtIn ? entry.weight : entry.attack.Weight);

        /// <summary>Mismas reglas que un jefe: a su distancia, y cumplidos 'cooldownInAttacks' y 'cooldownSeconds'.</summary>
        private bool IsReady(EnemyDeckEntry entry, float distance, float heightDelta, EnemyTuning tuning)
        {
            if (entry == null || !entry.InReach(distance, heightDelta, tuning.attackRange)) return false;
            if (entry.builtIn) return true;   // el ataque propio no tiene enfriamientos propios: manda attackCooldown

            var attack = entry.attack;
            if (_lastUsedAt.TryGetValue(attack, out int last) && _launched - last <= attack.CooldownInAttacks) return false;

            return attack.CooldownSeconds <= 0f || !_lastUsedTime.TryGetValue(attack, out float at) ||
                   Time.time - at >= attack.CooldownSeconds;
        }

        /// <summary>
        /// Lanza lo que salió en <see cref="Choose"/>. True si era una carta de la baraja: el ataque
        /// corre por su cuenta y avisa con <see cref="DeckAttackFinished"/>. False = toca el ataque
        /// propio, que el cerebro lanza por la animación como siempre.
        /// </summary>
        public bool BeginChosen(Transform target)
        {
            var entry = _chosen;
            _chosen = null;
            BuiltInGesture = entry != null && entry.builtIn ? entry.gesture : null;

            // Una variante del ataque propio (otro gesto) sale por la animación, como el propio.
            if (entry == null || entry.builtIn || entry.attack == null || _host == null)
            {
                _launched++;
                return false;
            }

            _lastUsedAt[entry.attack] = _launched;
            _lastUsedTime[entry.attack] = Time.time;
            _launched++;

            _host.Run(entry.attack, target, _hasAim ? _aimPoint : (Vector2)transform.position,
                      () => DeckAttackFinished?.Invoke());
            return true;
        }

        /// <summary>Hacia dónde mira el golpe, desde donde nace de verdad.</summary>
        private Vector2 Direction(Vector2 origin)
        {
            if (!_hasAim) return new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0f);

            Vector2 direction = _aimPoint - origin;
            if (direction.sqrMagnitude < 0.0001f) return Vector2.right;

            // Sin apuntado libre el tiro sale plano, pero al menos hacia el lado correcto.
            if (!_stats.Tuning.aimAtTarget) return new Vector2(direction.x >= 0f ? 1f : -1f, 0f);

            return direction.normalized;
        }

        /// <summary>
        /// El golpe. Público para poder dispararlo desde fuera (una prueba, un jefe que reutiliza
        /// al enemigo), aunque lo normal es que lo llame el evento de la animación.
        /// </summary>
        public void Execute()
        {
            var tuning = _stats.Tuning;
            if (_health != null && _health.IsDead) return;

            // Un ataque de la baraja en marcha no es el propio: sus gestos no lo disparan.
            if (_host != null && _host.Running) return;

            SoundTriggered?.Invoke(SoundTrigger.OnAttack);

            if (tuning.selfDestruct) Explode(tuning);
            else if (tuning.IsRanged) Shoot(tuning);
            else Strike(tuning);
        }

        // ============================================================ kamikaze

        /// <summary>
        /// Daño en círculo alrededor del cuerpo y muerte. La explosión que se ve es el clip de
        /// muerte, que arranca con <see cref="Health.Die"/>: por eso aquí no se instancia ningún FX.
        /// </summary>
        private void Explode(EnemyTuning tuning)
        {
            Vector2 center = _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;
            var context = Context(tuning, new Vector2(Facing(), 0f), Facing());

            AbilityHit.DamageCircle(context, center, tuning.explosionRadius,
                                    tuning.attackDamage, tuning.attackKnockbackMultiplier);

            if (tuning.explosionShake > 0f) CameraFollow.ShakeAll(tuning.explosionShake, 0.25f);

            if (_health != null) _health.Die();
        }

        // ============================================================ a distancia

        private void Shoot(EnemyTuning tuning)
        {
            // El facing se decide con el punto, no con la dirección, porque la dirección todavía
            // no existe: depende de dónde caiga la boca, que a su vez depende del facing.
            int facing = Facing();
            Vector2 origin = MuzzleOrigin(tuning, facing);

            Vector2 direction = Direction(origin);
            var context = Context(tuning, direction, facing);

            ProjectileFactory.Spawn(context, tuning.projectile, origin, direction,
                                    tuning.attackDamage, tuning.attackKnockbackMultiplier,
                                    tuning.projectileSprite, tuning.projectileTint);
        }

        /// <summary>
        /// Dónde nace el disparo. Con un hijo "Muzzle" colocado a mano, su posición local manda —
        /// reflejada en X por el facing, igual que el número de <c>muzzleOffset</c> — porque
        /// apunta al sitio real (la boca del dragón), sea cual sea la proporción del personaje. Sin
        /// ese hijo cae al número tal cual, sin cambiar nada para los enemigos existentes.
        /// </summary>
        private Vector2 MuzzleOrigin(EnemyTuning tuning, int facing)
        {
            // Anidado (brazo de un rig): su posición en el mundo ya es la buena — el brazo apunta
            // y la raíz visual está volteada —, así que no se refleja nada a mano.
            if (_muzzle != null && _muzzle.parent != transform) return _muzzle.position;

            Vector2 offset = _muzzle != null
                ? (Vector2)_muzzle.localPosition
                : tuning.projectile.muzzleOffset;

            return (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);
        }

        // ============================================================ melé

        private void Strike(EnemyTuning tuning)
        {
            // La caja sale hacia donde mira, no desde donde está: un enemigo pegado al jugador debe
            // golpear en el sentido del gesto, igual que hace el melé del jugador.
            int facing = Facing();
            Vector2 center = (Vector2)transform.position +
                             new Vector2(tuning.meleeHitboxOffset.x * facing, tuning.meleeHitboxOffset.y);

            var context = Context(tuning, new Vector2(facing, 0f), facing);

            AbilityHit.DamageBox(context, center, tuning.meleeHitboxSize, 0f,
                                 tuning.attackDamage, tuning.attackKnockbackMultiplier, center);
        }

        /// <summary>Lado hacia el que va el golpe, según el punto apuntado.</summary>
        private int Facing()
        {
            if (!_hasAim) return 1;
            return _aimPoint.x >= transform.position.x ? 1 : -1;
        }

        // ============================================================ contexto

        /// <summary>
        /// El paquete que <see cref="AbilityHit"/> y <see cref="ProjectileFactory"/> esperan. La
        /// etiqueta "amiga" es la del propio enemigo: es lo que impide que sus balas maten a los
        /// suyos sin montar capas ni bandos.
        /// </summary>
        private AbilityContext Context(EnemyTuning tuning, Vector2 aim, int facing)
        {
            string friendly = CompareTag("Untagged") ? null : tag;

            return new AbilityContext(gameObject, this, _health, tuning.hitLayers, facing, aim, friendly);
        }

        private void OnDrawGizmosSelected()
        {
            var stats = GetComponent<EnemyStats>();
            if (stats == null || stats.Tuning.selfDestruct) return;

            var tuning = stats.Tuning;
            if (tuning.IsRanged)
            {
                var muzzle = FindMuzzle(transform);
                Vector3 at = muzzle != null && muzzle.parent != transform
                    ? muzzle.position
                    : transform.position + (Vector3)(muzzle != null ? (Vector2)muzzle.localPosition
                                                                    : tuning.projectile.muzzleOffset);
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Gizmos.DrawSphere(at, 0.06f);
                return;
            }

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position + (Vector3)tuning.meleeHitboxOffset,
                                tuning.meleeHitboxSize);
        }
    }
}
