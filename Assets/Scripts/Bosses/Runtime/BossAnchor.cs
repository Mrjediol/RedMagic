using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un ancla del ritual: un objetivo pequeño y rompible que el jefe planta en el suelo y que
    /// hay que destruir <b>antes de que se acabe el tiempo</b>.
    ///
    /// Existe para hacer la única pregunta que el resto del combate no hace: <b>¿a qué le pego?</b>
    /// Mientras haya anclas en pie el jefe no recibe daño, así que todo el daño que se le meta a él
    /// es daño tirado; y como el ritual tiene cuenta atrás, tampoco vale repartir. Es un problema
    /// de reparto de recursos metido dentro de un combate de reflejos.
    ///
    /// Lleva un <see cref="Health"/> normal y corriente, así que cualquier arma, habilidad o
    /// elemento del juego la rompe sin que nada tenga que saber qué es un ancla. Va etiquetada
    /// como el jefe para que los proyectiles de éste no se las lleven por delante.
    ///
    /// Va <b>pooled</b>: un combate largo planta docenas.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossAnchor : MonoBehaviour, IPooled
    {
        private static Pool<BossAnchor> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private SpriteRenderer _renderer;
        private BoxCollider2D _collider;
        private Health _health;

        private Color _tint;
        private bool _released;

        /// <summary>La vida del ancla, para que el ataque sepa cuándo ha caído.</summary>
        public Health Health => _health;

        /// <summary>True mientras siga en pie.</summary>
        public bool IsStanding => !_released && _health != null && !_health.IsDead;

        public static BossAnchor Spawn(in BossContext ctx, Vector2 position, Vector2 size,
                                       float maxHealth, Sprite sprite)
        {
            _pool ??= new Pool<BossAnchor>(Build, prewarm: 4);

            var anchor = _pool.Get();
            anchor.Begin(ctx, position, size, maxHealth, sprite);
            return anchor;
        }

        private static BossAnchor Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Anchor", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            var health = go.AddComponent<Health>();
            go.AddComponent<HitFlash>();

            var anchor = go.AddComponent<BossAnchor>();
            anchor._collider = collider;
            anchor._health = health;
            return anchor;
        }

        private void Begin(in BossContext ctx, Vector2 position, Vector2 size, float maxHealth,
                           Sprite sprite)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            _tint = ctx.Accent;
            _released = false;

            transform.position = position;

            _renderer.sprite = sprite != null ? sprite
                             : ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
            _renderer.color = _tint;
            AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
            AbilityFx.Resize(transform, _renderer, size);

            var scale = transform.localScale;
            _collider.size = new Vector2(size.x / Mathf.Max(0.0001f, scale.x),
                                         size.y / Mathf.Max(0.0001f, scale.y));
            _collider.enabled = true;

            // La etiqueta del jefe: sus propios ataques no deben romperle el ritual.
            if (ctx.Ability.Caster != null) TryCopyTag(ctx.Ability.Caster);

            _health.SetMaxHealth(maxHealth, healToFull: true);
            _health.ResetHealth();
        }

        void IPooled.OnReturnedToPool()
        {
            if (_collider != null) _collider.enabled = false;
        }

        private void Update()
        {
            if (_released) return;

            if (_health != null && _health.IsDead) { Release(true); return; }

            // Late despacio: un objetivo que respira se distingue del decorado del escenario.
            if (_renderer == null) return;

            var color = _tint;
            color.a = _tint.a * (0.72f + 0.28f * Mathf.Sin(Time.time * 4f));
            _renderer.color = color;
        }

        /// <summary>Retira el ancla. <paramref name="broken"/> distingue romperla de que expire.</summary>
        public void Release(bool broken)
        {
            if (_released) return;
            _released = true;

            if (_collider != null) _collider.enabled = false;

            AbilityFx.Flash(_renderer != null ? _renderer.sprite : null, transform.position,
                            Vector2.one * (broken ? 2.6f : 1.8f),
                            new Color(_tint.r, _tint.g, _tint.b, broken ? 0.9f : 0.5f),
                            broken ? 0.4f : 0.25f, 0f, broken ? 2.2f : 1.4f, gameObject);

            _pool?.Release(this);
        }

        private void TryCopyTag(GameObject reference)
        {
            try
            {
                gameObject.tag = reference.tag;
            }
            catch (UnityException)
            {
                // Etiqueta inexistente: el ancla se queda como está y, como mucho, el jefe puede
                // romper su propio ritual. No merece reventar el ataque.
            }
        }
    }
}
