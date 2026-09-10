using RedMagic.Abilities;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un orbe que el jefe tiene <b>flotando</b>, todavía sin disparar: el visual de carga de
    /// <see cref="OrbRingAttack"/>. No hace daño y no tiene collider — es puro aviso. Lo mueve y lo
    /// engorda el ataque, frame a frame, y cuando la corona se lanza el ataque lo suelta y en su
    /// sitio nace un proyectil de verdad.
    ///
    /// Pooled como el resto de visuales de jefe (<see cref="BossFxSpawn"/>): prefab si el ataque
    /// tiene uno, y si no, un sprite construido en código.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossOrb : MonoBehaviour, IPooled
    {
        private static Pool<BossOrb> _pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _pool = null;

        private SpriteRenderer _renderer;
        private bool _pooledPrefab;
        private bool _live;
        private float _lifeLeft;

        /// <summary>
        /// Escala autorizada en el prefab. Se lee en <c>Awake</c>, una sola vez por instancia y
        /// antes de que ningún <see cref="Place"/> la toque: el pool no restaura la escala al
        /// reciclar, así que leerla en cada spawn haría que el orbe menguase un poco en cada uso
        /// hasta desaparecer.
        /// </summary>
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (_baseScale.sqrMagnitude < 0.000001f) _baseScale = Vector3.one;
        }

        /// <summary>
        /// Saca un orbe. <paramref name="prefab"/> vacío = se construye en código con
        /// <paramref name="sprite"/>. <paramref name="maxLifetime"/> es un seguro: si la corrutina
        /// del ataque se corta a medias (cambio de fase, jefe muerto), el orbe se recoge solo en vez
        /// de quedarse flotando en la arena para siempre.
        /// </summary>
        public static BossOrb Spawn(in BossContext ctx, GameObject prefab, Vector2 position,
                                    Sprite sprite, Color tint, bool applyTint, float maxLifetime,
                                    float codeSize = 1f)
        {
            var orb = BossFxSpawn.FromPrefab<BossOrb>(prefab);
            bool fromPrefab = orb != null;

            if (orb == null)
            {
                _pool ??= new Pool<BossOrb>(Build, prewarm: 12);
                orb = _pool.Get();
            }

            orb._pooledPrefab = fromPrefab;
            orb.Begin(ctx, position, sprite, tint, applyTint, maxLifetime, codeSize);
            return orb;
        }

        private static BossOrb Build()
        {
            var go = AbilityFx.SpawnSprite("Boss Orb", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);
            return go.AddComponent<BossOrb>();
        }

        private void Begin(in BossContext ctx, Vector2 position, Sprite sprite, Color tint,
                           bool applyTint, float maxLifetime, float codeSize)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>(true);

            _live = true;
            _lifeLeft = Mathf.Max(0.2f, maxLifetime);
            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (_renderer == null) return;

            // Con prefab, el arte manda: sólo se le tiñe si el ataque lo pide expresamente. Sin
            // prefab hay que darle sprite, color y tamaño o saldría el cuadrado blanco de un metro.
            if (!_pooledPrefab)
            {
                _renderer.sprite = sprite != null ? sprite : AbilityFx.DefaultSprite;
                _renderer.color = tint;
                transform.localScale = Vector3.one;
                AbilityFx.Resize(_renderer.transform, _renderer, Vector2.one * Mathf.Max(0.01f, codeSize));
                _baseScale = transform.localScale;   // el sprite de código no tiene escala autorizada
            }
            else if (applyTint)
            {
                _renderer.color = tint;
            }

            AbilityFx.CopySorting(_renderer, ctx.Ability.Caster);
        }

        /// <summary>
        /// Recoloca y reescala el orbe; lo llama el ataque cada frame de la carga.
        /// <paramref name="scale"/> es un multiplicador sobre el tamaño natural del prefab: 1 = tal
        /// cual está autorizado, 0.2 = recién creado. Se escala en uniforme a propósito — el orbe es
        /// redondo y deformarlo por ejes lo convierte en un huevo.
        /// </summary>
        public void Place(Vector2 position, float scale)
        {
            if (!_live) return;

            transform.position = position;
            transform.localScale = _baseScale * Mathf.Max(0.001f, scale);
        }

        public Vector2 Position => transform.position;

        /// <summary>Devuelve el orbe al pool. Idempotente: llamarlo dos veces no duplica la suelta.</summary>
        public void Release()
        {
            if (!_live) return;
            _live = false;
            BossFxSpawn.Release(this, _pooledPrefab, _pool);
        }

        private void Update()
        {
            if (!_live) return;

            _lifeLeft -= Time.deltaTime;
            if (_lifeLeft <= 0f) Release();
        }

        // PrefabPool no tiene hook por instancia: al reactivarse el orbe queda inerte hasta que el
        // próximo Begin lo arme, para que no herede el estado del uso anterior.
        private void OnEnable() => _live = false;

        void IPooled.OnReturnedToPool() => _live = false;
    }
}
