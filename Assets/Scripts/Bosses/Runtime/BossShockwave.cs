using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Una onda que barre la arena en horizontal dentro de una <b>franja de altura</b>. Es la pieza
    /// que hace que la verticalidad importe: la franja decide cómo se esquiva.
    ///  - Franja pegada al suelo → hay que <b>estar en el aire</b> (saltar por encima).
    ///  - Franja alta → hay que <b>estar en el suelo</b> (no saltar).
    /// El mismo componente cubre las dos porque lo único que cambia son dos números del asset.
    ///
    /// El daño se resuelve con <see cref="AbilityHit.OverlapBox"/> en vez de con un collider: la
    /// onda no necesita física propia, atraviesa el escenario y golpea a cada objetivo <b>una sola
    /// vez</b> aunque lo alcance durante varios frames.
    ///
    /// Va <b>pooled</b> (<see cref="Pool{T}"/>): un jefe lanza decenas por combate, así que ni se
    /// instancia ni se destruye — el GameObject y su SpriteRenderer se construyen una vez.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossShockwave : MonoBehaviour, IPooled
    {
        private static Pool<BossShockwave> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private SpriteRenderer _renderer;
        private FxPlaceholderStyle _style;
        private bool _pooledPrefab;
        private bool _styled = true;

        private AbilityContext _ctx;
        private int _direction = 1;
        private float _speed;
        private float _distanceLeft;
        private float _damage;
        private float _knockbackMultiplier;
        private float _width;
        private float _height;
        private float _centerY;
        private Color _tint;
        private bool _finished;

        /// <summary>Objetivos ya golpeados por esta onda: una onda es un golpe, no un daño por tick.</summary>
        private readonly HashSet<Health> _hit = new HashSet<Health>();

        /// <summary>
        /// Lanza una onda desde <paramref name="originX"/> hacia <paramref name="direction"/>
        /// (-1 / 1), ocupando la franja vertical <paramref name="bandMinY"/>..<paramref name="bandMaxY"/>
        /// en coordenadas de mundo.
        /// </summary>
        public static BossShockwave Spawn(in BossContext ctx, float originX, int direction,
                                          float bandMinY, float bandMaxY, float width, float speed,
                                          float distance, float damage, float knockbackMultiplier)
        {
            var wave = BossFxSpawn.FromPrefab<BossShockwave>(ctx.Boss != null ? ctx.Boss.ShockwavePrefab : null);
            bool fromPrefab = wave != null;

            if (wave == null)
            {
                _pool ??= new Pool<BossShockwave>(Build, prewarm: 6);
                wave = _pool.Get();
            }

            wave._pooledPrefab = fromPrefab;
            wave.Begin(ctx, originX, direction, bandMinY, bandMaxY, width, speed, distance,
                       damage, knockbackMultiplier);
            return wave;
        }

        private static BossShockwave Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Shockwave", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);
            return go.AddComponent<BossShockwave>();
        }

        private void Begin(in BossContext ctx, float originX, int direction, float bandMinY,
                           float bandMaxY, float width, float speed, float distance,
                           float damage, float knockbackMultiplier)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_style == null) _style = GetComponent<FxPlaceholderStyle>();
            _styled = !_pooledPrefab || _style != null;

            _ctx = ctx.Ability;
            _direction = direction < 0 ? -1 : 1;
            _speed = Mathf.Max(0.1f, speed);
            _distanceLeft = Mathf.Max(0.5f, distance);
            _damage = Mathf.Max(0f, damage);
            _knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            _width = Mathf.Max(0.2f, width);
            _height = Mathf.Max(0.2f, bandMaxY - bandMinY);
            _centerY = (bandMinY + bandMaxY) * 0.5f;
            _tint = _styled ? ctx.Accent : Color.white;
            _finished = false;

            _hit.Clear();

            transform.position = new Vector3(originX, _centerY, 0f);

            if (!_pooledPrefab)
            {
                _renderer.sprite = ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
                _renderer.color = _tint;
                AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
                AbilityFx.Resize(transform, _renderer, new Vector2(_width, _height));
            }
            else if (_style != null)
            {
                _style.Apply(ctx.Accent, new Vector2(_width, _height), ctx.Ability.Caster);
            }
        }

        void IPooled.OnReturnedToPool() => _hit.Clear();

        private void FixedUpdate()
        {
            if (_finished) return;

            float step = _speed * Time.fixedDeltaTime;
            _distanceLeft -= step;

            var position = transform.position;
            position.x += _direction * step;
            position.y = _centerY;
            transform.position = position;

            DamageInside(position);

            if (_distanceLeft <= 0f) Finish();
        }

        private void Update()
        {
            if (_finished || _renderer == null || !_styled) return;

            // Se apaga al final del recorrido, para que se lea que la onda se disipa.
            var color = _tint;
            color.a = _tint.a * Mathf.Clamp01(_distanceLeft * 0.35f);
            _renderer.color = color;
        }

        // PrefabPool no tiene hook por instancia: al reactivarse queda inerte hasta el próximo Begin.
        private void OnEnable() { _finished = true; _hit.Clear(); }

        private void DamageInside(Vector2 center)
        {
            var targets = AbilityHit.OverlapBox(_ctx, center, new Vector2(_width, _height));

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!_hit.Add(target)) continue;

                AbilityHit.Damage(target, _ctx, _damage, center, _knockbackMultiplier);
            }
        }

        private void Finish()
        {
            _finished = true;
            BossFxSpawn.Release(this, _pooledPrefab, _pool);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(_width, _height, 0f));
        }
    }
}
