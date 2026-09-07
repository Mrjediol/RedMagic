using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Proyectil del sistema de armas por items. Se construye en código desde un
    /// <see cref="WeaponShot"/> ya resuelto (sin prefab, como los del sistema de habilidades).
    ///
    /// Lleva el <see cref="WeaponShot"/> entero, así que la trayectoria (auto-mira) y el elemento
    /// van consigo. Al impactar, si el shot todavía tiene generaciones de split, engendra
    /// <see cref="WeaponShot.splitCount"/> hijos con una copia del mismo shot (una generación
    /// menos): los hijos heredan auto-mira y elemento sin ninguna lógica especial.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class ShotProjectile : MonoBehaviour
    {
        private static readonly Collider2D[] Buffer = new Collider2D[32];

        private WeaponShot _shot;
        private ShotContext _ctx;
        private float _damage;
        private Vector2 _direction = Vector2.right;
        private float _lifeTimer;
        private bool _done;

        private Rigidbody2D _body;
        private readonly HashSet<Health> _hit = new HashSet<Health>();

        /// <summary>Elemento con el que va "pintado" este proyectil. Lo comprueba el test de herencia.</summary>
        public ElementId Element => _shot != null ? _shot.element : ElementId.None;

        public bool IsHoming => _shot != null && _shot.homingTurnRate > 0f;

        public float Damage => _damage;

        public int SplitGenerationsLeft => _shot != null ? _shot.splitGenerationsLeft : 0;

        /// <summary>Construye y lanza un proyectil. Lo usan el volley inicial y el split al impacto.</summary>
        public static ShotProjectile Spawn(WeaponShot shot, in ShotContext ctx, Vector2 position,
                                           Vector2 direction, float damage)
        {
            var go = AbilityFx.SpawnSprite("Shot Projectile", null, position, shot.size, shot.tint,
                                           0f, ctx.Caster);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            var renderer = go.GetComponent<SpriteRenderer>();
            var localSize = renderer != null && renderer.sprite != null
                ? (Vector2)renderer.sprite.bounds.size
                : Vector2.one;
            collider.radius = Mathf.Max(localSize.x, localSize.y) * 0.5f;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = go.AddComponent<ShotProjectile>();
            projectile.Init(shot, ctx, direction, damage);
            return projectile;
        }

        private void Awake() => _body = GetComponent<Rigidbody2D>();

        private void Init(WeaponShot shot, in ShotContext ctx, Vector2 direction, float damage)
        {
            _shot = shot;
            _ctx = ctx;
            _damage = damage;
            _direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            _lifeTimer = shot.lifetime;
            _done = false;

            if (_body == null) _body = GetComponent<Rigidbody2D>();
            _body.linearVelocity = _direction * shot.speed;
            transform.right = _direction;
        }

        private void Update()
        {
            if (_done) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f) Finish(impacted: false);
        }

        private void FixedUpdate()
        {
            if (_done || _body == null) return;

            if (_shot.homingTurnRate > 0f) Steer(Time.fixedDeltaTime);

            _body.linearVelocity = _direction * _shot.speed;
            if (_direction.sqrMagnitude > 0.0001f) transform.right = _direction;
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
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_done || other == null) return;

            // Un proyectil nunca choca con otro (los hijos de un split salen solapados).
            if (other.GetComponentInParent<ShotProjectile>() != null) return;

            bool onHitLayer = ((1 << other.gameObject.layer) & _ctx.HitLayers) != 0;
            var health = other.GetComponentInParent<Health>();

            if (health != null)
            {
                if (!IsTarget(health) || _hit.Contains(health)) return;

                float scaled = _damage * _ctx.DamageScale;
                if (scaled > 0f && health.TakeDamage(scaled, transform.position, 1f))
                    _hit.Add(health);

                Finish(impacted: true);
                return;
            }

            // Sin Health: escenario. Termina (y splitea) sólo si está en una capa de impacto.
            if (onHitLayer) Finish(impacted: true);
        }

        private void Finish(bool impacted)
        {
            if (_done) return;
            _done = true;

            if (impacted && _shot.splitGenerationsLeft > 0 && _shot.splitCount > 0)
                SpawnSplit();

            if (_body != null) _body.linearVelocity = Vector2.zero;
            Destroy(gameObject);
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
