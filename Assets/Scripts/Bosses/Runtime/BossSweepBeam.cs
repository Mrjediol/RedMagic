using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un brazo largo anclado al jefe que <b>gira</b> barriendo la arena, como una guadaña.
    ///
    /// Es la pareja de <see cref="BossShockwave"/> y a propósito se esquiva de otra manera. La onda
    /// es una franja que <i>viaja</i> en horizontal: se lee mirando la altura. Esto es una recta
    /// que <i>pivota</i>, así que su altura depende de <b>lo lejos que estés</b>: cerca del jefe
    /// baja de golpe y hay que estar fuera o ya en el aire; lejos tarda, pero cuando llega pasa
    /// deprisa. La misma pasada pide cosas distintas según dónde te haya pillado, y eso es lo que
    /// obliga a decidir la distancia antes de que empiece a girar.
    ///
    /// Golpea <b>una sola vez por pasada</b> (igual que la onda): rozar la guadaña es un golpe, no
    /// un daño por tick, así que quedarse pegado al borde no funde la barra de vida.
    ///
    /// Va <b>pooled</b> (<see cref="Pool{T}"/>): un combate largo lanza decenas de pasadas.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossSweepBeam : MonoBehaviour, IPooled
    {
        private static Pool<BossSweepBeam> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private SpriteRenderer _renderer;

        private AbilityContext _ctx;
        private Vector2 _pivot;
        private float _fromAngle;
        private float _toAngle;
        private float _duration;
        private float _elapsed;
        private float _length;
        private float _width;
        private float _damage;
        private float _knockbackMultiplier;
        private Color _tint;
        private bool _finished;

        /// <summary>Objetivos ya golpeados por esta pasada: una pasada es un golpe.</summary>
        private readonly HashSet<Health> _hit = new HashSet<Health>();

        /// <summary>
        /// Lanza una pasada que gira desde <paramref name="fromAngle"/> hasta
        /// <paramref name="toAngle"/> (grados, 0 = hacia la derecha, 90 = hacia arriba) alrededor
        /// de <paramref name="pivot"/>.
        /// </summary>
        public static BossSweepBeam Spawn(in BossContext ctx, Vector2 pivot, float fromAngle, float toAngle,
                                          float seconds, float length, float width, float damage,
                                          float knockbackMultiplier)
        {
            _pool ??= new Pool<BossSweepBeam>(Build, prewarm: 4);

            var beam = _pool.Get();
            beam.Begin(ctx, pivot, fromAngle, toAngle, seconds, length, width, damage, knockbackMultiplier);
            return beam;
        }

        private static BossSweepBeam Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Sweep Beam", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);
            return go.AddComponent<BossSweepBeam>();
        }

        private void Begin(in BossContext ctx, Vector2 pivot, float fromAngle, float toAngle,
                           float seconds, float length, float width, float damage,
                           float knockbackMultiplier)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            _ctx = ctx.Ability;
            _pivot = pivot;
            _fromAngle = fromAngle;
            _toAngle = toAngle;
            _duration = Mathf.Max(0.05f, seconds);
            _elapsed = 0f;
            _length = Mathf.Max(1f, length);
            _width = Mathf.Max(0.2f, width);
            _damage = Mathf.Max(0f, damage);
            _knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            _tint = ctx.Accent;
            _finished = false;

            _hit.Clear();

            _renderer.sprite = ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
            AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
            AbilityFx.Resize(transform, _renderer, new Vector2(_length, _width));

            Place(_fromAngle, 0f);
        }

        void IPooled.OnReturnedToPool() => _hit.Clear();

        /// <summary>
        /// El giro va en <c>Update</c> y no en <c>FixedUpdate</c>: a 50 Hz una pasada rápida se
        /// vería a saltos, y esto es sobre todo un aviso visual. El daño sí se resuelve al ritmo
        /// de la física, sobre la posición que ya se está viendo.
        /// </summary>
        private void Update()
        {
            if (_finished) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            Place(Mathf.Lerp(_fromAngle, _toAngle, t), Envelope(t));

            if (t >= 1f) Finish();
        }

        private void FixedUpdate()
        {
            if (_finished) return;

            float angle = transform.eulerAngles.z;
            var targets = AbilityHit.OverlapBox(_ctx, transform.position, new Vector2(_length, _width), angle);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!_hit.Add(target)) continue;

                AbilityHit.Damage(target, _ctx, _damage, _pivot, _knockbackMultiplier);
            }
        }

        /// <summary>Coloca la recta: nace en el pivote y se extiende hacia <paramref name="angle"/>.</summary>
        private void Place(float angle, float alpha)
        {
            float radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            transform.position = _pivot + direction * (_length * 0.5f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (_renderer == null) return;

            var color = _tint;
            color.a = _tint.a * alpha;
            _renderer.color = color;
        }

        /// <summary>
        /// Entra deprisa y se apaga al final: el filo tiene que verse ya girando desde el primer
        /// fotograma (si apareciera a plena opacidad de golpe no se distinguiría del aviso), y
        /// desvanecerse deja claro que la pasada ha terminado y se puede volver a pisar ahí.
        /// </summary>
        private static float Envelope(float t)
        {
            const float fadeIn = 0.12f;
            const float fadeOut = 0.22f;

            if (t < fadeIn) return t / fadeIn;
            if (t > 1f - fadeOut) return (1f - t) / fadeOut;
            return 1f;
        }

        private void Finish()
        {
            _finished = true;
            _pool?.Release(this);
        }

        private void OnDrawGizmosSelected()
        {
            // Sin la escala del transform: el tamaño real de la caja de golpeo son _length/_width,
            // y localToWorldMatrix la aplicaría dos veces.
            Gizmos.color = new Color(1f, 0.4f, 0.5f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_length, _width, 0f));
        }
    }
}
