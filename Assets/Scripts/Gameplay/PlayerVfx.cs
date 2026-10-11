using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Engancha los efectos visuales del jugador a los eventos de <see cref="PlayerMovement"/>.
    /// Se mantiene aparte para que el controlador de movimiento no tenga que conocer prefabs.
    ///
    /// Cada efecto puede ser de dos tipos, según el prefab asignado:
    /// <list type="bullet">
    /// <item><see cref="MeleeSlashFx"/> — mismo set visual que los tajos melee (variantes en
    /// <c>Assets/Prefabs/Fx/Player/Movement/</c>, <b>Tools ▸ RedMagic ▸ FX ▸ Movimiento · Generar
    /// variantes</b>). Se espeja según hacia dónde mira el jugador; el del dash le sigue mientras dura.</item>
    /// <item><see cref="DashTrailFx"/> (sólo dash): salida + cápsula de luz + onda de choque; termina al acabar el dash.</item>
    /// <item>Partículas pooled (<see cref="VfxOneShot"/>), los antiguos <c>VFX_Dash/Jump/DoubleJump</c>.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerVfx : MonoBehaviour
    {
        [Header("Dash")]
        [Tooltip("Estela del dash. Sigue al jugador mientras dura el dash.")]
        [SerializeField] private GameObject dashEffect;
        [Tooltip("Posición relativa al jugador. La X se invierte según la dirección del dash.")]
        [SerializeField] private Vector2 dashEffectOffset = new Vector2(-0.2f, -0.1f);
        [Tooltip("Tamaño del efecto (1 = el del prefab). Sólo efectos del set de tajos (MeleeSlashFx).")]
        [SerializeField, Min(0.05f)] private float dashEffectSize = 1f;

        [Header("Salto")]
        [Tooltip("Efecto en los pies al despegar del suelo.")]
        [SerializeField] private GameObject jumpEffect;
        [Tooltip("Por defecto en la base del collider (-0.75), es decir a la altura de los pies.")]
        [SerializeField] private Vector2 jumpEffectOffset = new Vector2(0f, -0.72f);
        [Tooltip("Tamaño del efecto (1 = el del prefab). Sólo efectos del set de tajos (MeleeSlashFx).")]
        [SerializeField, Min(0.05f)] private float jumpEffectSize = 1f;

        [Header("Doble salto")]
        [Tooltip("Efecto bajo el jugador al saltar en el aire.")]
        [SerializeField] private GameObject doubleJumpEffect;
        [Tooltip("Por defecto en la base del collider (-0.75), es decir a la altura de los pies.")]
        [SerializeField] private Vector2 doubleJumpEffectOffset = new Vector2(0f, -0.72f);
        [Tooltip("Tamaño del efecto (1 = el del prefab). Sólo efectos del set de tajos (MeleeSlashFx).")]
        [SerializeField, Min(0.05f)] private float doubleJumpEffectSize = 1f;

        private PlayerMovement _movement;
        private DashTrailFx _dashTrail;
        private MeleeSlashFx _dashSlash;
        private Vector2 _dashSlashOffset;
        private float _dashFollowLeft;

        private void Awake() => _movement = GetComponent<PlayerMovement>();

        private void OnEnable()
        {
            _movement.Dashed += OnDashed;
            _movement.Jumped += OnJumped;
            _movement.AirJumped += OnAirJumped;
            _dashSlash = null;
        }

        private void OnDisable()
        {
            _movement.Dashed -= OnDashed;
            _movement.Jumped -= OnJumped;
            _movement.AirJumped -= OnAirJumped;
        }

        // Los efectos y sus offsets siguen la escala del jugador.
        private float Scale => Mathf.Abs(transform.lossyScale.y);

        private void LateUpdate()
        {
            // El viaje del dash termina cuando PlayerMovement deja de estar en dash.
            if (_dashTrail != null && !_movement.IsDashing) { _dashTrail.End(); _dashTrail = null; }
            FollowDashSlash();
        }

        private void FollowDashSlash()
        {
            if (_dashSlash == null) return;
            _dashFollowLeft -= Time.deltaTime;
            if (_dashFollowLeft <= 0f || !_dashSlash.isActiveAndEnabled) { _dashSlash = null; return; }
            _dashSlash.transform.position = transform.position + (Vector3)_dashSlashOffset;
        }

        private void OnDashed(int direction)
        {
            var offset = new Vector2(dashEffectOffset.x * direction, dashEffectOffset.y) * Scale;
            if (dashEffect != null && dashEffect.GetComponent<DashTrailFx>() != null)
            {
                if (_dashTrail != null) _dashTrail.End();
                var trailGo = PrefabPool.Spawn(dashEffect, transform.position, Quaternion.identity);
                _dashTrail = trailGo.GetComponent<DashTrailFx>();
                _dashTrail.Begin(transform, offset, new Vector2(direction, 0f), dashEffectSize);
                return;
            }
            var slash = SpawnSlash(dashEffect, offset, direction, dashEffectSize);
            if (slash != null)
            {
                _dashSlash = slash;
                _dashSlashOffset = offset;
                _dashFollowLeft = _movement.DashDuration;
                return;
            }
            VfxOneShot.SpawnFollowing(dashEffect, transform, dashEffectOffset * Scale, direction, _movement.DashDuration, Scale);
        }

        private void OnJumped() => SpawnAtFeet(jumpEffect, jumpEffectOffset, jumpEffectSize);

        private void OnAirJumped() => SpawnAtFeet(doubleJumpEffect, doubleJumpEffectOffset, doubleJumpEffectSize);

        private void SpawnAtFeet(GameObject prefab, Vector2 offset, float size)
        {
            if (SpawnSlash(prefab, offset * Scale, _movement.Facing, size) != null) return;
            VfxOneShot.Spawn(prefab, transform.position + (Vector3)(offset * Scale), _movement.Facing, null, Scale);
        }

        /// <summary>Si el prefab es un <see cref="MeleeSlashFx"/>, lo saca del pool y lo lanza; si no, null.</summary>
        private MeleeSlashFx SpawnSlash(GameObject prefab, Vector2 offset, int facing, float size)
        {
            if (prefab == null || prefab.GetComponent<MeleeSlashFx>() == null) return null;
            var go = PrefabPool.Spawn(prefab, transform.position + (Vector3)offset, Quaternion.identity);
            var slash = go.GetComponent<MeleeSlashFx>();
            slash.Play(Scale * size, facing < 0, false);
            return slash;
        }
    }
}
