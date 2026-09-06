using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Ataque cuerpo a cuerpo del jugador, con las tres fuentes de entrada (teclado / mando /
    /// táctil) igual que el movimiento.
    ///
    /// El golpe no es instantáneo: al pulsar se dispara la animación y, tras <see cref="windup"/>
    /// segundos, se abre una caja de daño delante del personaje durante <see cref="activeTime"/>.
    /// Así el impacto cae más o menos donde lo enseña el sprite en vez de al pulsar el botón.
    ///
    /// La animación concreta (suelo / agachado / aire) la elige el Animator a partir de los
    /// parámetros Grounded y Crouching que escribe <see cref="PlayerAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string attackActionName = "Attack";

        [Header("Golpe")]
        [SerializeField] private float damage = 25f;
        [Tooltip("Tiempo mínimo entre ataques.")]
        [SerializeField] private float cooldown = 0.45f;
        [Tooltip("Retardo desde que se pulsa hasta que la caja de daño se activa.")]
        [SerializeField] private float windup = 0.12f;
        [Tooltip("Cuánto tiempo permanece activa la caja de daño.")]
        [SerializeField] private float activeTime = 0.1f;

        [Header("Caja de daño (relativa al jugador, hacia donde mira)")]
        [Tooltip("Desplazamiento del centro. La X se invierte según la dirección del sprite.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.7f, 0.1f);
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.1f, 1.0f);
        [Tooltip("Desplazamiento extra en Y cuando se ataca agachado.")]
        [SerializeField] private float crouchYOffset = -0.35f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("SFX — id de sonido del AudioManager")]
        [SerializeField] private string attackSfxId = "SFX_PlayerAttack";

        [Header("Depuración")]
        [Tooltip("Dibuja la caja de daño en la vista de escena.")]
        [SerializeField] private bool drawGizmo = true;

        private PlayerMovement _movement;
        private PlayerAnimator _animator;
        private Health _health;
        private InputAction _attackAction;

        private float _cooldownTimer;
        private bool _swinging;

        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private readonly HashSet<Health> _alreadyHit = new HashSet<Health>();
        private ContactFilter2D _filter;

        /// <summary>True mientras dura la ventana activa del golpe.</summary>
        public bool IsAttacking => _swinging;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _animator = GetComponent<PlayerAnimator>();
            _health = GetComponent<Health>();

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = hitLayers,
                useTriggers = true
            };

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                    _attackAction = map.FindAction(attackActionName, throwIfNotFound: false);
                else
                    Debug.LogWarning($"[PlayerAttack] No existe el action map '{actionMapName}' en {inputActions.name}.", this);
            }
        }

        private void OnEnable() => _attackAction?.Enable();

        private void OnDisable() => _attackAction?.Disable();

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            // En pausa o muerto no se ataca, pero se vacía la cola táctil para que no se acumule.
            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                TouchInput.ConsumeAttack();
                return;
            }

            bool pressed = (_attackAction != null && _attackAction.WasPressedThisFrame()) || TouchInput.ConsumeAttack();
            if (!pressed || _cooldownTimer > 0f || _swinging) return;

            _cooldownTimer = cooldown;
            if (_animator != null) _animator.TriggerAttack();
            if (!string.IsNullOrWhiteSpace(attackSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(attackSfxId);

            StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            _swinging = true;

            if (windup > 0f) yield return new WaitForSeconds(windup);

            _alreadyHit.Clear();
            float elapsed = 0f;

            // Se comprueba durante toda la ventana activa (no sólo un frame) para no fallar
            // contra enemigos que entran en el rango a mitad del golpe.
            while (elapsed < activeTime)
            {
                ApplyHit();
                elapsed += Time.deltaTime;
                yield return null;
            }

            _swinging = false;
        }

        private void ApplyHit()
        {
            Vector2 center = GetHitboxCenter();

            _hits.Clear();
            Physics2D.OverlapBox(center, hitboxSize, 0f, _filter, _hits);

            foreach (var hit in _hits)
            {
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var target = hit.GetComponentInParent<Health>();
                if (target == null || target == _health || target.IsDead) continue;
                if (!_alreadyHit.Add(target)) continue;   // un golpe por objetivo y por ataque

                target.TakeDamage(damage);
            }
        }

        private Vector2 GetHitboxCenter()
        {
            int facing = _movement != null ? _movement.Facing : 1;
            bool crouched = _movement != null && _movement.IsCrouching;

            var offset = new Vector2(hitboxOffset.x * facing,
                                     hitboxOffset.y + (crouched ? crouchYOffset : 0f));
            return (Vector2)transform.position + offset;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Gizmos.color = _swinging ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(1f, 0.8f, 0.2f, 0.45f);
            Gizmos.DrawWireCube(GetHitboxCenter(), hitboxSize);
        }
    }
}
