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
    /// La animación concreta (suelo / aire) la elige el Animator a partir del parámetro Grounded
    /// que escribe <see cref="PlayerAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerAttack : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnAttack al iniciar el golpe. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

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

        [Tooltip("Cuánto empuja este golpe: multiplica el retroceso que cada enemigo tiene " +
                 "configurado en su componente Knockback. 1 = el suyo tal cual, 0 = no empuja.")]
        [Min(0f)]
        [SerializeField] private float knockbackMultiplier = 1f;

        [Header("Caja de daño (relativa al jugador, hacia donde mira)")]
        [Tooltip("Desplazamiento del centro. La X se invierte según la dirección del sprite.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.7f, 0.1f);
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.1f, 1.0f);
        [SerializeField] private LayerMask hitLayers = ~0;

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

        /// <summary>
        /// Silencia el ataque básico sin desactivar el componente. Lo usa <c>AbilityUser</c>
        /// mientras hay una habilidad equipada.
        ///
        /// <b>No vale con poner <c>enabled = false</c></b>: los dos scripts sacan su acción de
        /// ataque del mismo <see cref="InputActionAsset"/>, así que comparten el <b>mismo</b>
        /// objeto <see cref="InputAction"/>. Al desactivar este componente, su <c>OnDisable</c>
        /// llamaba a <c>Disable()</c> sobre esa acción compartida y dejaba también a la habilidad
        /// sin entrada: se equipaba y no pasaba nada al pulsar.
        ///
        /// Silenciado no consume la cola táctil: esas pulsaciones son para quien esté al mando.
        /// </summary>
        public bool Suppressed { get; set; }

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
            if (Suppressed) return;

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
            SoundTriggered?.Invoke(SoundTrigger.OnAttack);

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

                // TakeDamage devuelve false si el golpe no ha entrado (i-frames): en ese caso
                // tampoco se empuja, o un enemigo invulnerable saldría volando de todos modos.
                // Por PlayerHit: los items "al matar" cuentan también las bajas de espada.
                if (!Items.PlayerHit.Deal(target, damage, transform.position, 0f, Items.HitKind.Melee)) continue;

                // El empujón va hacia donde mira el jugador, no "alejándose de su posición": con
                // el enemigo pegado encima ambas cosas se separan, y lo que se espera de un
                // espadazo es que mande al bicho en la dirección del golpe.
                if (knockbackMultiplier > 0f)
                {
                    var knockback = target.GetComponent<Knockback>();
                    if (knockback != null)
                        knockback.Apply(_movement != null ? _movement.Facing : 1, knockbackMultiplier);
                }
            }
        }

        private Vector2 GetHitboxCenter()
        {
            int facing = _movement != null ? _movement.Facing : 1;

            var offset = new Vector2(hitboxOffset.x * facing, hitboxOffset.y);
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
