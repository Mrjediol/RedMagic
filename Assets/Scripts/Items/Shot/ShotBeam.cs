using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Entrega de haz del sistema de armas por items: un rayo instantáneo que <b>barre</b> durante
    /// <see cref="WeaponShot.beamDuration"/> en vez de un proyectil que viaja. Lo usa
    /// <see cref="ShotResolver"/> cuando el disparo base es <see cref="ShotDelivery.Hitscan"/>
    /// (arma tipo Rayo Arcano / Brimstone).
    ///
    /// Se construye en código (sprite estirado, sin prefab) y va <b>pooled</b>
    /// (<see cref="Core.Pool{T}"/>): el sprite se crea una vez y se reutiliza. Sigue al lanzador
    /// mientras dura y aplica daño en caja cada <see cref="WeaponShot.beamTickInterval"/>. Siempre
    /// atraviesa: un haz golpea a todo lo que tenga en línea.
    ///
    /// Hereda del <see cref="WeaponShot"/> ya resuelto:
    /// <list type="bullet">
    /// <item><b>Trayectoria</b>: con <see cref="WeaponShot.homingTurnRate"/> &gt; 0 (Auto-mira) el
    /// haz gira hacia el enemigo más cercano mientras dura; si no, apunta fijo a donde se disparó.</item>
    /// <item><b>Elemento</b>: <see cref="WeaponShot.element"/> / <see cref="WeaponShot.tint"/> pintan
    /// el haz y cada tick de daño, sin código extra.</item>
    /// <item><b>Carga</b>: <see cref="WeaponShot.chargeFraction"/> escala alcance y duración.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShotBeam : MonoBehaviour, Core.IPooled
    {
        // Capa 'Ground' del proyecto (igual que BeamAbility): corta el haz en la primera pared.
        private const int GroundMask = 1 << 6;

        private static readonly Collider2D[] Buffer = new Collider2D[32];
        private static readonly HashSet<Health> TickSet = new HashSet<Health>();

        private static Core.Pool<ShotBeam> _pool;

        private WeaponShot _shot;
        private ShotContext _ctx;
        private Vector2 _direction = Vector2.right;

        private SpriteRenderer _renderer;
        private float _life;
        private float _lifeLeft;
        private float _reach;
        private float _tickTimer;
        private Color _tint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        /// <summary>Elemento con el que va pintado el haz. Lo comprueba el test de herencia.</summary>
        public ElementId Element => _shot != null ? _shot.element : ElementId.None;

        public bool IsHoming => _shot != null && _shot.homingTurnRate > 0f;

        public static ShotBeam Spawn(WeaponShot shot, in ShotContext ctx, Vector2 direction)
        {
            _pool ??= new Core.Pool<ShotBeam>(Build, prewarm: 4);

            float frac = Mathf.Clamp01(shot.chargeFraction);
            float reach = shot.beamLength * Mathf.Lerp(0.55f, 1f, frac);

            var beam = _pool.Get();
            beam.Init(shot, ctx, direction, reach, frac);

            // Fogonazo corto en la boca, del color del haz.
            AbilityFx.Flash(null, ctx.Muzzle(shot.muzzleOffset), Vector2.one * (shot.beamWidth * 2.5f),
                            new Color(shot.tint.r, shot.tint.g, shot.tint.b, 0.6f), 0.12f, 0f, 1.4f, ctx.Caster);
            return beam;
        }

        private static ShotBeam Build()
        {
            var go = new GameObject("Shot Beam");
            go.AddComponent<SpriteRenderer>().sprite = AbilityFx.DefaultSprite;
            var beam = go.AddComponent<ShotBeam>();
            beam._renderer = go.GetComponent<SpriteRenderer>();
            return beam;
        }

        void Core.IPooled.OnReturnedToPool()
        {
            _shot = null;
        }

        private void Init(WeaponShot shot, in ShotContext ctx, Vector2 direction, float reach, float chargeFraction)
        {
            _shot = shot;
            _ctx = ctx;
            _direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            _reach = reach;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            _tint = new Color(shot.tint.r, shot.tint.g, shot.tint.b, 0.85f);
            _renderer.color = _tint;
            AbilityFx.CopySorting(_renderer, ctx.Caster);

            _life = _lifeLeft = shot.beamDuration * Mathf.Lerp(0.5f, 1f, chargeFraction);
            _tickTimer = 0f;

            Tick();          // primer tick de daño ya, en el frame del disparo
            UpdateVisual();
        }

        private void Update()
        {
            if (_shot == null) return;

            float dt = Time.deltaTime;
            _lifeLeft -= dt;

            if (_shot.homingTurnRate > 0f) Steer(dt);

            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = Mathf.Max(0.02f, _shot.beamTickInterval);
                Tick();
            }

            UpdateVisual();

            if (_lifeLeft <= 0f) _pool.Release(this);
        }

        private void Steer(float dt)
        {
            var target = NearestTarget();
            if (target == null) return;

            Vector2 desired = ((Vector2)target.transform.position - Origin()).normalized;
            float maxRadians = _shot.homingTurnRate * dt * Mathf.Deg2Rad;
            _direction = ((Vector2)Vector3.RotateTowards(_direction, desired, maxRadians, 0f)).normalized;
        }

        private Vector2 Origin() => _ctx.Muzzle(_shot.muzzleOffset);

        private void Tick()
        {
            Vector2 origin = Origin();
            float reach = _reach;

            var clip = Physics2D.Raycast(origin, _direction, reach, GroundMask);
            if (clip.collider != null) reach = clip.distance;

            Vector2 center = origin + _direction * (reach * 0.5f);
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int count = Physics2D.OverlapBox(center, new Vector2(reach, _shot.beamWidth), angle, filter, Buffer);

            TickSet.Clear();
            float damage = _shot.damage * _ctx.DamageScale;
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i] != null ? Buffer[i].GetComponentInParent<Health>() : null;
                if (!IsTarget(health) || !TickSet.Add(health)) continue;
                if (damage > 0f) health.TakeDamage(damage, health.transform.position, 1f);
            }
        }

        private Health NearestTarget()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int count = Physics2D.OverlapCircle(Origin(), _shot.homingRange, filter, Buffer);

            Health best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i] != null ? Buffer[i].GetComponentInParent<Health>() : null;
                if (!IsTarget(health)) continue;

                float distance = ((Vector2)health.transform.position - Origin()).sqrMagnitude;
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

        private void UpdateVisual()
        {
            if (_renderer == null) return;

            Vector2 origin = Origin();
            float reach = _reach;
            var clip = Physics2D.Raycast(origin, _direction, reach, GroundMask);
            if (clip.collider != null) reach = clip.distance;

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.position = origin + _direction * (reach * 0.5f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            AbilityFx.Resize(transform, _renderer, new Vector2(reach, _shot.beamWidth));

            // Se desvanece en el último 40% de vida.
            float t = _life > 0f ? Mathf.Clamp01(_lifeLeft / (_life * 0.4f)) : 1f;
            var color = _tint;
            color.a = _tint.a * t;
            _renderer.color = color;
        }
    }
}
