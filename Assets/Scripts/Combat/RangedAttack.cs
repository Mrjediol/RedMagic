using System.Collections;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Combat
{
    /// <summary>
    /// Ataque a distancia reutilizable: dispara un proyectil (la bola de fuego) hacia donde mira
    /// el personaje. Está pensado para ponerse <b>tal cual</b> tanto en el jugador como en un
    /// enemigo — lo único que cambia es el modo de disparo y las capas a las que hace daño.
    ///
    /// Modos (<see cref="TriggerMode"/>):
    ///  - <b>PlayerInput</b>: lo dispara el jugador con la acción de input y el botón táctil.
    ///  - <b>AutoDetect</b>: IA sencilla, dispara solo cuando hay un objetivo a tiro.
    ///  - <b>External</b>: no dispara por su cuenta; otro script llama a <see cref="TryFire()"/>
    ///    (útil para encadenarlo a una máquina de estados o a un evento de animación).
    ///
    /// La dirección sale de <c>PlayerMovement.Facing</c> si existe, y si no del <c>flipX</c> del
    /// SpriteRenderer — que es justo lo que ya usa <c>EnemyController</c> al patrullar.
    ///
    /// El proyectil sale de <see cref="Core.PrefabPool"/>, nunca de <c>Instantiate</c>: en modo
    /// <b>AutoDetect</b> dispara con cooldown durante toda la pelea, así que es justo el caso que
    /// la regla del proyecto de poolear lo que se repite pide cubrir.
    /// </summary>
    [DisallowMultipleComponent]
    public class RangedAttack : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnAttack al disparar. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public enum TriggerMode
        {
            External,
            PlayerInput,
            AutoDetect
        }

        [Header("Modo de disparo")]
        [SerializeField] private TriggerMode mode = TriggerMode.PlayerInput;

        [Header("Proyectil")]
        [Tooltip("Prefab con el componente Projectile (p. ej. Fireball).")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Punto de salida respecto al personaje. La X se invierte según hacia dónde mira.")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.65f, 0.15f);
        [Tooltip("Daño del proyectil. Negativo = deja el que traiga el prefab.")]
        [SerializeField] private float damage = 20f;
        [Tooltip("Velocidad del proyectil. 0 o menos = deja la del prefab.")]
        [SerializeField] private float projectileSpeed = 12f;
        [Tooltip("Capas a las que puede dañar el proyectil. Aquí es donde se distingue un " +
                 "disparo del jugador (que daña enemigos) de uno enemigo (que daña al jugador).")]
        [SerializeField] private LayerMask projectileHitLayers = ~0;

        [Header("Ritmo")]
        [SerializeField] private float cooldown = 0.85f;
        [Tooltip("Retardo entre iniciar el ataque y soltar el proyectil, para cuadrar con la animación.")]
        [SerializeField] private float windup = 0.15f;

        [Header("Animación y sonido")]
        [Tooltip("Trigger del Animator al atacar. Vacío = no se toca el Animator.")]
        [SerializeField] private string animatorTrigger = "Attack";
        [Tooltip("Efecto opcional en la boca del disparo.")]
        [SerializeField] private GameObject muzzleEffect;

        [Header("Input — sólo en modo PlayerInput")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Fireball";
        [Tooltip("Añade también el botón táctil de bola de fuego.")]
        [SerializeField] private bool useTouchButton = true;

        [Header("IA — sólo en modo AutoDetect")]
        [Tooltip("Distancia a la que detecta al objetivo.")]
        [SerializeField] private float detectionRange = 8f;
        [Tooltip("Etiqueta del objetivo. Vacío = cualquier cosa con Health que no sea uno mismo.")]
        [SerializeField] private string targetTag = "Player";
        [Tooltip("Sólo dispara si el objetivo está del lado hacia el que mira.")]
        [SerializeField] private bool onlyFireWhenFacing = true;
        [Tooltip("Se gira hacia el objetivo antes de disparar.")]
        [SerializeField] private bool turnToFaceTarget = true;
        [Tooltip("Margen vertical: no dispara a objetivos muy por encima o por debajo.")]
        [SerializeField] private float verticalTolerance = 2.5f;

        [Header("Depuración")]
        [SerializeField] private bool drawGizmos = true;

        private PlayerMovement _movement;
        private SpriteRenderer _sprite;
        private Animator _animator;
        private Health _health;
        private InputAction _fireAction;

        private float _cooldownTimer;
        private bool _firing;

        /// <summary>True si puede disparar ahora mismo (sin cooldown, vivo y no ocupado).</summary>
        public bool CanFire => _cooldownTimer <= 0f && !_firing && !IsDead;

        private bool IsDead => _health != null && _health.IsDead;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();

            if (mode == TriggerMode.PlayerInput && inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                    _fireAction = map.FindAction(actionName, throwIfNotFound: false);
                else
                    Debug.LogWarning($"[RangedAttack] No existe el action map '{actionMapName}' en {inputActions.name}.", this);
            }
        }

        private void OnEnable() => _fireAction?.Enable();

        private void OnDisable() => _fireAction?.Disable();

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (!GameStateManager.CanPlayerAct || IsDead)
            {
                if (mode == TriggerMode.PlayerInput && useTouchButton) TouchInput.ConsumeFireball();
                return;
            }

            switch (mode)
            {
                case TriggerMode.PlayerInput:
                    bool pressed = (_fireAction != null && _fireAction.WasPressedThisFrame())
                                   || (useTouchButton && TouchInput.ConsumeFireball());
                    if (pressed) TryFire();
                    break;

                case TriggerMode.AutoDetect:
                    UpdateAutoDetect();
                    break;
            }
        }

        // ------------------------------------------------------------------ API pública

        /// <summary>Dispara hacia donde mira el personaje. Devuelve false si aún no puede.</summary>
        public bool TryFire() => TryFire(new Vector2(ResolveFacing(), 0f));

        /// <summary>Dispara en una dirección concreta. Devuelve false si aún no puede.</summary>
        public bool TryFire(Vector2 direction)
        {
            if (!CanFire || projectilePrefab == null) return false;
            if (direction.sqrMagnitude < 0.0001f) direction = new Vector2(ResolveFacing(), 0f);

            _cooldownTimer = cooldown;
            _firing = true;

            if (_animator != null && !string.IsNullOrWhiteSpace(animatorTrigger))
                _animator.SetTrigger(animatorTrigger);

            SoundTriggered?.Invoke(SoundTrigger.OnAttack);

            StartCoroutine(FireRoutine(direction.normalized));
            return true;
        }

        private IEnumerator FireRoutine(Vector2 direction)
        {
            if (windup > 0f) yield return new WaitForSeconds(windup);

            // Si ha muerto durante el windup, el disparo se cancela.
            if (!IsDead) SpawnProjectile(direction);

            _firing = false;
        }

        private void SpawnProjectile(Vector2 direction)
        {
            Vector3 origin = MuzzlePosition(direction.x < 0f ? -1 : 1);

            // Vía PrefabPool, no Instantiate: esto dispara con cooldown, así que a lo largo de una
            // pelea son decenas de instancias — exactamente lo que la regla del proyecto de "nunca
            // Instantiate/Destroy lo que se repite" pide poolear.
            var instance = PrefabPool.Spawn(projectilePrefab, origin, Quaternion.identity);
            var projectile = instance != null ? instance.GetComponent<Projectile>() : null;

            if (projectile != null)
            {
                projectile.PooledPrefab = true;
                projectile.Configure(damage, projectileSpeed, projectileHitLayers);
                projectile.Launch(direction, gameObject);
            }
            else
            {
                Debug.LogWarning($"[RangedAttack] El prefab '{projectilePrefab.name}' no tiene componente Projectile.", this);
                if (instance != null) PrefabPool.Despawn(instance);
            }

            VfxOneShot.Spawn(muzzleEffect, origin, direction.x < 0f ? -1 : 1);
        }

        private Vector3 MuzzlePosition(int facing) =>
            transform.position + new Vector3(muzzleOffset.x * facing, muzzleOffset.y, 0f);

        // ------------------------------------------------------------------ IA

        private void UpdateAutoDetect()
        {
            if (!CanFire) return;

            var target = FindTarget();
            if (target == null) return;

            Vector2 toTarget = target.position - transform.position;
            if (Mathf.Abs(toTarget.y) > verticalTolerance) return;

            int dirToTarget = toTarget.x < 0f ? -1 : 1;

            if (turnToFaceTarget) SetFacing(dirToTarget);
            else if (onlyFireWhenFacing && dirToTarget != ResolveFacing()) return;

            TryFire(new Vector2(dirToTarget, 0f));
        }

        private Transform FindTarget()
        {
            // Con etiqueta es lo más barato y suficiente para un enemigo de patrulla.
            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                var tagged = GameObject.FindGameObjectWithTag(targetTag);
                if (tagged == null) return null;

                var targetHealth = tagged.GetComponentInParent<Health>();
                if (targetHealth != null && targetHealth.IsDead) return null;

                return Vector2.Distance(tagged.transform.position, transform.position) <= detectionRange
                    ? tagged.transform
                    : null;
            }

            var hit = Physics2D.OverlapCircle(transform.position, detectionRange, projectileHitLayers);
            if (hit == null) return null;

            var health = hit.GetComponentInParent<Health>();
            if (health == null || health == _health || health.IsDead) return null;

            return health.transform;
        }

        // ------------------------------------------------------------------ orientación

        private int ResolveFacing()
        {
            if (_movement != null) return _movement.Facing;
            if (_sprite != null) return _sprite.flipX ? -1 : 1;
            return 1;
        }

        private void SetFacing(int facing)
        {
            if (_movement != null) return;              // el jugador se orienta él solo
            if (_sprite != null) _sprite.flipX = facing < 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            if (mode == TriggerMode.AutoDetect)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, detectionRange);
            }

            int facing = Application.isPlaying ? ResolveFacing() : 1;
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(MuzzlePosition(facing), 0.12f);
        }
    }
}
