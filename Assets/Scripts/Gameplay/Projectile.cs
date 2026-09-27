using System.Collections;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Fx;
using RedMagic.Pipeline;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Proyectil placeholder pensado para pooling: al terminar (impacto o fin de vida) se
    /// desactiva en lugar de destruirse.
    ///
    /// Avisa OnHit al impactar y OnDeath al volver al pool (<see cref="ISoundEventSource"/>); si lleva
    /// <see cref="SoundEmitter"/>, suena. El sonido no se corta al desactivar el objeto porque lo
    /// reproduce el AudioManager en sus propias voces, no un AudioSource local.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class Projectile : MonoBehaviour, ISoundEventSource
    {
        [Header("Movimiento")]
        [SerializeField] private float speed = 14f;
        [Tooltip("Segundos antes de auto-despawn si no impacta con nada.")]
        [SerializeField] private float lifetime = 3f;

        [Tooltip("Rota el sprite entero para que apunte hacia donde vuela (el arte se autoriza " +
                 "mirando +X). Apágalo para un proyectil redondo/blob (una seta, una roca, un " +
                 "orbe) cuyo arte no está pensado para rotar — en su lugar sólo se refleja en " +
                 "horizontal según la dirección, así el \"arriba\" del sprite se mantiene siempre " +
                 "arriba, igual que ya hace BulletHellAttack con sus proyectiles redondos.")]
        [SerializeField] private bool faceTravelDirection = true;

        [Header("Daño")]
        [SerializeField] private float damage = 10f;
        [Tooltip("Capas contra las que impacta.")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [Tooltip("Cuánto empuja el impacto: multiplica el retroceso configurado en el Knockback " +
                 "del objetivo. 1 = el suyo tal cual, 0 = no empuja.")]
        [Min(0f)]
        [SerializeField] private float knockbackMultiplier = 1f;

        [Header("Ciclo de vida")]
        [Tooltip("Destruir al terminar en vez de sólo desactivar. Sólo para un proyectil que de " +
                 "verdad se instancia en caliente y no pasa por ningún pool; RangedAttack y " +
                 "ProjectileFactory ya poolean por su cuenta y dejan esto apagado.")]
        [SerializeField] private bool destroyWhenDone;

        [Header("Impacto")]
        [Tooltip("Efecto que aparece al terminar el proyectil (explosión). Opcional.")]
        [SerializeField] private GameObject impactEffect;
        [Tooltip("Desplazamiento del efecto respecto al punto final del proyectil.")]
        [SerializeField] private Vector2 impactEffectOffset;

        [Header("Comportamiento avanzado (todo a 0 = proyectil recto de siempre)")]
        [Tooltip("Objetivos que atraviesa antes de desaparecer. 0 = muere en el primero.")]
        [Min(0)]
        [SerializeField] private int pierceCount;
        [Tooltip("Grados por segundo que puede girar para seguir al objetivo más cercano. 0 = no persigue.")]
        [Min(0f)]
        [SerializeField] private float homingTurnRate;
        [Tooltip("Distancia a la que busca objetivo cuando persigue.")]
        [Min(0f)]
        [SerializeField] private float homingRange = 9f;
        [Tooltip("Caída en unidades/s². 0 = trayectoria recta; >0 = tiro parabólico (granada).")]
        [Min(0f)]
        [SerializeField] private float arcGravity;

        [Header("Daño en área al terminar")]
        [Tooltip("Radio de la explosión final. 0 = sin explosión.")]
        [Min(0f)]
        [SerializeField] private float impactRadius;
        [Tooltip("Daño de esa explosión (independiente del daño de impacto directo).")]
        [Min(0f)]
        [SerializeField] private float impactDamage;

        private Rigidbody2D _body;
        public event System.Action<SoundTrigger> SoundTriggered;
        private Collider2D[] _ownColliders;
        private SpriteRenderer _renderer;

        // Orientación de este disparo (ProjectileSpec ▸ Aiming), puesta por ProjectileFactory.
        private bool _forceFace;
        private ProjectileFacingAxis _facingAxis = ProjectileFacingAxis.Right;

        /// <summary>
        /// Null si el prefab no lleva una (la mayoría no la lleva: el resto del proyecto es un
        /// sprite fijo). Cuando existe y tiene un estado "Impact", <see cref="Despawn"/> lo
        /// reproduce y retrasa la vuelta al pool su duración, en vez de desaparecer en el mismo
        /// frame del golpe — ver el comentario de <see cref="Despawn"/>.
        /// </summary>
        private SpriteStateMachine _spriteMachine;

        /// <summary>Lo pone <see cref="Abilities.ProjectileFactory"/>: proyectil construido en código, va a su <c>Pool&lt;Projectile&gt;</c>.</summary>
        internal bool PooledCode;

        /// <summary>Lo pone <see cref="Abilities.ProjectileFactory"/>: proyectil con prefab, va a <see cref="Core.PrefabPool"/>.</summary>
        internal bool PooledPrefab;

        private Vector2 _direction = Vector2.right;
        private Vector2 _velocity;
        private GameObject _owner;
        private string _friendlyTag;
        private float _lifeTimer;
        private int _pierceLeft;
        private Health _lifestealTarget;
        private float _lifesteal;
        private bool _despawning;

        /// <summary>Objetivos ya golpeados por este disparo (para que atravesar no golpee dos veces).</summary>
        private readonly System.Collections.Generic.HashSet<Health> _hitTargets =
            new System.Collections.Generic.HashSet<Health>();

        // Capa 'Ground' del proyecto: el terreno pintado. Lo único que detiene un proyectil aparte
        // de un objetivo con Health.
        private const int GroundMask = 1 << 6;

        private static readonly Collider2D[] ImpactBuffer = new Collider2D[32];

        /// <summary>Objetivos ya alcanzados por la explosión que se está resolviendo.</summary>
        private static readonly System.Collections.Generic.HashSet<Health> ExplosionTargets =
            new System.Collections.Generic.HashSet<Health>();

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _ownColliders = GetComponentsInChildren<Collider2D>();
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _spriteMachine = GetComponent<SpriteStateMachine>();

            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        private void OnEnable()
        {
            // Reset para reutilización desde el pool.
            _despawning = false;
            _lifeTimer = lifetime;
            _pierceLeft = pierceCount;
            _hitTargets.Clear();

            // La orientación por disparo (ProjectileSpec.faceDirection / facingAxis) vuelve a lo del
            // prefab: quien lo saque del pool la vuelve a poner con ConfigureFacing antes de Launch.
            _forceFace = false;
            _facingAxis = ProjectileFacingAxis.Right;

            // Sin rotación por dirección, el transform no debe conservar ninguna rotación de una
            // vida anterior del pool — sólo el flipX del renderer orienta.
            if (!faceTravelDirection) transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Ajusta daño, velocidad y capas antes de lanzar. Lo usa <c>RangedAttack</c> para que un
        /// mismo prefab de proyectil sirva a distintos atacantes (jugador o enemigo).
        /// Cualquier valor negativo deja el del prefab.
        /// </summary>
        public void Configure(float newDamage, float newSpeed, LayerMask newHitLayers)
        {
            if (newDamage >= 0f) damage = newDamage;
            if (newSpeed > 0f) speed = newSpeed;
            hitLayers = newHitLayers;
        }

        /// <summary>
        /// Ajusta el comportamiento avanzado (atravesar, perseguir, parábola, explosión final).
        /// Lo usan las habilidades para que un solo proyectil construido en código sirva de flecha
        /// perforante, de orbe teledirigido o de granada sin necesidad de un prefab por variante.
        /// </summary>
        public void ConfigureBehaviour(int newPierce, float newHomingTurnRate, float newHomingRange,
                                       float newArcGravity, float newImpactRadius, float newImpactDamage,
                                       float newKnockbackMultiplier, float newLifetime)
        {
            pierceCount = Mathf.Max(0, newPierce);
            homingTurnRate = Mathf.Max(0f, newHomingTurnRate);
            if (newHomingRange > 0f) homingRange = newHomingRange;
            arcGravity = Mathf.Max(0f, newArcGravity);
            impactRadius = Mathf.Max(0f, newImpactRadius);
            impactDamage = Mathf.Max(0f, newImpactDamage);
            if (newKnockbackMultiplier >= 0f) knockbackMultiplier = newKnockbackMultiplier;
            if (newLifetime > 0f) lifetime = newLifetime;

            _pierceLeft = pierceCount;
        }

        /// <summary>
        /// Hace que el daño de este proyectil cure a <paramref name="beneficiary"/> una fracción
        /// de lo que haga (robo de vida de las armas de nivel alto). Fracción 0 lo apaga.
        /// </summary>
        public void ConfigureLifesteal(Health beneficiary, float fraction)
        {
            _lifestealTarget = beneficiary;
            _lifesteal = Mathf.Clamp01(fraction);
        }

        /// <summary>
        /// Orientación de este disparo (<c>ProjectileSpec ▸ Aiming</c>): <paramref name="faceDirection"/>
        /// fuerza a rotar hacia donde vuela aunque el prefab no lo haga (<see cref="faceTravelDirection"/>
        /// apagado); <paramref name="axis"/> es el lado del sprite que hace de punta. Se llama antes de
        /// <see cref="Launch"/>; el pool lo reinicia en <c>OnEnable</c>.
        /// </summary>
        public void ConfigureFacing(bool faceDirection, ProjectileFacingAxis axis)
        {
            _forceFace = faceDirection;
            _facingAxis = axis;
        }

        /// <summary>
        /// Cura al dueño del disparo por una fracción del daño hecho. Se cura por el daño
        /// pretendido, no por la vida que le quedara al objetivo: rematar cura igual que golpear.
        /// </summary>
        private void Lifesteal(float damageDealt)
        {
            if (_lifesteal <= 0f || _lifestealTarget == null) return;
            _lifestealTarget.Heal(damageDealt * _lifesteal);
        }

        /// <summary>
        /// Marca el proyectil como "instanciado en caliente": al terminar se destruye en vez de
        /// desactivarse. Lo usan los proyectiles que se construyen en código (habilidades), que no
        /// vienen de ningún pool y quedarían acumulándose desactivados en la escena.
        /// </summary>
        public void DestroyWhenDone() => destroyWhenDone = true;

        /// <summary>Lanza el proyectil. <paramref name="owner"/> se ignora en las colisiones.</summary>
        public void Launch(Vector2 direction, GameObject owner = null)
        {
            _direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            _owner = owner;
            _lifeTimer = lifetime;
            _despawning = false;
            _pierceLeft = pierceCount;
            _hitTargets.Clear();
            _velocity = _direction * speed;

            // La etiqueta del lanzador marca a los suyos: la explosión y el proyectil que atraviesa
            // no deben dañar a quien dispara ni a sus aliados. "Untagged" no distingue a nadie, así
            // que en ese caso no se filtra (los enemigos sin etiqueta se comportan como hasta ahora).
            _friendlyTag = owner != null && !owner.CompareTag("Untagged") ? owner.tag : null;

            if (_body != null) _body.linearVelocity = _velocity;

            ApplyFacing(_direction);
        }

        private void Update()
        {
            if (_despawning) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f) Despawn();
        }

        private void FixedUpdate()
        {
            if (_despawning || _body == null) return;

            float dt = Time.fixedDeltaTime;

            if (homingTurnRate > 0f) SteerTowardsTarget(dt);

            if (arcGravity > 0f)
            {
                // Tiro parabólico: la velocidad deja de ser constante, así que a partir de aquí
                // manda _velocity y no dirección × velocidad.
                _velocity.y -= arcGravity * dt;
            }
            else
            {
                _velocity = _direction * speed;
            }

            _body.linearVelocity = _velocity;

            if (_velocity.sqrMagnitude > 0.0001f) ApplyFacing(_velocity.normalized);
        }

        /// <summary>
        /// Orienta el proyectil hacia <paramref name="direction"/>. Con
        /// <see cref="faceTravelDirection"/> rota el transform entero — la convención del resto
        /// del proyecto, que exige el arte autorizado mirando +X. Apagado, no rota nada: sólo
        /// refleja el <see cref="SpriteRenderer"/> en horizontal según el signo de X, así un
        /// sprite redondo (una seta, una roca) mantiene su "arriba" siempre arriba en vez de
        /// quedar boca abajo al volar hacia la izquierda.
        /// </summary>
        private void ApplyFacing(Vector2 direction)
        {
            if (faceTravelDirection || _forceFace)
            {
                ProjectileAim.Face(transform, direction, _facingAxis);
                return;
            }

            transform.rotation = Quaternion.identity;

            if (_renderer != null && Mathf.Abs(direction.x) > 0.0001f)
                _renderer.flipX = direction.x < 0f;
        }

        /// <summary>
        /// Gira la dirección hacia el objetivo válido más cercano, como mucho
        /// <see cref="homingTurnRate"/> grados por segundo. El giro es limitado a propósito: un
        /// proyectil que apunta perfecto es imposible de esquivar y no se lee como un disparo.
        /// </summary>
        private void SteerTowardsTarget(float dt)
        {
            var target = FindHomingTarget();
            if (target == null) return;

            Vector2 desired = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            float maxDegrees = homingTurnRate * dt;

            _direction = Vector3.RotateTowards(_direction, desired, maxDegrees * Mathf.Deg2Rad, 0f);
            _direction.Normalize();

            if (arcGravity > 0f) _velocity = _direction * _velocity.magnitude;
        }

        private Health FindHomingTarget()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
            int count = Physics2D.OverlapCircle(transform.position, homingRange, filter, ImpactBuffer);

            Health best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var health = ImpactBuffer[i] != null ? ImpactBuffer[i].GetComponentInParent<Health>() : null;
                if (!IsEnemyTarget(health)) continue;

                float distance = ((Vector2)health.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = health;
            }

            return best;
        }

        /// <summary>Objetivo dañable: vivo, no el que dispara, ni de su bando, ni ya golpeado.</summary>
        private bool IsEnemyTarget(Health health)
        {
            if (health == null || health.IsDead) return false;
            if (_owner != null && health.transform.IsChildOf(_owner.transform)) return false;
            if (!string.IsNullOrEmpty(_friendlyTag) && health.CompareTag(_friendlyTag)) return false;

            // Bandos: un enemigo no puede dañar a otro enemigo aunque su bala le cruce por delante
            // (los del pipeline nacen Untagged, así que _friendlyTag no filtra nada). Ver Teams.
            if (Teams.Allied(_owner, health)) return false;

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);

        private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

        private void HandleHit(Collider2D other)
        {
            if (_despawning || other == null) return;
            // Se ignora todo lo que no esté en las capas de impacto ni sea terreno.
            if (((1 << other.gameObject.layer) & (hitLayers | GroundMask)) == 0) return;

            // Ignorar a quien lo disparó y a los colliders propios.
            if (_owner != null && (other.gameObject == _owner || other.transform.IsChildOf(_owner.transform))) return;
            foreach (var own in _ownColliders)
                if (other == own) return;

            // Un proyectil nunca choca con otro. Sin esto, los cinco perdigones de una escopeta
            // —que salen del mismo punto y por tanto solapados— se detectan entre ellos en el
            // primer frame y se aniquilan mutuamente: el disparo parece no existir.
            if (other.GetComponentInParent<Projectile>() != null) return;

            var health = other.GetComponentInParent<Health>();

            if (health != null)
            {
                // Aliado o ya golpeado: se atraviesa sin gastar perforación ni terminar el disparo.
                if (!IsEnemyTarget(health) || _hitTargets.Contains(health)) return;

                if (damage > 0f)
                {
                    // El empujón sale del punto de impacto, así que empuja en el sentido en el que
                    // volaba el proyectil. Si los i-frames se comen el golpe, TakeDamage no empuja.
                    if (health.TakeDamage(damage, transform.position, knockbackMultiplier)) Lifesteal(damage);
                    _hitTargets.Add(health);
                    SoundTriggered?.Invoke(SoundTrigger.OnHit);
                }

                // Perforación: sólo se gasta contra objetivos, nunca contra el escenario.
                if (_pierceLeft > 0)
                {
                    _pierceLeft--;
                    return;
                }

                Despawn();
                return;
            }

            // Sin Health: sólo el terreno pintado (capa Ground) detiene el proyectil. Decoración y
            // zonas de trigger se atraviesan.
            if (((1 << other.gameObject.layer) & GroundMask) != 0) Despawn();
        }

        /// <summary>
        /// Apaga el proyectil para devolverlo al pool, sonando antes su OnDeath.
        ///
        /// Si el prefab trae un estado "Impact" (ver <see cref="_spriteMachine"/> — los que
        /// construye <c>FxPrefabBuilder</c> a partir de la biblioteca web), esa animación se
        /// reproduce ANTES de volver al pool, no después: devolverlo en el mismo frame apagaba el
        /// GameObject de inmediato y el impacto nunca llegaba a dibujarse ni un solo frame. La
        /// duración sale de la propia animación (<c>SpriteStateMachine.DurationOf</c>), así que
        /// cambiar el fps/frames del "Impact" no desincroniza nada aquí. Sin ese estado —el caso de
        /// todo proyectil anterior a esto, sprite fijo o placeholder— el comportamiento es idéntico
        /// al de siempre: vuelta inmediata al pool.
        /// </summary>
        public void Despawn()
        {
            if (_despawning) return;
            _despawning = true;

            // El sonido se dispara ANTES de desactivar el GameObject.
            SoundTriggered?.Invoke(SoundTrigger.OnDeath);

            ExplodeIfDue();

            // La explosión es un objeto aparte, así que sobrevive al despawn del proyectil.
            int facing = _direction.x < 0f ? -1 : 1;
            VfxOneShot.Spawn(impactEffect,
                             transform.position + new Vector3(impactEffectOffset.x * facing, impactEffectOffset.y, 0f),
                             facing);

            if (_body != null) _body.linearVelocity = Vector2.zero;

            if (_spriteMachine != null && _spriteMachine.Has("Impact"))
            {
                // PlayOnce: una sola pasada aunque el estado venga marcado con bucle (el exportador
                // web lo marca por defecto), bloqueado y sin volver a Move — se queda congelado en
                // el último frame del golpe hasta que el propio pool lo apague. Ver AnimStates.
                _spriteMachine.PlayOnce("Impact");
                StartCoroutine(ReturnToPoolAfter(_spriteMachine.DurationOf("Impact")));
                return;
            }

            ReturnToPool();
        }

        private IEnumerator ReturnToPoolAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            ReturnToPool();
        }

        /// <summary>
        /// Daño en área al terminar (granadas, bombas). Golpea una vez a cada objetivo del radio,
        /// incluidos los que el impacto directo ya tocó: la explosión es un golpe aparte, y de
        /// todos modos los i-frames del objetivo deciden si le entra.
        /// </summary>
        private void ExplodeIfDue()
        {
            if (impactRadius <= 0f || impactDamage <= 0f) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
            int count = Physics2D.OverlapCircle(transform.position, impactRadius, filter, ImpactBuffer);

            ExplosionTargets.Clear();

            for (int i = 0; i < count; i++)
            {
                var health = ImpactBuffer[i] != null ? ImpactBuffer[i].GetComponentInParent<Health>() : null;
                if (!IsEnemyTarget(health)) continue;
                if (!ExplosionTargets.Add(health)) continue;   // un objetivo con varios colliders es uno

                if (health.TakeDamage(impactDamage, transform.position, knockbackMultiplier)) Lifesteal(impactDamage);
            }
        }

        /// <summary>
        /// Devolución al pool según cómo se creó: <see cref="Abilities.ProjectileFactory"/> marca
        /// <see cref="PooledCode"/> / <see cref="PooledPrefab"/>. Sin marca, respeta
        /// <see cref="destroyWhenDone"/> (proyectil instanciado en caliente) o sólo desactiva.
        /// </summary>
        protected virtual void ReturnToPool()
        {
            if (PooledCode) { Abilities.ProjectileFactory.ReleaseCodePooled(this); return; }
            if (PooledPrefab) { Core.PrefabPool.Despawn(gameObject); return; }
            if (destroyWhenDone) Destroy(gameObject);
            else gameObject.SetActive(false);
        }

        /// <summary>
        /// Reaplica lo que cambia por disparo en un proyectil construido en código y reutilizado
        /// del pool: posición, sprite, tinte, tamaño, capa de ordenación y radio del collider.
        /// </summary>
        internal void ApplyCodeVisual(Vector3 origin, Vector2 worldSize, Sprite sprite, Color tint,
                                      GameObject sortingReference)
        {
            transform.position = origin;
            transform.rotation = Quaternion.identity;

            var renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer == null) return;

            renderer.sprite = sprite != null ? sprite : Abilities.AbilityFx.DefaultSprite;
            renderer.color = tint;
            Abilities.AbilityFx.CopySorting(renderer, sortingReference);
            Abilities.AbilityFx.Resize(renderer.transform, renderer, worldSize);

            var circle = GetComponent<CircleCollider2D>();
            if (circle != null && renderer.sprite != null)
            {
                var bounds = renderer.sprite.bounds.size;
                circle.radius = Mathf.Max(bounds.x, bounds.y) * 0.5f;
            }
        }
    }
}
