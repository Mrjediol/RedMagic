using System;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Proyectil que viaja en línea recta del jefe a un <b>punto fijo</b> y avisa al llegar. No es
    /// un <c>Projectile</c>: no busca al jugador ni choca con nada por el camino — es el heraldo
    /// de lo que pase en destino (<see cref="PlatformDenialAttack"/>: el fuego que se queda).
    ///
    /// Se mueve solo, así que si el ataque se corta a medias (cambio de fase) llega igual; quien lo
    /// lanza decide en el aviso de llegada si todavía toca hacer algo (con el jefe muerto, no).
    ///
    /// Pooled como el resto de visuales de jefe (<see cref="BossFxSpawn"/>): prefab si el ataque
    /// trae uno, cuadrado de código si no. El prefab placeholder lleva
    /// <see cref="FxPlaceholderStyle"/> (se tiñe y se escala); el arte real sin él sólo se coloca y
    /// se orienta. Gira hacia donde viaja: el arte se dibuja mirando a +X.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossBolt : MonoBehaviour, IPooled
    {
        private static Pool<BossBolt> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private SpriteRenderer _renderer;
        private FxPlaceholderStyle _style;
        private bool _pooledPrefab;
        private bool _live;
        private Vector2 _target;
        private float _speed;
        private float _lifeLeft;
        private Action<Vector2> _onArrive;

        /// <summary>
        /// Lanza un proyectil de <paramref name="from"/> a <paramref name="to"/>.
        /// <paramref name="onArrive"/> recibe el punto de llegada.
        /// </summary>
        public static BossBolt Launch(in BossContext ctx, GameObject prefab, Vector2 from, Vector2 to,
                                      float speed, Vector2 size, Color tint, Action<Vector2> onArrive)
        {
            var bolt = BossFxSpawn.FromPrefab<BossBolt>(prefab);
            bool fromPrefab = bolt != null;

            if (bolt == null)
            {
                _pool ??= new Pool<BossBolt>(Build, prewarm: 6);
                bolt = _pool.Get();
            }

            bolt._pooledPrefab = fromPrefab;
            bolt.Begin(ctx, from, to, speed, size, tint, onArrive);
            return bolt;
        }

        private static BossBolt Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Bolt", null, Vector3.zero, Vector2.one, Color.white, 0f, null);
            return go.AddComponent<BossBolt>();
        }

        private void Begin(in BossContext ctx, Vector2 from, Vector2 to, float speed, Vector2 size,
                           Color tint, Action<Vector2> onArrive)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_style == null) _style = GetComponent<FxPlaceholderStyle>();

            _target = to;
            _speed = Mathf.Max(0.1f, speed);
            _onArrive = onArrive;
            // Seguro: si por lo que sea no llega, se recoge solo en vez de quedarse en la arena.
            _lifeLeft = Vector2.Distance(from, to) / _speed + 2f;
            _live = true;

            transform.position = from;
            Face(to - from);

            if (!_pooledPrefab)
            {
                _renderer.sprite = ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
                _renderer.color = tint;
                AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
                AbilityFx.Resize(transform, _renderer, size);
            }
            else if (_style != null)
            {
                _style.Apply(tint, size, ctx.Ability.Caster);
            }
        }

        private void Update()
        {
            if (!_live) return;

            Vector2 next = Vector2.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
            transform.position = next;

            if ((next - _target).sqrMagnitude < 0.0001f)
            {
                var arrive = _onArrive;
                Release();
                arrive?.Invoke(_target);
                return;
            }

            _lifeLeft -= Time.deltaTime;
            if (_lifeLeft <= 0f) Release();
        }

        private void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>Devuelve el proyectil al pool sin avisar de llegada. Idempotente.</summary>
        public void Release()
        {
            if (!_live) return;
            _live = false;
            _onArrive = null;
            BossFxSpawn.Release(this, _pooledPrefab, _pool);
        }

        // PrefabPool no tiene hook por instancia: inerte hasta el próximo Begin.
        private void OnEnable() => _live = false;

        void IPooled.OnReturnedToPool()
        {
            _live = false;
            _onArrive = null;
        }
    }
}
