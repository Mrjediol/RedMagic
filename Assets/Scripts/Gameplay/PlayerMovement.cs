using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Movimiento 2D de scroll lateral (correr + saltar) con tres fuentes de entrada simultáneas:
    ///  - Teclado: A/D o flechas para moverse, Espacio para saltar.
    ///  - Mando: stick izquierdo / cruceta, botón Sur para saltar.
    ///  - Táctil: botones en pantalla, que escriben en <see cref="TouchInput"/>.
    ///
    /// Teclado y mando llegan por el asset de Input Actions (cambio automático según el último
    /// dispositivo usado, porque la acción escucha todas las rutas a la vez).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 13f;
        [Tooltip("Multiplicador de gravedad al caer, para que el salto no se sienta flotante.")]
        [SerializeField] private float fallGravityMultiplier = 1.9f;
        [Tooltip("Margen tras salirse de una plataforma en el que aún se puede saltar.")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("Margen para registrar un salto pulsado justo antes de tocar el suelo.")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Detección de suelo")]
        [Tooltip("Punto desde el que se comprueba el suelo. Si se deja vacío se usa la base del collider.")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.14f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";

        [Header("SFX — ids de SoundData del AudioManager")]
        [SerializeField] private string jumpSfxId = "SFX_PlayerJump";
        [Tooltip("Pasos al correr por el suelo. Déjalo vacío para desactivarlos.")]
        [SerializeField] private string footstepSfxId = "SFX_PlayerFootstep";
        [SerializeField] private float footstepInterval = 0.32f;

        private Rigidbody2D _body;
        private SpriteRenderer _sprite;
        private Collider2D _collider;
        private Health _health;

        private InputAction _moveAction;
        private InputAction _jumpAction;

        private float _horizontal;
        private bool _grounded;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _footstepTimer;
        private bool _controlEnabled = true;

        private readonly List<Collider2D> _groundHits = new List<Collider2D>();
        private ContactFilter2D _groundFilter;

        public bool IsGrounded => _grounded;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _health = GetComponent<Health>();

            _body.freezeRotation = true;

            _groundFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayers,
                useTriggers = false
            };

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                {
                    _moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
                    _jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);
                }
                else
                {
                    Debug.LogWarning($"[PlayerMovement] No existe el action map '{actionMapName}' en {inputActions.name}.", this);
                }
            }
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _jumpAction?.Enable();

            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();

            if (_health != null) _health.Died -= OnDied;
        }

        private void Update()
        {
            // Con un menú abierto el juego está en pausa: ni se lee input ni se acumulan saltos.
            if (!_controlEnabled || !GameStateManager.CanPlayerAct)
            {
                _horizontal = 0f;
                TouchInput.ConsumeJump();
                return;
            }

            ReadInput();
            UpdateTimers();
            UpdateFootsteps();
        }

        private void FixedUpdate()
        {
            _grounded = CheckGrounded();
            if (_grounded) _coyoteTimer = coyoteTime;

            ApplyHorizontalMovement();
            TryJump();
            ApplyFallGravity();
        }

        // ------------------------------------------------------------------ input

        private void ReadInput()
        {
            // Teclado + mando desde el asset de acciones…
            float axis = _moveAction != null ? _moveAction.ReadValue<Vector2>().x : 0f;

            // …más los botones táctiles en pantalla. Se queda con el de mayor magnitud
            // para que las tres fuentes puedan usarse indistintamente.
            float touch = TouchInput.Horizontal;
            _horizontal = Mathf.Abs(touch) > Mathf.Abs(axis) ? touch : axis;
            _horizontal = Mathf.Clamp(_horizontal, -1f, 1f);

            bool jumpPressed = (_jumpAction != null && _jumpAction.WasPressedThisFrame()) || TouchInput.ConsumeJump();
            if (jumpPressed) _jumpBufferTimer = jumpBufferTime;

            if (_sprite != null && Mathf.Abs(_horizontal) > 0.01f)
                _sprite.flipX = _horizontal < 0f;
        }

        private void UpdateTimers()
        {
            _coyoteTimer -= Time.deltaTime;
            _jumpBufferTimer -= Time.deltaTime;
        }

        private void UpdateFootsteps()
        {
            if (string.IsNullOrWhiteSpace(footstepSfxId)) return;

            if (!_grounded || Mathf.Abs(_horizontal) < 0.1f)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer > 0f) return;

            _footstepTimer = footstepInterval;
            PlaySfx(footstepSfxId);
        }

        // ------------------------------------------------------------------ física

        private void ApplyHorizontalMovement()
        {
            var velocity = _body.linearVelocity;
            velocity.x = _horizontal * moveSpeed;
            _body.linearVelocity = velocity;
        }

        private void TryJump()
        {
            if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f) return;

            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;

            var velocity = _body.linearVelocity;
            velocity.y = jumpForce;
            _body.linearVelocity = velocity;

            PlaySfx(jumpSfxId);
        }

        private void ApplyFallGravity()
        {
            if (_body.linearVelocity.y >= 0f) return;

            _body.linearVelocity += Vector2.up *
                (Physics2D.gravity.y * _body.gravityScale * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        private bool CheckGrounded()
        {
            Vector2 origin;

            if (groundCheck != null)
            {
                origin = groundCheck.position;
            }
            else if (_collider != null)
            {
                var bounds = _collider.bounds;
                origin = new Vector2(bounds.center.x, bounds.min.y);
            }
            else
            {
                origin = transform.position;
            }

            _groundHits.Clear();
            Physics2D.OverlapCircle(origin, groundCheckRadius, _groundFilter, _groundHits);

            foreach (var hit in _groundHits)
            {
                if (hit == null) continue;
                // Ignorar los colliders del propio jugador.
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ varios

        private void OnDied()
        {
            _controlEnabled = false;
            _horizontal = 0f;
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        private static void PlaySfx(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(id);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = groundCheck != null
                ? groundCheck.position
                : transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
        }
    }
}
