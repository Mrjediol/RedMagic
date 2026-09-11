using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un trozo de suelo que se queda <b>envenenado</b> durante unos segundos: escombro ardiendo,
    /// brea, cristal. Mientras dura, hace daño a quien se plante encima.
    ///
    /// Es lo contrario de todo lo demás que lanza un jefe. Una onda, una lluvia o una guadaña son
    /// preguntas instantáneas: pasan y se olvidan. Esto <b>no se va</b>, así que la arena se va
    /// quedando pequeña y la pregunta deja de ser "¿cómo esquivo esto?" para ser "¿dónde voy a
    /// poder estar dentro de diez segundos?". Por eso conviene que dure bastante y haga poco daño
    /// por tic: su trabajo es quitar sitio, no matar.
    ///
    /// El daño se resuelve por <see cref="AbilityHit.DamageBox"/> a intervalos, sin collider
    /// propio; los i-frames del jugador ya limitan cuántas veces le puede entrar de verdad.
    ///
    /// Con <see cref="UntilBossDies"/> no se apaga nunca: dura hasta que su jefe muere
    /// (<see cref="ClearFrom"/>, que llama <see cref="BossController"/>) o se descarga la escena.
    ///
    /// Va <b>pooled</b>: un combate largo deja el suelo lleno de estos.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHazard : MonoBehaviour, IPooled
    {
        /// <summary>Duración de un hazard que no se apaga solo: dura hasta que muere su jefe.</summary>
        public const float UntilBossDies = float.PositiveInfinity;

        private static Pool<BossHazard> _pool;

        /// <summary>Hazards vivos ahora mismo, para poder recoger los de un jefe cuando muere.</summary>
        private static readonly List<BossHazard> Active = new List<BossHazard>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools()
        {
            _pool = null;
            Active.Clear();
        }

        private SpriteRenderer _renderer;
        private FxPlaceholderStyle _style;
        private bool _pooledPrefab;
        private bool _styled = true;

        private AbilityContext _ctx;
        private BossController _owner;
        private Vector2 _size;
        private float _secondsLeft;
        private float _totalSeconds;
        private bool _permanent;
        private float _tickInterval;
        private float _tickTimer;
        private float _damagePerTick;
        private float _knockbackMultiplier;
        private Color _tint;
        private bool _finished;

        /// <summary>Hazard con el prefab compartido del jefe (<see cref="BossController.HazardPrefab"/>) y el color de la fase.</summary>
        public static BossHazard Spawn(in BossContext ctx, Vector2 center, Vector2 size, float seconds,
                                       float damagePerTick, float tickInterval, float knockbackMultiplier) =>
            Spawn(ctx, ctx.Boss != null ? ctx.Boss.HazardPrefab : null, ctx.Accent, center, size, seconds,
                  damagePerTick, tickInterval, knockbackMultiplier);

        /// <summary>
        /// Hazard con prefab y color propios del ataque (el fuego verde), en vez del slot compartido
        /// del jefe. <paramref name="seconds"/> = <see cref="UntilBossDies"/> para uno permanente.
        /// </summary>
        public static BossHazard Spawn(in BossContext ctx, GameObject prefab, Color tint, Vector2 center,
                                       Vector2 size, float seconds, float damagePerTick, float tickInterval,
                                       float knockbackMultiplier)
        {
            var hazard = BossFxSpawn.FromPrefab<BossHazard>(prefab);
            bool fromPrefab = hazard != null;

            if (hazard == null)
            {
                _pool ??= new Pool<BossHazard>(Build, prewarm: 8);
                hazard = _pool.Get();
            }

            hazard._pooledPrefab = fromPrefab;
            hazard.Begin(ctx, tint, center, size, seconds, damagePerTick, tickInterval, knockbackMultiplier);
            return hazard;
        }

        /// <summary>Recoge todos los hazards que dejó <paramref name="boss"/>. Lo llama al morir.</summary>
        public static void ClearFrom(BossController boss)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (i >= Active.Count) continue;

                var hazard = Active[i];
                if (hazard == null) { Active.RemoveAt(i); continue; }
                if (hazard._owner == boss && !hazard._finished) hazard.Finish();
            }
        }

        private static BossHazard Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Hazard", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);
            return go.AddComponent<BossHazard>();
        }

        private void Begin(in BossContext ctx, Color tint, Vector2 center, Vector2 size, float seconds,
                           float damagePerTick, float tickInterval, float knockbackMultiplier)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_style == null) _style = GetComponent<FxPlaceholderStyle>();
            _styled = !_pooledPrefab || _style != null;

            _ctx = ctx.Ability;
            _owner = ctx.Boss;
            _size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
            _permanent = float.IsPositiveInfinity(seconds);
            _totalSeconds = _permanent ? 1f : Mathf.Max(0.2f, seconds);
            _secondsLeft = _totalSeconds;
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            // El primer tic no es inmediato: aparecer justo debajo del jugador no debería contar
            // como un golpe antes de que le dé tiempo a apartarse.
            _tickTimer = _tickInterval;
            _damagePerTick = Mathf.Max(0f, damagePerTick);
            _knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            _tint = _styled ? tint : Color.white;
            _finished = false;

            if (!Active.Contains(this)) Active.Add(this);

            transform.position = center;

            if (!_pooledPrefab)
            {
                _renderer.sprite = ctx.FxSprite != null ? ctx.FxSprite : AbilityFx.DefaultSprite;
                AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
                AbilityFx.Resize(transform, _renderer, _size);
            }
            else if (_style != null)
            {
                _style.Apply(tint, _size, ctx.Ability.Caster);
            }
        }

        void IPooled.OnReturnedToPool()
        {
        }

        // PrefabPool no tiene hook por instancia: inerte hasta el próximo Begin.
        private void OnEnable() => _finished = true;

        // También cubre el caso de PoolRunner recogiéndolo al cargar escena, que no pasa por Finish.
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (_finished) return;

            if (!_permanent)
            {
                _secondsLeft -= Time.deltaTime;
                if (_secondsLeft <= 0f) { Finish(); return; }
            }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = _tickInterval;
                if (_damagePerTick > 0f)
                    AbilityHit.DamageBox(_ctx, transform.position, _size, 0f,
                                         _damagePerTick, _knockbackMultiplier, transform.position);
            }

            Breathe();
        }

        /// <summary>
        /// Late lentamente y se apaga al final. Lo importante es lo segundo: el jugador tiene que
        /// poder ver que un trozo de suelo está a punto de volver a ser suyo.
        /// </summary>
        private void Breathe()
        {
            if (_renderer == null || !_styled) return;

            float life = _permanent ? 1f : Mathf.Clamp01(_secondsLeft / _totalSeconds);
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 6f);
            float fade = life < 0.25f ? life / 0.25f : 1f;

            var color = _tint;
            color.a = _tint.a * 0.7f * pulse * fade;
            _renderer.color = color;
        }

        private void Finish()
        {
            _finished = true;
            Active.Remove(this);
            BossFxSpawn.Release(this, _pooledPrefab, _pool);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(_size.x, _size.y, 0f));
        }
    }
}
