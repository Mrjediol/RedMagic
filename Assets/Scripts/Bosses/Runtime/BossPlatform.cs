using RedMagic.Abilities;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un saliente temporal en el que <b>se puede estar de pie</b>: nace en la capa
    /// <c>Ground</c> con un collider sólido, así que el jugador lo pisa exactamente igual que al
    /// terreno pintado, sin que <c>PlayerMovement</c> necesite saber que existe.
    ///
    /// Es lo que permite el único ataque que cambia el sitio donde se juega en vez de por dónde se
    /// pasa: si el suelo se vuelve mortal, esto es la respuesta. Y como desaparece, la respuesta
    /// caduca — quedarse arriba tampoco es gratis.
    ///
    /// Parpadea el último segundo antes de irse. Sin ese aviso, "el suelo se ha desvanecido bajo
    /// mis pies" sería la peor muerte posible: la que no se ve venir.
    ///
    /// Va <b>pooled</b>; el GameObject, su sprite y su collider se construyen una sola vez.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossPlatform : MonoBehaviour, IPooled
    {
        private static Pool<BossPlatform> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private const float BlinkSeconds = 1f;

        private SpriteRenderer _renderer;
        private BoxCollider2D _collider;

        private float _secondsLeft;
        private Color _tint;
        private bool _finished;

        public static BossPlatform Spawn(in BossContext ctx, Vector2 center, Vector2 size, float seconds)
        {
            _pool ??= new Pool<BossPlatform>(Build, prewarm: 6);

            var platform = _pool.Get();
            platform.Begin(ctx, center, size, seconds);
            return platform;
        }

        private static BossPlatform Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Platform", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);

            // La capa es lo único que hace que esto sea suelo de verdad: el jugador busca la capa
            // 'Ground' con sus propios rayos, no colliders concretos.
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) go.layer = ground;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;

            var platform = go.AddComponent<BossPlatform>();
            platform._collider = collider;
            return platform;
        }

        private void Begin(in BossContext ctx, Vector2 center, Vector2 size, float seconds)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();

            _secondsLeft = Mathf.Max(0.5f, seconds);
            _tint = ctx.Accent;
            _finished = false;

            transform.position = center;
            transform.rotation = Quaternion.identity;

            _renderer.sprite = ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
            _renderer.color = _tint;
            AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
            AbilityFx.Resize(transform, _renderer, size);

            // El collider va en coordenadas locales, así que hay que deshacer la escala que acaba
            // de aplicar Resize para que mida justo lo que se ve.
            var scale = transform.localScale;
            _collider.size = new Vector2(size.x / Mathf.Max(0.0001f, scale.x),
                                         size.y / Mathf.Max(0.0001f, scale.y));
            _collider.offset = Vector2.zero;
            _collider.enabled = true;
        }

        void IPooled.OnReturnedToPool()
        {
            if (_collider != null) _collider.enabled = false;
        }

        private void Update()
        {
            if (_finished) return;

            _secondsLeft -= Time.deltaTime;
            if (_secondsLeft <= 0f) { Finish(); return; }

            if (_renderer == null) return;

            // El último segundo parpadea: es el aviso de que hay que bajarse.
            var color = _tint;
            color.a = _secondsLeft < BlinkSeconds
                ? _tint.a * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_secondsLeft * 14f)))
                : _tint.a;
            _renderer.color = color;
        }

        private void Finish()
        {
            _finished = true;
            if (_collider != null) _collider.enabled = false;
            _pool?.Release(this);
        }
    }
}
