using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Motas de vida (Vampirismo): pequeñas cruces que se mueven solas y vuelven al pool.
    /// <list type="bullet">
    /// <item><see cref="Drain"/> — rojas: salen del enemigo muerto con un pequeño estallido y vuelan
    /// hasta el jugador, persiguiéndolo si se mueve. Es "la vida que le robas".</item>
    /// <item><see cref="Heal"/> — verdes: aparecen sobre el jugador y suben desvaneciéndose. Es "la
    /// curación que entra".</item>
    /// </list>
    /// Pooled (<see cref="Core.Pool{T}"/>) y construidas en código, como <c>DamagePopups</c>: el
    /// GameObject y el SpriteRenderer se crean una vez. El dibujo es una cruz generada en código;
    /// para arte real, pasa un sprite en <see cref="Sprite"/> (lo expone <c>SynergyTuning</c>).
    /// </summary>
    public static class LifeMotes
    {
        public static readonly Color DrainColor = new Color(1f, 0.25f, 0.3f, 1f);
        public static readonly Color HealColor = new Color(0.4f, 1f, 0.45f, 1f);

        private static Core.Pool<LifeMote> _pool;
        private static Sprite _plus;

        /// <summary>Sprite de las motas; null = la cruz de código.</summary>
        public static Sprite Sprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _pool = null;
            Sprite = null;
        }

        /// <summary>
        /// Motas rojas desde <paramref name="from"/> hasta <paramref name="target"/>, que <b>llevan la
        /// curación</b>: cada una cura <c>healAmount / count</c> al llegar al jugador, no antes. La
        /// primera que de verdad sube la vida suelta las motas verdes (<paramref name="healMotes"/>)
        /// una sola vez por baja; a tope de vida no sale ninguna verde. Sin motas (count 0) cura ya.
        /// </summary>
        public static void Drain(Vector2 from, Combat.Health target, int count, float healAmount, int healMotes,
                                 GameObject sortingReference = null)
        {
            if (target == null) return;

            if (count <= 0)
            {
                float before = target.CurrentHealth;
                target.Heal(healAmount);
                if (target.CurrentHealth > before) Heal(target.transform, healMotes, sortingReference);
                return;
            }

            var packet = DrainPacket.Get(target, healAmount, count, healMotes, sortingReference);
            for (int i = 0; i < count; i++)
            {
                var mote = Get(sortingReference);
                Vector2 burst = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f) + Vector2.up * 1.2f;
                mote.BeginDrain(from + Random.insideUnitCircle * 0.25f, target.transform, burst, DrainColor, packet);
            }
        }

        /// <summary>Motas verdes que suben sobre <paramref name="target"/>.</summary>
        public static void Heal(Transform target, int count = 5, GameObject sortingReference = null)
        {
            if (target == null) return;
            for (int i = 0; i < count; i++)
            {
                var mote = Get(sortingReference);
                var offset = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.2f, 0.5f));
                mote.BeginHeal(target, offset, Random.Range(0f, 0.15f), HealColor);
            }
        }

        internal static void Release(LifeMote mote) => _pool?.Release(mote);

        /// <summary>Centro del cuerpo de <paramref name="target"/> (su collider), no el pivote de los pies.</summary>
        internal static Vector2 BodyCenter(Transform target)
        {
            var collider = target.GetComponentInChildren<Collider2D>();
            return collider != null && collider.enabled ? (Vector2)collider.bounds.center : (Vector2)target.position;
        }

        private static LifeMote Get(GameObject sortingReference)
        {
            _pool ??= new Core.Pool<LifeMote>(Build, prewarm: 24);
            var mote = _pool.Get();
            mote.Renderer.sprite = Sprite != null ? Sprite : Plus();
            if (sortingReference != null) Abilities.AbilityFx.CopySorting(mote.Renderer, sortingReference, 20);
            return mote;
        }

        private static LifeMote Build()
        {
            var go = new GameObject("Life Mote");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 50;
            var mote = go.AddComponent<LifeMote>();
            mote.Renderer = renderer;
            return mote;
        }

        /// <summary>Cruz blanca de 16 px con borde suave: el color lo pone cada mota.</summary>
        private static Sprite Plus()
        {
            if (_plus != null) return _plus;

            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "LifeMotePlus",
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Abs(x - 7.5f), cy = Mathf.Abs(y - 7.5f);
                bool arm = (cx <= 2f && cy <= 7f) || (cy <= 2f && cx <= 7f);
                bool edge = (cx <= 3f && cy <= 7.5f) || (cy <= 3f && cx <= 7.5f);
                byte a = arm ? (byte)255 : edge ? (byte)110 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _plus = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return _plus;
        }
    }
}
