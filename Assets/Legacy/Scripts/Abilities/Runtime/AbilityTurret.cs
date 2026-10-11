using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Tótem invocado: se queda quieto, busca objetivos a su alrededor y les dispara hasta que se
    /// le acaba el tiempo.
    ///
    /// Hereda el bando de quien lo invocó (el <see cref="AbilityContext"/> que se le pasa), así que
    /// nunca dispara a los suyos. Lo crea <see cref="TurretAbility"/> en código: no hay prefab que
    /// mantener.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityTurret : MonoBehaviour
    {
        private AbilityContext _ctx;
        private ProjectileSpec _spec;
        private Sprite _projectileSprite;
        private Color _projectileTint;

        private float _damage;
        private float _knockbackMultiplier;
        private float _range;
        private float _fireInterval;
        private float _lifeTimer;
        private float _fireTimer;

        public void Configure(in AbilityContext ctx, ProjectileSpec spec, float damage,
                              float knockbackMultiplier, float range, float fireInterval,
                              float duration, Sprite projectileSprite, Color projectileTint)
        {
            _ctx = ctx;
            _spec = spec;
            _damage = damage;
            _knockbackMultiplier = knockbackMultiplier;
            _range = Mathf.Max(1f, range);
            _fireInterval = Mathf.Max(0.05f, fireInterval);
            _lifeTimer = Mathf.Max(0.2f, duration);
            _projectileSprite = projectileSprite;
            _projectileTint = projectileTint;

            // Dispara nada más plantarse: un tótem que tarda un ciclo en arrancar parece roto.
            _fireTimer = 0f;
        }

        private void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _fireTimer -= Time.deltaTime;
            if (_fireTimer > 0f) return;

            var target = FindTarget();
            if (target == null) return;

            _fireTimer = _fireInterval;

            Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            ProjectileFactory.Spawn(_ctx, _spec, transform.position, direction, _damage,
                                    _knockbackMultiplier, _projectileSprite, _projectileTint);
        }

        private Combat.Health FindTarget()
        {
            var targets = AbilityHit.OverlapCircle(_ctx, transform.position, _range);

            Combat.Health best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                float distance = ((Vector2)targets[i].transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = targets[i];
            }

            return best;
        }
    }
}
