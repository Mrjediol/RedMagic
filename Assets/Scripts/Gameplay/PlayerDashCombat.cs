using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Lo que el dash hace en combate, aparte de mover (eso es <see cref="PlayerMovement"/>):
    /// <list type="bullet">
    /// <item><b>Esquiva</b>: al empezar el dash, i-frames durante la misma ventana que el parry
    /// (<see cref="PlayerParry.Window"/>), así un dash a tiempo atraviesa un golpe.</item>
    /// <item><b>Daño</b> (mejora de build): mientras dura el dash, cada enemigo que toque el cuerpo
    /// recibe <see cref="PlayerStats.DashDamage"/> una vez por dash. Sin items, 0 = no hace daño.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerDashCombat : MonoBehaviour
    {
        [Header("Esquiva")]
        [Tooltip("I-frames al empezar el dash = la ventana del parry. Si no hay PlayerParry, se usa Fallback.")]
        [SerializeField] private bool matchParryWindow = true;
        [Tooltip("I-frames si no se copia la ventana del parry (o no hay PlayerParry).")]
        [SerializeField] private float fallbackInvulnerability = 0.12f;

        [Header("Daño (mejora)")]
        [Tooltip("Margen alrededor del collider del jugador para el golpe del dash.")]
        [SerializeField] private float hitPadding = 0.25f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private PlayerMovement _movement;
        private PlayerParry _parry;
        private Health _health;
        private Collider2D _body;
        private ContactFilter2D _filter;
        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private readonly HashSet<Health> _alreadyHit = new HashSet<Health>();
        private int _dashDirection = 1;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _parry = GetComponent<PlayerParry>();
            _health = GetComponent<Health>();
            _body = GetComponent<Collider2D>();
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
        }

        private void OnEnable()
        {
            _movement.Dashed -= OnDashed;
            _movement.Dashed += OnDashed;
            _alreadyHit.Clear();
        }

        private void OnDisable() => _movement.Dashed -= OnDashed;

        private void OnDashed(int direction)
        {
            _dashDirection = direction;
            _alreadyHit.Clear();
            if (_health == null) return;
            float iframes = matchParryWindow && _parry != null ? _parry.Window : fallbackInvulnerability;
            _health.StartInvulnerability(iframes);
        }

        private void FixedUpdate()
        {
            if (!_movement.IsDashing || _body == null) return;
            float damage = PlayerStats.DashDamage;
            if (damage <= 0f) return;

            var b = _body.bounds;
            _hits.Clear();
            Physics2D.OverlapBox(b.center, (Vector2)b.size + Vector2.one * hitPadding * 2f, 0f, _filter, _hits);
            foreach (var hit in _hits)
            {
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                var target = hit.GetComponentInParent<Health>();
                if (target == null || target == _health || target.IsDead || !_alreadyHit.Add(target)) continue;
                if (Items.PlayerHit.Deal(target, damage, transform.position, 0f, Items.HitKind.Melee))
                {
                    var knockback = target.GetComponent<Knockback>();
                    if (knockback != null) knockback.Apply(_dashDirection, 0.1f);
                }
            }
        }
    }
}
