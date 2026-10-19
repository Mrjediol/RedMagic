using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>Un enemigo visto como en la web: centro del cuerpo y semiejes de su elipse (de los bounds de sus colliders).</summary>
    public sealed class Body
    {
        public Health health;
        public Rigidbody2D rb;
        public Knockback knockback;
        public Vector2 c;
        public float rx, ry;
        public bool flies;

        public bool Dead => health == null || health.IsDead;
        public GameObject Go => health != null ? health.gameObject : null;
    }

    /// <summary>Golpe de un segmento contra un cuerpo: fracción <see cref="u"/> del segmento y punto de entrada.</summary>
    public struct RayHit
    {
        public Body body;
        public float u;
        public Vector2 p;
    }

    /// <summary>
    /// Consultas de enemigos para las armas del arsenal (las <c>alive()</c>, <c>raycastEnemy</c>, <c>nearestEnemy</c>… de la
    /// web) y del suelo. Enemigo = cualquier <see cref="Health"/> vivo que no sea del bando del jugador (<see cref="Teams"/>).
    /// La lista se recalcula una vez por frame.
    /// </summary>
    public static class ArsenalTargets
    {
        /// <summary>Capa del terreno pintado (la que para proyectiles y haces).</summary>
        public const int GroundLayer = 6;
        public static readonly int GroundMask = 1 << GroundLayer;

        private static readonly List<Body> AliveList = new List<Body>(64);
        private static readonly Dictionary<Health, Body> Cache = new Dictionary<Health, Body>();
        private static readonly List<Collider2D> Cols = new List<Collider2D>(8);
        private static int _frame = -1;

        /// <summary>Sólo para los renders del editor (sin Play, <c>Health.Active</c> está vacío): los enemigos a usar.</summary>
        public static List<Health> PreviewHealths;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AliveList.Clear();
            Cache.Clear();
            _frame = -1;
            PreviewHealths = null;
        }

        /// <summary>Enemigos vivos este frame. Lista compartida: léela, no la guardes.</summary>
        public static List<Body> Alive()
        {
            if (_frame == Time.frameCount && PreviewHealths == null) return AliveList;
            _frame = Time.frameCount;
            AliveList.Clear();
            IEnumerable<Health> source = PreviewHealths != null ? (IEnumerable<Health>)PreviewHealths : Health.Active;
            foreach (var h in source)
            {
                if (h == null || h.IsDead || !h.isActiveAndEnabled) continue;
                if (Teams.Of(h) == Team.Player) continue;
                var b = Of(h);
                if (b != null) AliveList.Add(b);
            }
            return AliveList;
        }

        /// <summary>El cuerpo de <paramref name="h"/> (centro y semiejes actualizados), o null si no tiene collider.</summary>
        public static Body Of(Health h)
        {
            if (h == null) return null;
            if (!Cache.TryGetValue(h, out var b) || b.health == null)
            {
                b = new Body { health = h, rb = h.GetComponentInParent<Rigidbody2D>(), knockback = h.GetComponent<Knockback>() };
                if (b.rb == null) b.rb = h.GetComponentInChildren<Rigidbody2D>();
                Cache[h] = b;
            }
            Cols.Clear();
            h.GetComponentsInChildren(false, Cols);
            bool any = false; Bounds bounds = default;
            foreach (var c in Cols)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!any) { bounds = c.bounds; any = true; } else bounds.Encapsulate(c.bounds);
            }
            if (!any)
            {
                foreach (var c in Cols) { if (c == null || !c.enabled) continue; if (!any) { bounds = c.bounds; any = true; } else bounds.Encapsulate(c.bounds); }
                if (!any) return null;
            }
            b.c = bounds.center;
            b.rx = Mathf.Max(0.1f, bounds.extents.x);
            b.ry = Mathf.Max(0.1f, bounds.extents.y);
            b.flies = b.rb != null && b.rb.gravityScale <= 0.001f;
            return b;
        }

        /// <summary>Primer enemigo que corta el segmento a→b (u = fracción 0..1), saltando los de <paramref name="skip"/>.</summary>
        public static bool Raycast(Vector2 a, Vector2 b, out RayHit hit, HashSet<Health> skip = null, float pad = 0f)
        {
            hit = default; bool found = false;
            foreach (var e in Alive())
            {
                if (skip != null && skip.Contains(e.health)) continue;
                if (!SegHitsEllipse(a, b, e.c, e.rx + pad, e.ry + pad, out float u0)) continue;
                if (!found || u0 < hit.u) { hit = new RayHit { body = e, u = u0, p = Vector2.Lerp(a, b, u0) }; found = true; }
            }
            return found;
        }

        /// <summary>Segmento contra elipse (como <c>segHitsEllipse</c>): entra si el punto de entrada cae dentro del segmento.</summary>
        public static bool SegHitsEllipse(Vector2 a, Vector2 b, Vector2 c, float rx, float ry, out float u0)
        {
            u0 = 0f;
            float dx = (b.x - a.x) / rx, dy = (b.y - a.y) / ry, fx = (a.x - c.x) / rx, fy = (a.y - c.y) / ry;
            float A = dx * dx + dy * dy, B = 2f * (fx * dx + fy * dy), C = fx * fx + fy * fy - 1f, D = B * B - 4f * A * C;
            if (D < 0f || A <= 0f) return false;
            float s = Mathf.Sqrt(D);
            u0 = (-B - s) / (2f * A);
            return u0 >= 0f && u0 <= 1f;
        }

        /// <summary>El vivo más cercano a <paramref name="p"/> a menos de <paramref name="maxD"/> (centro a centro).</summary>
        public static Body Nearest(Vector2 p, float maxD = 9999f, HashSet<Health> skip = null)
        {
            Body best = null; float bd = maxD;
            foreach (var e in Alive())
            {
                if (skip != null && skip.Contains(e.health)) continue;
                float d = Vector2.Distance(e.c, p);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        /// <summary>El enemigo cuya elipse (ampliada en <paramref name="pad"/>) contiene <paramref name="p"/>.</summary>
        public static Body Inside(Vector2 p, float pad = 0f)
        {
            foreach (var e in Alive())
            {
                float x = (p.x - e.c.x) / (e.rx + pad), y = (p.y - e.c.y) / (e.ry + pad);
                if (x * x + y * y < 1f) return e;
            }
            return null;
        }

        /// <summary>Enemigos cuyo cuerpo queda a menos de <paramref name="r"/> de <paramref name="p"/> (margen del 60 % del cuerpo).</summary>
        public static void Near(Vector2 p, float r, List<Body> into)
        {
            into.Clear();
            foreach (var e in Alive()) if (Vector2.Distance(e.c, p) < r + Mathf.Max(e.rx, e.ry) * 0.6f) into.Add(e);
        }

        /// <summary>Altura del suelo bajo <paramref name="x"/> (rayo hacia abajo desde <paramref name="fromY"/>); sin suelo, fromY − 20.</summary>
        public static float GroundY(float x, float fromY, float maxDown = 40f)
        {
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, maxDown, GroundMask);
            return hit.collider != null ? hit.point.y : fromY - 20f;
        }

        /// <summary>¿Toca el segmento a→b el terreno? Devuelve el punto y la normal del primer contacto.</summary>
        public static bool GroundHit(Vector2 a, Vector2 b, out Vector2 point, out Vector2 normal)
        {
            var hit = Physics2D.Linecast(a, b, GroundMask);
            point = hit.point; normal = hit.normal;
            return hit.collider != null;
        }

        /// <summary>¿Está <paramref name="p"/> dentro del terreno?</summary>
        public static bool InGround(Vector2 p) => Physics2D.OverlapPoint(p, GroundMask) != null;
    }
}
