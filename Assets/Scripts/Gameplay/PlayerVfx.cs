using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Engancha los efectos visuales del jugador a los eventos de <see cref="PlayerMovement"/>.
    /// Se mantiene aparte para que el controlador de movimiento no tenga que conocer prefabs.
    ///
    /// Los tres efectos son sistemas de partículas pooled (<see cref="VfxOneShot"/>), generados por
    /// <b>Tools ▸ RedMagic ▸ FX ▸ VFX del jugador · Generar</b> en <c>Assets/Prefabs/Fx/Player/</c>.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerVfx : MonoBehaviour
    {
        [Header("Dash")]
        [Tooltip("Estela + soplo del dash (VFX_Dash). Sigue al jugador mientras dura el dash; sus " +
                 "partículas quedan a lo largo del camino.")]
        [SerializeField] private GameObject dashEffect;
        [Tooltip("Posición relativa al jugador. La X se invierte según la dirección del dash.")]
        [SerializeField] private Vector2 dashEffectOffset = new Vector2(-0.2f, -0.1f);

        [Header("Salto")]
        [Tooltip("Estallido en los pies al despegar del suelo (VFX_Jump).")]
        [SerializeField] private GameObject jumpEffect;
        [Tooltip("Por defecto en la base del collider (-0.75), es decir a la altura de los pies.")]
        [SerializeField] private Vector2 jumpEffectOffset = new Vector2(0f, -0.72f);

        [Header("Doble salto")]
        [Tooltip("Anillo mágico bajo el jugador al saltar en el aire (VFX_DoubleJump).")]
        [SerializeField] private GameObject doubleJumpEffect;
        [Tooltip("Por defecto en la base del collider (-0.75), es decir a la altura de los pies.")]
        [SerializeField] private Vector2 doubleJumpEffectOffset = new Vector2(0f, -0.72f);

        private PlayerMovement _movement;

        private void Awake() => _movement = GetComponent<PlayerMovement>();

        private void OnEnable()
        {
            _movement.Dashed += OnDashed;
            _movement.Jumped += OnJumped;
            _movement.AirJumped += OnAirJumped;
        }

        private void OnDisable()
        {
            _movement.Dashed -= OnDashed;
            _movement.Jumped -= OnJumped;
            _movement.AirJumped -= OnAirJumped;
        }

        // Los efectos y sus offsets siguen la escala del jugador (PlayerScaleConfig la cambia por escena).
        private float Scale => Mathf.Abs(transform.lossyScale.y);

        private void OnDashed(int direction) =>
            VfxOneShot.SpawnFollowing(dashEffect, transform, dashEffectOffset * Scale, direction, _movement.DashDuration, Scale);

        private void OnJumped() => SpawnAtFeet(jumpEffect, jumpEffectOffset);

        private void OnAirJumped() => SpawnAtFeet(doubleJumpEffect, doubleJumpEffectOffset);

        private void SpawnAtFeet(GameObject prefab, Vector2 offset) =>
            VfxOneShot.Spawn(prefab, transform.position + (Vector3)(offset * Scale), _movement.Facing, null, Scale);
    }
}
