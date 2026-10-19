using RedMagic.Gameplay;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>Sensación de golpe compartida (temblor de cámara), en amplitud de unidades.</summary>
    public static class ArsenalFeel
    {
        /// <summary>Temblor; <paramref name="amount"/> en "px" de la web × <see cref="ArsenalDraw.PX"/> aprox. (0,1 = suave, 0,3 = fuerte).</summary>
        public static void Shake(float amount) => CameraFollow.ShakeAll(Mathf.Min(0.35f, amount), 0.18f);
    }

    /// <summary>Dibujo de las reacciones y de los estados (bloque de hielo, barra de congelación, llamas).</summary>
    public static class ArsenalReactFx
    {
        /// <summary>Añicos: trozos de hielo que salen despedidos girando, destello y anillo.</summary>
        public static void Shatter(Vector2 c, float rx, float ry)
        {
            float seed = Mathf.Floor(c.x * 31f);
            ArsenalWorld.AddFx(1.1f, (l, t0) =>
            {
                float t = ArsenalWorld.T, u = t - t0; var I = ArsenalPal.Ice;
                if (l == L.Front)
                {
                    float fu = Seg(u, 0f, 0.18f);
                    Glow(c, 2.4f * (1f - fu * 0.4f), I.g1, 0.8f * (1f - fu));
                    Star4(l, c, 1.3f * (1f - fu * 0.3f), 0.4f, I.core, 1f - fu);
                    float ru = Seg(u, 0f, 0.35f);
                    EllArc(l, c, (0.4f + 2.2f * EaseOut(ru)), (0.4f + 2.2f * EaseOut(ru)), 0f, TAU, 3.2f * PX * (1f - ru) + 0.5f * PX, I.light, 1f - ru, new ArcOpt { m = 40, wob = 0.08f, seed = 4, noTaper = true });
                }
                var R = new Rng(seed);
                for (int i = 0; i < 16; i++)
                {
                    float ang = R.Next() * TAU, v = (3f + R.Next() * 6f), spin = R.Next() * 12f - 6f, r = (5f + R.Next() * 7f) * PX;
                    bool back = R.Next() < 0.4f; if ((back ? L.Back : L.Front) != l) continue;
                    Vector2 p = c + new Vector2(Mathf.Cos(ang) * rx * 0.6f, Mathf.Sin(ang) * ry * 0.6f) + Dir(ang) * v * u + new Vector2(0f, -9f * u * u);
                    Chunk(l, p, r * (1f - Seg(u, 0.6f, 1.1f) * 0.5f), u * spin, i * 13 + seed, I.mid, I.core, 1f - Seg(u, 0.6f, 1.1f));
                }
                Sparks(l, t, t0, c, new SparkOpt { n = 14, pal = I, seed = seed + 2, sp = 420, grav = 400, len = 16, a0 = 0f, a1 = TAU, life = 0.5f, w = 2.6f, backShare = 0.45f });
            });
        }

        /// <summary>Vapor / deshielo: bocanadas de vapor que suben.</summary>
        public static void Steam(Vector2 c)
        {
            float seed = Mathf.Floor(c.x * 17f);
            ArsenalWorld.AddFx(1f, (l, t0) =>
            {
                float u = ArsenalWorld.T - t0; var R = new Rng(seed);
                for (int i = 0; i < 12; i++)
                {
                    float ang = R.Next() * TAU, d = (0.3f + R.Next() * 1.2f), lf = 0.6f + R.Next() * 0.4f, k = u / lf;
                    bool back = R.Next() < 0.5f; if (k >= 1f || (back ? L.Back : L.Front) != l) continue;
                    Vector2 p = c + Dir(ang) * d * EaseOut(k) + Vector2.up * (1.2f * k);
                    float r = (0.25f + R.Next() * 0.3f) * (0.6f + k);
                    Disc(l, p, r, new Color(0.78f, 0.84f, 0.9f), (1f - k) * 0.45f);
                    Disc(l, p + new Vector2(-r * 0.3f, r * 0.3f), r * 0.55f, Color.white, (1f - k) * 0.5f);
                }
            });
        }
    }

    /// <summary>
    /// Dibuja los estados de todos los enemigos afectados: barra de congelación cian bajo el cuerpo, bloque de hielo
    /// translúcido mientras está congelado (con grietas que crecen al acabarse) y llamas mientras arde. Una sola entidad
    /// permanente del mundo; la crea <see cref="Ensure"/>.
    /// </summary>
    public sealed class ArsenalStatusDrawer : ArsenalEntity
    {
        private static ArsenalStatusDrawer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public static void Ensure()
        {
            if (_instance != null && Contains()) return;
            _instance = ArsenalWorld.Add(new ArsenalStatusDrawer());
        }

        private static bool Contains()
        {
            foreach (var e in ArsenalWorld.Entities) if (e == _instance) return true;
            return false;
        }

        public override bool Tick(float dt) => true;

        public override void Draw(L l)
        {
            var list = ElementStatus.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || s.Health == null || s.Health.IsDead) continue;
                var b = ArsenalTargets.Of(s.Health); if (b == null) continue;
                if (s.IsFrozen) IceBlock(l, b, s);
                else if (s.Freeze > 0f && l == L.Front) FreezeBar(l, b, s.Freeze);
                if (s.IsBurning) Burning(l, b, i);
            }
        }

        private static void FreezeBar(L l, Body b, float k)
        {
            Vector2 a = new Vector2(b.c.x - 0.55f, b.c.y - b.ry - 0.22f);
            var I = ArsenalPal.Ice;
            var cv = Solid(l);
            cv.Line(a, a + new Vector2(1.1f, 0f), 0.11f, new Color(0.05f, 0.1f, 0.2f, 0.7f));
            cv.Line(a, a + new Vector2(1.1f * k, 0f), 0.07f, A(I.mid, 1f));
            cv.Line(a + new Vector2(0f, 0.015f), a + new Vector2(1.1f * k, 0.015f), 0.025f, A(I.core, 0.9f));
        }

        private static void IceBlock(L l, Body b, ElementStatus s)
        {
            var I = ArsenalPal.Ice; float t = ArsenalWorld.T, grow = EaseOut(Seg(t, s.FrozenAt, s.FrozenAt + 0.15f));
            float rx = (b.rx + 0.12f) * grow, ry = (b.ry + 0.12f) * grow;
            float crack = 1f - Mathf.Clamp01(s.FrozenRemaining / 0.8f);
            if (l == L.Back) { Solid(l).Ellipse(b.c, rx, ry, 0f, A(I.mid, 0.35f), A(I.deep, 0.5f), 28); return; }
            Glow(b.c, Mathf.Max(rx, ry) * 1.5f, I.g1, 0.35f);
            Solid(l).Ellipse(b.c, rx, ry, 0f, A(I.light, 0.18f), A(I.mid, 0.4f), 28);
            EllArc(l, b.c, rx, ry, 0.6f, 2.5f, 3f * PX, I.core, 0.8f, new ArcOpt { m = 24, seed = 3, taper = 1 });
            Lens(l, b.c + new Vector2(-rx * 0.35f, ry * 0.3f), 1.9f, ry * 0.9f, 0.05f, I.core, 0.55f);
            if (crack > 0f)
            {
                var R = new Rng(b.c.x * 7f + 3f); var pts = TempPoints();
                for (int k = 0; k < 3; k++)
                {
                    pts.Clear(); Vector2 p = b.c + new Vector2((R.Next() - 0.5f) * rx, (R.Next() - 0.5f) * ry); pts.Add(p);
                    for (int j = 0; j < 3; j++) { p += Dir(R.Next() * TAU) * 0.2f * crack; pts.Add(p); }
                    StrokeTaper(l, pts, 2.4f * PX, I.core, 0.9f * crack, false);
                }
            }
        }

        private static void Burning(L l, Body b, int i)
        {
            float t = ArsenalWorld.T;
            for (int k = 0; k < 4; k++)
            {
                bool back = k % 2 == 0; if ((back ? L.Back : L.Front) != l) continue;
                float x = b.c.x + (k - 1.5f) * b.rx * 0.5f, y = b.c.y - b.ry * 0.3f + Mathf.Sin(k * 2.1f) * b.ry * 0.3f;
                Flame(l, new Vector2(x, y), (18f + 8f * Mathf.Sin(t * 5f + k)) * PX, 6f * PX, t, i * 11 + k, 0.9f);
            }
            if (l == L.Front) Glow(b.c, b.ry * 1.6f, ArsenalPal.Fire.g1, 0.25f);
        }
    }
}
