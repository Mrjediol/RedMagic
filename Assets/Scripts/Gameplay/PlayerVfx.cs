using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Engancha los efectos visuales del jugador a los eventos de <see cref="PlayerMovement"/>.
    /// Se mantiene aparte para que el controlador de movimiento no tenga que conocer prefabs.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerVfx : MonoBehaviour
    {
        [Header("Dash")]
        [Tooltip("Estela de viento del dash (dashwind).")]
        [SerializeField] private GameObject dashEffect;
        [Tooltip("Posición relativa al jugador. La X se invierte según la dirección del dash.")]
        [SerializeField] private Vector2 dashEffectOffset = new Vector2(-0.35f, -0.25f);

        [Header("Doble salto")]
        [Tooltip("Efecto en los pies al saltar en el aire (dashwind_02).")]
        [SerializeField] private GameObject doubleJumpEffect;
        [Tooltip("Por defecto en la base del collider (-0.75), es decir a la altura de los pies.")]
        [SerializeField] private Vector2 doubleJumpEffectOffset = new Vector2(0f, -0.72f);

        private PlayerMovement _movement;

        private void Awake() => _movement = GetComponent<PlayerMovement>();

        private void OnEnable()
        {
            _movement.Dashed += OnDashed;
            _movement.AirJumped += OnAirJumped;
        }

        private void OnDisable()
        {
            _movement.Dashed -= OnDashed;
            _movement.AirJumped -= OnAirJumped;
        }

        private void OnDashed(int direction)
        {
            var position = transform.position +
                           new Vector3(dashEffectOffset.x * direction, dashEffectOffset.y, 0f);

            VfxOneShot.Spawn(dashEffect, position, direction);
        }

        private void OnAirJumped()
        {
            var position = transform.position +
                           new Vector3(doubleJumpEffectOffset.x, doubleJumpEffectOffset.y, 0f);

            VfxOneShot.Spawn(doubleJumpEffect, position, _movement.Facing);
        }
    }
}
