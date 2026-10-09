using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Lo que el dash hace en combate, aparte de mover (eso es <see cref="PlayerMovement"/>):
    /// <list type="bullet">
    /// <item><b>Esquiva</b>: i-frames sólo al arrancar (<see cref="startInvulnerability"/>). Recibir daño
    /// en el resto del dash lo corta (<see cref="PlayerMovement.CancelDash"/>), salvo "dash imparable".</item>
    /// <item><b>Atravesar</b>: el jugador sólo choca con el terreno, así que el dash pasa a través de
    /// los enemigos (pueden golpearle por contacto al cruzarlos).</item>
    /// <item><b>Daño</b> (mejora de build): mientras dura el dash, cada enemigo que toque el cuerpo
    /// recibe <see cref="PlayerStats.DashDamage"/> una vez por dash. Sin items, 0 = no hace daño.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerDashCombat : MonoBehaviour
    {
        [Header("Esquiva")]
        [Tooltip("I-frames sólo justo al empezar el dash (segundos). El resto del dash se puede recibir daño, y recibirlo corta el dash (salvo item \"Dash imparable\").")]
        [SerializeField, Min(0f)] private float startInvulnerability = 0.06f;
        [Tooltip("Durante el dash se ignora el choque físico con enemigos a esta distancia por delante/detrás (no los empuja, los atraviesa).")]
        [SerializeField, Min(0f)] private float passThroughLookAhead = 1.2f;

        [Header("Daño (mejora)")]
        [Tooltip("Margen alrededor del collider del jugador para el golpe del dash.")]
        [SerializeField] private float hitPadding = 0.25f;
        [Tooltip("Daño de dash para probar sin items (se suma al de los items). En juego, 0.")]
        [SerializeField, Min(0f)] private float testDamage;
        [SerializeField] private LayerMask hitLayers = ~0;
        [Tooltip("Impacto al atravesar a un enemigo (prefab con ImpactBurstFx en modo enemigo).")]
        [SerializeField] private GameObject hitEffect;

        private PlayerMovement _movement;
        private Health _health;
        private Collider2D _body;
        private ContactFilter2D _filter;
        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private readonly HashSet<Health> _alreadyHit = new HashSet<Health>();
        private int _dashDirection = 1;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<Health>();
            _body = GetComponent<Collider2D>();
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
            _solidFilter = new ContactFilter2D { useTriggers = false };
            GetComponentsInChildren(true, _ownColliders);
        }

        private void OnEnable()
        {
            _movement.Dashed -= OnDashed;
            _movement.Dashed += OnDashed;
            if (_health != null) { _health.Damaged -= OnDamaged; _health.Damaged += OnDamaged; }
            _alreadyHit.Clear();
        }

        private void OnDisable()
        {
            _movement.Dashed -= OnDashed;
            if (_health != null) _health.Damaged -= OnDamaged;
            RestoreAllCollisions();
        }

        private void OnDamaged(float amount) => _movement.CancelDash();

        private void OnDashed(int direction)
        {
            _dashDirection = direction;
            _alreadyHit.Clear();
            if (_health == null) return;
            float iframes = startInvulnerability;
            _health.StartInvulnerability(iframes);
        }

        private void FixedUpdate()
        {
            UpdatePassThrough();
            if (!_movement.IsDashing || _body == null) return;
            float damage = PlayerStats.DashDamage + testDamage;
            if (damage <= 0f) return;

            var b = _body.bounds;
            _hits.Clear();
            Physics2D.OverlapBox(b.center, (Vector2)b.size + Vector2.one * hitPadding * 2f, 0f, _filter, _hits);
            foreach (var hit in _hits)
            {
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                var target = hit.GetComponentInParent<Health>();
                if (target == null || target == _health || target.IsDead || !_alreadyHit.Add(target)) continue;
                if (!Items.PlayerHit.Deal(target, damage, transform.position, 0f, Items.HitKind.Melee)) continue;

                SpawnHitEffect(hitEffect, hit.ClosestPoint(transform.position), new Vector2(_dashDirection, 0f));
                var knockback = target.GetComponent<Knockback>();
                if (knockback != null) knockback.Apply(_dashDirection, 0.1f);
            }
        }


        // ------------------------------------------------------------------ atravesar enemigos

        // El jugador es cinemático y los enemigos dinámicos: al cruzarlos los empujaría. Durante el
        // dash se ignora la colisión física con los enemigos cercanos y se devuelve cuando ya no se
        // solapan (si se devolviera dentro, el motor los separaría de golpe).
        private readonly List<Collider2D> _ignored = new List<Collider2D>();
        private readonly List<Collider2D> _ownColliders = new List<Collider2D>();
        private readonly List<Collider2D> _near = new List<Collider2D>();
        private ContactFilter2D _solidFilter;

        private void UpdatePassThrough()
        {
            if (_body == null) return;
            var b = _body.bounds;

            if (_movement.IsDashing)
            {
                var size = (Vector2)b.size + new Vector2(passThroughLookAhead * 2f, 0.4f);
                _near.Clear();
                Physics2D.OverlapBox(b.center, size, 0f, _solidFilter, _near);
                foreach (var col in _near)
                {
                    if (col == null || col.isTrigger || _ignored.Contains(col)) continue;
                    if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                    var h = col.GetComponentInParent<Health>();
                    if (h == null || h == _health) continue;
                    SetIgnored(col, true);
                    _ignored.Add(col);
                }
            }

            for (int i = _ignored.Count - 1; i >= 0; i--)
            {
                var col = _ignored[i];
                bool gone = col == null || !col.enabled || !col.gameObject.activeInHierarchy;
                if (!gone && (_movement.IsDashing || col.bounds.Intersects(b))) continue;
                if (!gone) SetIgnored(col, false);
                _ignored.RemoveAt(i);
            }
        }

        private void SetIgnored(Collider2D other, bool ignore)
        {
            foreach (var own in _ownColliders)
                if (own != null && !own.isTrigger) Physics2D.IgnoreCollision(own, other, ignore);
        }

        private void RestoreAllCollisions()
        {
            foreach (var col in _ignored) if (col != null) SetIgnored(col, false);
            _ignored.Clear();
        }
        /// <summary>Impacto visual en un enemigo golpeado (dash o picado). Null-safe.</summary>
        public static void SpawnHitEffect(GameObject prefab, Vector2 point, Vector2 direction)
        {
            if (prefab == null) return;
            var go = Core.PrefabPool.Spawn(prefab, point, Quaternion.identity);
            var fx = go.GetComponent<Fx.ImpactBurstFx>();
            if (fx != null) fx.Play(point, direction);
        }
    }
}
