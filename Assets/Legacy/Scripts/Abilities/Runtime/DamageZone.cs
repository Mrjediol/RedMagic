using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Zona de daño que persiste en el escenario: un charco que va haciendo daño mientras estés
    /// dentro, una mina que espera y revienta, o un orbe que gira alrededor del jugador.
    ///
    /// La crean las habilidades en tiempo de ejecución (<see cref="Configure"/>), no un prefab,
    /// porque lo único que cambia entre un charco y una mina son números y un modo. El objeto
    /// visual lo monta <see cref="AbilityFx"/> y este componente sólo pone la lógica encima.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageZone : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Hace daño cada <c>tickInterval</c> mientras haya objetivos dentro.</summary>
            Continuous,

            /// <summary>Espera a que entre un objetivo, explota una vez y desaparece.</summary>
            ProximityBomb
        }

        private AbilityContext _ctx;
        private Mode _mode;
        private float _radius;
        private float _damage;
        private float _tickInterval;
        private float _knockbackMultiplier;
        private float _lifeTimer;
        private float _tickTimer;
        private float _armTimer;
        private bool _spent;

        private Sprite _explosionSprite;
        private Color _explosionColor;

        /// <param name="armDelay">Segundos antes de que la mina pueda dispararse (o el charco empiece a dañar).</param>
        public void Configure(in AbilityContext ctx, Mode mode, float radius, float damage,
                              float duration, float tickInterval, float knockbackMultiplier,
                              float armDelay = 0.15f, Sprite explosionSprite = null,
                              Color explosionColor = default)
        {
            _ctx = ctx;
            _mode = mode;
            _radius = Mathf.Max(0.1f, radius);
            _damage = Mathf.Max(0f, damage);
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            _knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            _lifeTimer = Mathf.Max(0.05f, duration);
            _armTimer = Mathf.Max(0f, armDelay);
            _tickTimer = 0f;

            _explosionSprite = explosionSprite;
            _explosionColor = explosionColor.a <= 0f ? new Color(1f, 0.6f, 0.2f, 0.75f) : explosionColor;
        }

        /// <summary>Mueve la zona con un transform (orbes que siguen al jugador).</summary>
        public void Attach(Transform follow, Vector3 localOffset)
        {
            if (follow == null) return;
            transform.SetParent(follow, worldPositionStays: false);
            transform.localPosition = localOffset;
        }

        private void Update()
        {
            if (_spent) return;

            float dt = Time.deltaTime;

            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_armTimer > 0f)
            {
                _armTimer -= dt;
                return;
            }

            if (_mode == Mode.ProximityBomb) UpdateBomb();
            else UpdateContinuous(dt);
        }

        private void UpdateContinuous(float dt)
        {
            _tickTimer -= dt;
            if (_tickTimer > 0f) return;

            _tickTimer = _tickInterval;
            AbilityHit.DamageCircle(_ctx, transform.position, _radius, _damage, _knockbackMultiplier);
        }

        private void UpdateBomb()
        {
            var targets = AbilityHit.OverlapCircle(_ctx, transform.position, _radius);
            if (targets.Count == 0) return;

            Explode();
        }

        private void Explode()
        {
            _spent = true;

            AbilityHit.DamageCircle(_ctx, transform.position, _radius, _damage, _knockbackMultiplier);

            AbilityFx.Flash(_explosionSprite, transform.position, Vector2.one * _radius * 2f,
                            _explosionColor, 0.25f, 0f, 1.4f, _ctx.Caster);

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
