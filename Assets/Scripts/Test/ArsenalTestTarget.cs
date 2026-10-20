using RedMagic.Arsenal;
using RedMagic.Combat;
using RedMagic.UI;
using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Muñeco de la escena de prueba de las armas (los de <c>jugable.html</c>): <b>suelo</b> (quieto, vuelve a su sitio si lo
    /// empujan), <b>andarín</b> (va y viene entre dos puntos) o <b>volador</b> (flota a su altura). Tiene <see cref="Health"/>,
    /// <see cref="Knockback"/> y <see cref="HitFlash"/> como un enemigo de verdad, así que empujones, atracciones y
    /// congelación funcionan igual; al morir se oculta y reaparece entero a los <see cref="respawnDelay"/> s (las mejoras
    /// que dependen de matar necesitan bajas). Mientras el empujón está activo no toca su velocidad. Sale con barra de vida
    /// en el HUD como un enemigo (<see cref="IHudEnemy"/>).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Rigidbody2D))]
    public class ArsenalTestTarget : MonoBehaviour, IHudEnemy
    {
        public enum Kind { Ground, Walker, Flyer }

        [SerializeField] private Kind kind = Kind.Ground;
        [Tooltip("Andarín: distancia a cada lado de su punto de partida.")]
        [SerializeField] private float walkRange = 2.5f;
        [SerializeField] private float walkSpeed = 1f;
        [Min(0.1f)] [SerializeField] private float respawnDelay = 2.5f;

        private Health _health;
        private Rigidbody2D _rb;
        private Knockback _knockback;
        private Vector2 _home;
        private int _dir = 1;
        private float _deadFor = -1f;
        private Renderer[] _renderers;
        private Collider2D[] _colliders;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _rb = GetComponent<Rigidbody2D>();
            _knockback = GetComponent<Knockback>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _home = transform.position;
            _health.Died += OnDied;
            if (kind == Kind.Flyer) _rb.gravityScale = 0f;
        }

        private void OnDestroy() { if (_health != null) _health.Died -= OnDied; }

        private void OnDied()
        {
            _deadFor = 0f;
            Show(false);
        }

        private void Update()
        {
            if (_deadFor < 0f) return;
            _deadFor += Time.deltaTime;
            if (_deadFor < respawnDelay) return;
            _deadFor = -1f;
            transform.position = _home;
            _rb.linearVelocity = Vector2.zero;
            _health.ResetHealth();
            ElementStatus.Peek(_health)?.Clear();
            Show(true);
        }

        private void FixedUpdate()
        {
            if (_deadFor >= 0f || (_knockback != null && _knockback.IsActive)) return;
            if (ElementStatus.Disables(_health)) return;
            Vector2 p = _rb.position, v = _rb.linearVelocity;
            switch (kind)
            {
                case Kind.Ground:
                    v.x = Mathf.Lerp(v.x, (_home.x - p.x) * 2f, 0.2f);
                    break;
                case Kind.Walker:
                    if (p.x > _home.x + walkRange) _dir = -1; else if (p.x < _home.x - walkRange) _dir = 1;
                    v.x = _dir * walkSpeed;
                    break;
                case Kind.Flyer:
                    float bob = Mathf.Sin(Time.time * 2f + _home.x) * 0.15f;
                    v = new Vector2((_home.x - p.x) * 0.8f, (_home.y + bob - p.y) * 2f);
                    break;
            }
            _rb.linearVelocity = v;
        }

        private void Show(bool on)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = on;
            foreach (var c in _colliders) if (c != null) c.enabled = on;
            _rb.simulated = on;
        }
    }
}
