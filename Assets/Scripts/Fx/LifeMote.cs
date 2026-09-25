using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>Una mota de <see cref="LifeMotes"/>. Se mueve sola y vuelve al pool al acabar.</summary>
    public sealed class LifeMote : MonoBehaviour, Core.IPooled
    {
        // Red de seguridad: una mota roja que no ha llegado en este tiempo se da por llegada (cura).
        private const float DrainMaxLifetime = 2.5f;
        private const float HealLifetime = 0.75f;

        internal SpriteRenderer Renderer;

        private bool _drain;
        private Transform _target;
        private Vector2 _velocity;
        private Vector2 _offset;
        private float _age;
        private float _delay;
        private Color _color;

        // Curación que lleva esta mota roja (se aplica al LLEGAR al jugador). null en las verdes.
        private DrainPacket _packet;

        internal void BeginDrain(Vector2 from, Transform target, Vector2 burst, Color color, DrainPacket packet)
        {
            _drain = true;
            _packet = packet;
            _target = target;
            _velocity = burst;
            _age = 0f;
            _delay = 0f;
            _color = color;
            transform.position = from;
            transform.localScale = Vector3.one * Random.Range(0.9f, 1.2f);
            Renderer.color = color;
        }

        internal void BeginHeal(Transform target, Vector2 offset, float delay, Color color)
        {
            _drain = false;
            _packet = null;
            _target = target;
            _offset = offset;
            _age = 0f;
            _delay = delay;
            _color = color;
            transform.position = LifeMotes.BodyCenter(target) + offset;
            transform.localScale = Vector3.zero;
            Renderer.color = color;
        }

        private void Update()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                LifeMotes.Release(this);
                return;
            }

            float dt = Time.deltaTime;
            _age += dt;

            if (_drain) TickDrain(dt);
            else TickHeal();
        }

        /// <summary>
        /// Vuelta al pool. Si aún lleva curación es que se ha perdido sin llegar (el jugador
        /// desapareció, carga de escena): esa parte no cura.
        /// </summary>
        void Core.IPooled.OnReturnedToPool()
        {
            var packet = _packet;
            _packet = null;
            packet?.Lost();
        }

        private void TickDrain(float dt)
        {
            Vector2 pos = transform.position;
            Vector2 goal = LifeMotes.BodyCenter(_target);
            Vector2 to = goal - pos;
            float distance = to.magnitude;

            if (distance < 0.3f || _age >= DrainMaxLifetime)
            {
                // Ha llegado: AQUÍ entra la vida, no al matar.
                var packet = _packet;
                _packet = null;
                packet?.Arrive();
                LifeMotes.Release(this);
                return;
            }

            // Primero se abre en abanico (el estallido), luego lo atrae cada vez más fuerte: sin
            // esa rampa las motas irían en línea recta y no se leería que salen DEL enemigo.
            float pull = Mathf.Lerp(4f, 40f, Mathf.Clamp01(_age / 0.6f));
            float maxSpeed = Mathf.Lerp(3f, 16f, Mathf.Clamp01(_age / 0.8f));
            _velocity = Vector2.MoveTowards(_velocity, to / Mathf.Max(0.001f, distance) * maxSpeed, pull * dt);

            transform.position = pos + _velocity * dt;

            // Se encoge al llegar: se "funde" en el jugador en vez de chocar.
            float shrink = Mathf.Clamp01(distance / 1.2f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, shrink);
        }

        private void TickHeal()
        {
            float t = (_age - _delay) / HealLifetime;
            if (t < 0f) return;
            if (t >= 1f)
            {
                LifeMotes.Release(this);
                return;
            }

            // Sigue al jugador y sube; aparece de golpe y se desvanece.
            transform.position = LifeMotes.BodyCenter(_target) + _offset + Vector2.up * (t * 1.1f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.8f, t) * Mathf.Clamp01(t * 6f);

            var c = _color;
            c.a = 1f - t * t;
            Renderer.color = c;
        }
    }
}
