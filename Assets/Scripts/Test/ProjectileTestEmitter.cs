using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Torreta de pruebas: dispara cada <see cref="interval"/> segundos un proyectil enemigo (el mismo
    /// <see cref="ProjectileFactory"/> que usan enemigos y jefes, así que el parry lo trata igual que a
    /// uno de verdad). Apunta al jugador o en una dirección fija. Lleva <see cref="Health"/> para que
    /// el daño devuelto por el parry tenga a quién llegar; con <see cref="TrainingDummy"/> al lado no
    /// muere nunca y enseña lo que recibe.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ProjectileTestEmitter : MonoBehaviour
    {
        [Header("Disparo")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec
        {
            speed = 8f, lifetime = 5f, size = new Vector2(0.45f, 0.45f), muzzleOffset = Vector2.zero,
        };
        [Min(0f)] [SerializeField] private float damage = 10f;
        [Min(0f)] [SerializeField] private float knockbackMultiplier = 1f;
        [SerializeField] private Color tint = new Color(1f, 0.55f, 0.15f, 1f);

        [Header("Ritmo")]
        [Tooltip("Segundos entre ráfagas.")]
        [Min(0.05f)] [SerializeField] private float interval = 2.5f;
        [Tooltip("Retardo antes de la primera ráfaga (para desfasar varias torretas).")]
        [Min(0f)] [SerializeField] private float startDelay = 0f;
        [Tooltip("Proyectiles por ráfaga.")]
        [Min(1)] [SerializeField] private int burst = 1;
        [Min(0f)] [SerializeField] private float burstSpacing = 0.18f;

        [Header("Apuntado")]
        [SerializeField] private bool aimAtPlayer = true;
        [Tooltip("Dirección si no apunta al jugador.")]
        [SerializeField] private Vector2 fixedDirection = Vector2.right;
        [Tooltip("Desvío aleatorio en grados (± la mitad).")]
        [Min(0f)] [SerializeField] private float aimJitter = 0f;
        [Tooltip("No dispara si el jugador está más lejos que esto. 0 = siempre.")]
        [Min(0f)] [SerializeField] private float maxRange = 0f;

        private Health _health;
        private Transform _player;
        private float _timer;
        private int _burstLeft;
        private float _burstTimer;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable()
        {
            _timer = startDelay;
            _burstLeft = 0;
        }

        private void Update()
        {
            if (!GameStateManager.CanPlayerAct) return;

            if (_burstLeft > 0)
            {
                _burstTimer -= Time.deltaTime;
                if (_burstTimer <= 0f) FireOne();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = interval;

            if (maxRange > 0f && (Player() == null ||
                                  Vector2.Distance(_player.position, transform.position) > maxRange)) return;

            _burstLeft = burst;
            FireOne();
        }

        private void FireOne()
        {
            _burstLeft--;
            _burstTimer = burstSpacing;

            Vector2 direction = fixedDirection;
            if (aimAtPlayer && Player() != null)
                direction = (Vector2)(_player.position - transform.position);
            if (aimJitter > 0f)
                direction = Quaternion.Euler(0f, 0f, Random.Range(-aimJitter, aimJitter) * 0.5f) * direction;

            int facing = direction.x < 0f ? -1 : 1;
            var ctx = new AbilityContext(gameObject, this, _health, ~0, facing, direction, null);
            ProjectileFactory.Spawn(ctx, projectile, ctx.Muzzle(projectile.muzzleOffset), direction, damage,
                                    knockbackMultiplier, null, tint);
        }

        private Transform Player()
        {
            if (_player == null)
            {
                var go = GameObject.FindWithTag(Teams.PlayerTag);
                _player = go != null ? go.transform : null;
            }
            return _player;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            if (!aimAtPlayer) Gizmos.DrawRay(transform.position, (Vector3)fixedDirection.normalized * 1.5f);
        }
    }
}
