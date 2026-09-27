using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Proyectil del sistema de armas por items. Se construye en código desde un
    /// <see cref="WeaponShot"/> ya resuelto (sin prefab, como los del sistema de habilidades) y va
    /// <b>pooled</b> (<see cref="Core.Pool{T}"/>): el GameObject —sprite, collider, rigidbody— se
    /// arma una vez y se reutiliza; <see cref="Finish"/> lo devuelve al pool en vez de destruirlo.
    ///
    /// Lleva el <see cref="WeaponShot"/> entero, así que la trayectoria (auto-mira) y el elemento
    /// van consigo. Al impactar, si el shot todavía tiene generaciones de split, engendra
    /// <see cref="WeaponShot.splitCount"/> hijos con una copia del mismo shot (una generación
    /// menos): los hijos heredan auto-mira y elemento sin ninguna lógica especial.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class ShotProjectile : MonoBehaviour, Core.IPooled
    {
        // Capa 'Ground' del proyecto: el terreno pintado con el Tile Painter. Es lo único que
        // detiene un proyectil aparte de los objetivos con Health (igual que el clip de ShotBeam).
        private const int GroundMask = 1 << 6;

        private static readonly Collider2D[] Buffer = new Collider2D[32];
        private static readonly HashSet<Health> ExplodeSet = new HashSet<Health>();

        private static Core.Pool<ShotProjectile> _pool;

        [Tooltip("Rota el proyectil entero para que apunte hacia donde vuela (lo de siempre). Apágalo " +
                 "en un prefab con arte que no debe girar: entonces sólo se refleja en horizontal. " +
                 "El arma puede forzar el giro igualmente con BaseShot ▸ Aiming ▸ Face Direction.")]
        [SerializeField] private bool faceTravelDirection = true;

        /// <summary>El prefab gira hacia donde vuela (lo lee la vista previa de WeaponUser).</summary>
        public bool FacesTravelDirection => faceTravelDirection;

        private WeaponShot _shot;
        private ShotContext _ctx;
        private float _damage;
        private Vector2 _direction = Vector2.right;
        private Vector2 _velocity;
        private int _pierceLeft;
        private float _lifeTimer;
        private bool _done;

        /// <summary>Escondido entre la baja y la reaparición (Capa). Ni se mueve ni gasta vida.</summary>
        private bool _dormant;
        private Renderer[] _renderers;
        // Colliders del prefab (o el círculo del de código), con su estado original, y los de override
        // del arma (BaseShot ▸ Projectile Collider Override): uno por forma, creado la primera vez que
        // un arma lo pide y reutilizado después — nunca AddComponent por disparo.
        private Collider2D[] _prefabColliders;
        private bool[] _prefabEnabled;
        private CircleCollider2D _overrideCircle;
        private BoxCollider2D _overrideBox;
        private CapsuleCollider2D _overrideCapsule;
        private Collider2D _activeOverride;
        private TrailRenderer[] _trails;

        private Rigidbody2D _body;
        private SpriteRenderer _renderer;
        private CircleCollider2D _collider;
        private FxPlaceholderStyle _style;

        /// <summary>true = sacado de <see cref="Core.PrefabPool"/> (el arma trae prefab); false = del pool de código.</summary>
        private bool _pooledPrefab;

        private readonly HashSet<Health> _hit = new HashSet<Health>();

        /// <summary>Proyectil dorado (<see cref="GoldMark"/>): marca con oro a quien golpea.</summary>
        private bool _gilded;
        // Aura dorada: un sprite hijo creado la primera vez que esta instancia sale dorada y reutilizado.
        private SpriteRenderer _gildedAura;
        private Color _baseColor = Color.white; // color del sprite tras estilizarlo, para quitar el dorado

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        /// <summary>Elemento con el que va "pintado" este proyectil. Lo comprueba el test de herencia.</summary>
        public ElementId Element => _shot != null ? _shot.element : ElementId.None;

        public bool IsHoming => _shot != null && _shot.homingTurnRate > 0f;

        public float Damage => _damage;

        /// <summary>Salió dorado: marca de oro al golpear.</summary>
        public bool IsGilded => _gilded;

        public int SplitGenerationsLeft => _shot != null ? _shot.splitGenerationsLeft : 0;

        /// <summary>
        /// Saca del pool y lanza un proyectil. Lo usan el volley inicial y el split al impacto.
        ///
        /// Si el arma trae <see cref="WeaponShot.projectilePrefab"/> el proyectil sale de ese prefab
        /// por <see cref="Core.PrefabPool"/> (para poner arte real basta con editar el prefab); si
        /// no, o si el prefab está mal montado, se construye en código como hasta ahora.
        /// </summary>
        public static ShotProjectile Spawn(WeaponShot shot, in ShotContext ctx, Vector2 position,
                                           Vector2 direction, float damage)
        {
            ShotProjectile projectile = null;

            if (shot.projectilePrefab != null)
            {
                var go = Core.PrefabPool.Spawn(shot.projectilePrefab, position, Quaternion.identity);
                projectile = go != null ? go.GetComponent<ShotProjectile>() : null;

                if (projectile == null)
                {
                    Debug.LogWarning($"[Items] El prefab de proyectil '{shot.projectilePrefab.name}' " +
                                     $"no tiene componente ShotProjectile; se usa el proyectil de código.");
                    if (go != null) Core.PrefabPool.Despawn(go);
                }
                else
                {
                    projectile._pooledPrefab = true;
                }
            }

            if (projectile == null)
            {
                _pool ??= new Core.Pool<ShotProjectile>(Build, prewarm: 32);
                projectile = _pool.Get();
                projectile._pooledPrefab = false;
                projectile.transform.position = position;
            }

            projectile.Init(shot, ctx, direction, damage);
            return projectile;
        }

        /// <summary>Construcción cara: GameObject + sprite + collider + rigidbody, una vez por instancia.</summary>
        private static ShotProjectile Build()
        {
            var go = new GameObject("Shot Projectile");

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AbilityFx.DefaultSprite;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            var bounds = renderer.sprite.bounds.size;
            collider.radius = Mathf.Max(bounds.x, bounds.y) * 0.5f;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = go.AddComponent<ShotProjectile>();
            projectile._body = body;
            projectile._renderer = renderer;
            projectile._collider = collider;
            return projectile;
        }

        private void Awake()
        {
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_collider == null) _collider = GetComponent<CircleCollider2D>();
            if (_style == null) _style = GetComponent<FxPlaceholderStyle>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _prefabColliders = GetComponentsInChildren<Collider2D>(true);
            _prefabEnabled = new bool[_prefabColliders.Length];
            for (int i = 0; i < _prefabColliders.Length; i++) _prefabEnabled[i] = _prefabColliders[i].enabled;
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        void Core.IPooled.OnReturnedToPool() => ResetForReuse();

        /// <summary>
        /// El <see cref="Core.PrefabPool"/> no tiene hook por instancia: al reactivarse (spawn
        /// normal o reciclado forzado en carga de escena) queda inerte hasta que su <see cref="Init"/>
        /// lo relanza en el mismo frame.
        /// </summary>
        private void OnEnable() => ResetForReuse();

        private void ResetForReuse()
        {
            _done = true;
            SetDormant(false);
            _hit.Clear();
            if (_body != null) _body.linearVelocity = Vector2.zero;
            SetGilded(false);
        }

        private void Init(WeaponShot shot, in ShotContext ctx, Vector2 direction, float damage)
        {
            _shot = shot;
            _ctx = ctx;
            _damage = damage;
            _direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            _velocity = _direction * shot.speed;
            _pierceLeft = Mathf.Max(0, shot.pierce);
            _lifeTimer = shot.lifetime;
            _done = false;
            _hit.Clear();

            // Datos por disparo sobre el sprite reutilizado: tinte, tamaño y capa de ordenación.
            // El proyectil de código se estiliza siempre; el de prefab sólo si es un placeholder
            // (lleva FxPlaceholderStyle); el arte final se respeta tal cual.
            if (!_pooledPrefab)
            {
                _renderer.color = shot.tint;
                AbilityFx.Resize(transform, _renderer, shot.size);
                AbilityFx.CopySorting(_renderer, ctx.Caster);
            }
            else if (_style != null)
            {
                _style.Apply(shot.tint, shot.size, ctx.Caster);
            }

            // Después del tamaño: el override se da en unidades del mundo y se traduce con la escala final.
            ApplyColliderOverride(shot);

            _body.linearVelocity = _velocity;
            ApplyFacing(_direction);

            if (_renderer != null) _baseColor = _renderer.color;
            // Dorado: más daño, más probable (fórmula única en GoldMark). Tras el tamaño, que el aura lo sigue.
            SetGilded(GoldMark.RollGilded(damage * ctx.DamageScale, ctx.ForceGilded));
        }

        private void SetGilded(bool gilded)
        {
            bool was = _gilded;
            _gilded = gilded;
            if (!gilded)
            {
                if (_gildedAura != null) _gildedAura.enabled = false;
                if (was && _renderer != null) _renderer.color = _baseColor;
                return;
            }

            var t = SynergyConfig.CurrentTuning;
            if (_gildedAura == null)
            {
                var go = new GameObject("GildedAura");
                go.transform.SetParent(transform, false);
                _gildedAura = go.AddComponent<SpriteRenderer>();
                _gildedAura.sprite = Fx.ProceduralSprites.Glow;
            }
            if (t.goldAuraMaterial != null) _gildedAura.sharedMaterial = t.goldAuraMaterial;
            if (_renderer != null)
            {
                _gildedAura.sortingLayerID = _renderer.sortingLayerID;
                _gildedAura.sortingOrder = _renderer.sortingOrder - 1;
            }

            // Diámetro relativo al sprite del proyectil, en espacio local (el aura cuelga de su raíz).
            var body = _renderer != null && _renderer.sprite != null ? _renderer.sprite.bounds.size : Vector3.one;
            var bodyScale = _renderer != null ? _renderer.transform.localScale : Vector3.one;
            float diameter = Mathf.Max(body.x * bodyScale.x, body.y * bodyScale.y) * t.gildedAuraSize;
            var glow = _gildedAura.sprite.bounds.size;
            _gildedAura.transform.localScale = new Vector3(diameter / glow.x, diameter / glow.y, 1f);
            _gildedAura.color = t.gildedAuraColor;
            _gildedAura.enabled = !_dormant;

            // Y el propio sprite, teñido de oro: el halo solo no se lee en proyectiles finos.
            if (_renderer != null)
                _renderer.color = Color.Lerp(_baseColor, new Color(t.gildedTint.r, t.gildedTint.g, t.gildedTint.b, _baseColor.a), t.gildedTint.a);
        }

        /// <summary>
        /// Orientación (Gameplay.ProjectileAim): gira con la punta del arma (BaseShot ▸ Aiming ▸ Facing
        /// Axis) si el prefab gira o el arma lo fuerza; si no, sin rotación y reflejado según la X.
        /// </summary>
        private void ApplyFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            if (faceTravelDirection || (_shot != null && _shot.faceDirection))
            {
                if (_renderer != null) _renderer.flipX = false;
                Gameplay.ProjectileAim.Face(transform, direction, _shot != null ? _shot.facingAxis : Gameplay.ProjectileFacingAxis.Right);
                return;
            }

            transform.rotation = Quaternion.identity;
            if (_renderer != null && Mathf.Abs(direction.x) > 0.0001f) _renderer.flipX = direction.x < 0f;
        }

        private void Update()
        {
            if (_gilded && _gildedAura != null)
            {
                var t = SynergyConfig.CurrentTuning;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * t.gildedPulseSpeed * Mathf.PI * 2f);
                var c = t.gildedAuraColor;
                _gildedAura.color = new Color(c.r, c.g, c.b, c.a * Mathf.Lerp(0.65f, 1f, wave));
            }

            if (_done || _dormant) return;

            _lifeTimer -= Time.deltaTime;
            // Una granada (radio de explosión) revienta al agotar la vida; el resto sólo desaparece.
            if (_lifeTimer <= 0f) Finish(impacted: _shot.impactRadius > 0f);
        }

        private void FixedUpdate()
        {
            if (_done || _dormant || _body == null) return;

            float dt = Time.fixedDeltaTime;

            if (_shot.homingTurnRate > 0f)
            {
                // La auto-mira manda sobre la parábola si el jugador equipa las dos.
                Steer(dt);
                _velocity = _direction * _shot.speed;
            }
            else if (_shot.arcGravity > 0f)
            {
                _velocity += Vector2.down * (_shot.arcGravity * dt);
                if (_velocity.sqrMagnitude > 0.0001f) _direction = _velocity.normalized;
            }
            else
            {
                _velocity = _direction * _shot.speed;
            }

            _body.linearVelocity = _velocity;
            ApplyFacing(_direction);
        }

        private void Steer(float dt)
        {
            var target = NearestTarget();
            if (target == null) return;

            Vector2 desired = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            float maxRadians = _shot.homingTurnRate * dt * Mathf.Deg2Rad;
            _direction = ((Vector2)Vector3.RotateTowards(_direction, desired, maxRadians, 0f)).normalized;
        }

        private Health NearestTarget()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int count = Physics2D.OverlapCircle(transform.position, _shot.homingRange, filter, Buffer);

            Health best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i] != null ? Buffer[i].GetComponentInParent<Health>() : null;
                if (!IsTarget(health)) continue;

                float distance = ((Vector2)health.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = health;
            }

            return best;
        }

        private bool IsTarget(Health health)
        {
            if (health == null || health.IsDead) return false;
            if (_ctx.CasterHealth != null && health == _ctx.CasterHealth) return false;
            if (_ctx.Caster != null && health.transform.IsChildOf(_ctx.Caster.transform)) return false;
            if (!string.IsNullOrEmpty(_ctx.FriendlyTag) && health.CompareTag(_ctx.FriendlyTag)) return false;

            // Bandos (Combat.Teams): sólo el jugador daña a los enemigos y al revés. La etiqueta
            // amiga de arriba no basta cuando quien dispara no lleva etiqueta.
            if (Teams.Allied(_ctx.Caster, health)) return false;
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_done || _dormant || other == null) return;

            // Un proyectil nunca choca con otro (los hijos de un split salen solapados).
            if (other.GetComponentInParent<ShotProjectile>() != null) return;

            var health = other.GetComponentInParent<Health>();

            if (health != null)
            {
                if (!IsTarget(health) || _hit.Contains(health)) return;

                // El centro del cuerpo se toma ANTES del golpe: al morir, el cadáver apaga su collider.
                Vector2 body = other.bounds.center;

                float scaled = _damage * _ctx.DamageScale;
                bool killed = false;

                // Marca ANTES del golpe: si éste lo mata, su botín ya sale multiplicado.
                if (_gilded && scaled > 0f) GoldMark.Mark(health);
                if (scaled > 0f) PlayerHit.Deal(health, scaled, transform.position, 1f, HitKind.Projectile, out killed);
                _hit.Add(health);   // aunque no entre (i-frames), no lo re-golpeamos este vuelo

                // Primer impacto del disparo (Yelmo: la explosión sale aquí, en el cuerpo golpeado).
                _ctx.Impact?.Fire(body);

                // Capa (atravesar al matar): desaparece, espera y reaparece en el cuerpo del muerto con
                // la misma dirección y velocidad. No gasta perforación ni explota / splitea: sigue.
                if (killed && CombatModifiers.PierceOnKill(out float delay))
                {
                    StartCoroutine(ResumeAfterKill(body, delay));
                    return;
                }

                // Atraviesa hasta agotar la perforación; el último impacto sí explota / splitea.
                if (_pierceLeft > 0)
                {
                    _pierceLeft--;
                    return;
                }

                Finish(impacted: true);
                return;
            }

            // Sin Health: sólo el terreno pintado (capa Ground) detiene el proyectil. Decoración,
            // zonas de trigger (caldero, tumba…) y cualquier otro prop se atraviesan.
            if (((1 << other.gameObject.layer) & GroundMask) != 0) Finish(impacted: true);
        }

        /// <summary>
        /// Capa: el proyectil que mata se esconde <paramref name="delay"/> segundos y reaparece en
        /// <paramref name="position"/> (el cuerpo del muerto) con la misma velocidad. La vida del
        /// proyectil no corre mientras está escondido. Si vuelve al pool entretanto (carga de
        /// escena), el OnEnable del siguiente uso lo despierta y la corrutina ya murió con él.
        /// </summary>
        private System.Collections.IEnumerator ResumeAfterKill(Vector2 position, float delay)
        {
            SetDormant(true);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (_done) yield break;

            transform.position = position;
            if (_body != null) _body.position = position;
            foreach (var trail in _trails) if (trail != null) trail.Clear();

            SetDormant(false);
            if (_body != null) _body.linearVelocity = _velocity;
        }

        /// <summary>
        /// Collider de esta arma (BaseShot ▸ Projectile Collider Override). Encendido: se usa un
        /// collider propio de la forma pedida, medido en unidades del mundo, y los del prefab se
        /// apagan. Apagado: los del prefab vuelven tal cual. El prefab compartido nunca se modifica,
        /// así dos armas con el mismo prefab pueden tener colisiones distintas.
        /// </summary>
        private void ApplyColliderOverride(WeaponShot shot)
        {
            _activeOverride = null;

            if (shot.overrideCollider)
            {
                Collider2D collider = shot.colliderType switch
                {
                    Gameplay.ProjectileColliderType.Box => _overrideBox ??= NewOverride<BoxCollider2D>(),
                    Gameplay.ProjectileColliderType.Capsule => _overrideCapsule ??= NewOverride<CapsuleCollider2D>(),
                    _ => _overrideCircle ??= NewOverride<CircleCollider2D>(),
                };

                Gameplay.ProjectileColliderShape.Configure(collider, shot.colliderType, shot.colliderSize,
                                                  shot.colliderOffset, transform.lossyScale);
                _activeOverride = collider;
            }

            RefreshColliders(!_dormant);
        }

        private T NewOverride<T>() where T : Collider2D
        {
            var collider = gameObject.AddComponent<T>();
            collider.isTrigger = true;
            collider.enabled = false;
            return collider;
        }

        /// <summary>Enciende el juego de colliders que toca (override o prefab), o ninguno si <paramref name="on"/> es false.</summary>
        private void RefreshColliders(bool on)
        {
            if (_prefabColliders != null)
                for (int i = 0; i < _prefabColliders.Length; i++)
                    if (_prefabColliders[i] != null)
                        _prefabColliders[i].enabled = on && _activeOverride == null && _prefabEnabled[i];

            if (_overrideCircle != null) _overrideCircle.enabled = on && _activeOverride == _overrideCircle;
            if (_overrideBox != null) _overrideBox.enabled = on && _activeOverride == _overrideBox;
            if (_overrideCapsule != null) _overrideCapsule.enabled = on && _activeOverride == _overrideCapsule;
        }

        private void SetDormant(bool dormant)
        {
            _dormant = dormant;
            if (dormant && _body != null) _body.linearVelocity = Vector2.zero;

            if (_renderers != null)
                foreach (var r in _renderers) if (r != null) r.enabled = !dormant;
            if (_gildedAura != null) _gildedAura.enabled = _gilded && !dormant;
            RefreshColliders(!dormant);
        }

        private void Finish(bool impacted)
        {
            if (_done) return;
            _done = true;

            // Terreno o fin de vida sin haber tocado a nadie: el aviso de impacto salta donde acaba,
            // para que la carga de un efecto (Yelmo) no se pierda en un disparo al aire.
            _ctx.Impact?.Fire(transform.position);

            if (impacted && _shot.impactRadius > 0f && _shot.impactDamage > 0f)
                Explode();

            if (impacted && _shot.splitGenerationsLeft > 0 && _shot.splitCount > 0)
                SpawnSplit();

            if (_body != null) _body.linearVelocity = Vector2.zero;

            if (_pooledPrefab) Core.PrefabPool.Despawn(gameObject);
            else _pool.Release(this);
        }

        /// <summary>
        /// Explosión al terminar (granada): daño en círculo a todo objetivo válido en el radio, más
        /// un flash del tinte del disparo. El elemento ya va en el <see cref="WeaponShot"/>, así que
        /// una granada de hielo/fuego "pinta" su explosión sin código extra.
        /// </summary>
        private void Explode()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int count = Physics2D.OverlapCircle(transform.position, _shot.impactRadius, filter, Buffer);

            ExplodeSet.Clear();
            float damage = _shot.impactDamage * _ctx.DamageScale;
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i] != null ? Buffer[i].GetComponentInParent<Health>() : null;
                if (!IsTarget(health) || !ExplodeSet.Add(health)) continue;
                PlayerHit.Deal(health, damage, transform.position, 1f, HitKind.Projectile);
            }

            var tint = new Color(_shot.tint.r, _shot.tint.g, _shot.tint.b, 0.55f);
            AbilityFx.Flash(null, transform.position, Vector2.one * (_shot.impactRadius * 2f),
                            tint, 0.25f, 0f, 1.15f, _ctx.Caster);
        }

        /// <summary>
        /// Engendra los hijos del split: misma copia de shot para todos (una generación menos),
        /// repartidos en un abanico centrado en la dirección de vuelo. Heredan auto-mira y
        /// elemento por venir del mismo <see cref="WeaponShot"/>.
        /// </summary>
        private void SpawnSplit()
        {
            var child = _shot.SplitChild();
            int count = Mathf.Max(1, _shot.splitCount);

            float baseAngle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            const float spread = 120f;
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = baseAngle - spread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? start + step * i : baseAngle;
                Spawn(child, _ctx, transform.position, ShotResolver.DirFromAngle(angle), child.damage);
            }
        }
    }
}
