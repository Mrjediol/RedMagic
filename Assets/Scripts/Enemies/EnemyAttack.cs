using RedMagic.Abilities;
using RedMagic.Audio;
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
            _muzzle = transform.Find(MuzzleChildName);
        }

        private void OnEnable()
        {
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
                var muzzle = transform.Find(MuzzleChildName);
                Vector2 offset = muzzle != null ? (Vector2)muzzle.localPosition : tuning.projectile.muzzleOffset;
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Gizmos.DrawSphere(transform.position + (Vector3)offset, 0.06f);
                return;
            }

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position + (Vector3)tuning.meleeHitboxOffset,
                                tuning.meleeHitboxSize);
        }
    }
}
