using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;
using static RedMagic.Items.CometDraw;

namespace RedMagic.Items
{
    /// <summary>
    /// Efectos de golpe del cometa, dibujados por código y <b>pooled</b> (<see cref="Pool{T}"/>). Un tipo por uso:
    /// <list type="bullet">
    /// <item><see cref="Kind.Impact"/>: impacto final (destello, onda estrecha, esquirlas hacia atrás). Con
    /// upgrade, el impacto potente: púas, corona, nubes de energía, dos ondas, escombros y chispas, compacto.</item>
    /// <item><see cref="Kind.Pierce"/>: perforación — agujas radiales al entrar, hilo larguísimo a través del
    /// cuerpo y agujas hacia delante al salir (más y en azul/morado con upgrade, con ondas).</item>
    /// <item><see cref="Kind.Chain"/>: primer golpe de la cadena — llega, se queda pegado (brillo, venas,
    /// energía que converge y una aguja que apunta al siguiente) y sale reventando hacia él.</item>
    /// </list>
    /// Traducción de <c>impactN</c>, <c>impactPow</c>, <c>pierceP3</c> y <c>chainFx</c> del boceto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CometHitFx : MonoBehaviour, IPooled
    {
        public enum Kind { Impact, Pierce, Chain }

        private static Pool<CometHitFx> _pool;
        private static Material _material;

        private FxCanvas _back, _front;
        private Kind _kind;
        private CometPalette _pal = CometPalette.Normal;
        private float _px = 0.016f, _t, _life, _pm = 1f;
        private bool _pow;
        private Vector2 _p, _exit, _dir = Vector2.right;
        private float _ang, _tExit;

        // cadena
        private Transform _follow;
        private Vector2 _followOffset, _stickWorld;
        private float _stickTime, _releaseAt = -1f, _aimAng;
        private Collider2D _body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _pool = null;
            _material = null;
        }

        private static CometHitFx Get(Material material)
        {
            if (material != null) _material = material;
            _pool ??= new Pool<CometHitFx>(() => new GameObject("Comet Hit Fx").AddComponent<CometHitFx>(), prewarm: 4);
            var fx = _pool.Get();
            fx.EnsureCanvases();
            return fx;
        }

        /// <summary>Impacto final en <paramref name="point"/>, llegando con dirección <paramref name="dir"/>.</summary>
        public static void PlayImpact(Vector2 point, Vector2 dir, bool pow, CometPalette pal, float px, Material material)
        {
            var fx = Get(material);
            fx.Init(Kind.Impact, point, dir, pow, pal, px);
            fx._life = pow ? 0.65f : 0.5f;
        }

        /// <summary>
        /// Perforación: entra en <paramref name="entry"/>, sale por <paramref name="exit"/> tras
        /// <paramref name="timeToExit"/> segundos.
        /// </summary>
        public static void PlayPierce(Vector2 entry, Vector2 exit, Vector2 dir, float timeToExit, bool pow, CometPalette pal, float px, Material material)
        {
            var fx = Get(material);
            fx.Init(Kind.Pierce, entry, dir, pow, pal, px);
            fx._exit = exit;
            fx._tExit = Mathf.Max(0.01f, timeToExit);
            fx._life = fx._tExit + 0.5f;
        }

        /// <summary>
        /// Primer golpe de la cadena: llega a <paramref name="contact"/>, se pega en <paramref name="stick"/>
        /// (que sigue al enemigo) y espera a <see cref="ReleaseChain"/> para salir.
        /// </summary>
        public static CometHitFx PlayChain(Vector2 contact, Vector2 stick, Vector2 dir, Transform enemy, Collider2D body, float stickTime,
                                           bool pow, CometPalette pal, float px, Material material)
        {
            var fx = Get(material);
            fx.Init(Kind.Chain, contact, dir, pow, pal, px);
            fx._follow = enemy;
            fx._followOffset = enemy != null ? stick - (Vector2)enemy.position : Vector2.zero;
            fx._stickWorld = stick;
            fx._body = body;
            fx._stickTime = Mathf.Max(0.02f, stickTime);
            fx._aimAng = fx._ang;
            fx._releaseAt = -1f;
            fx._life = 99f;
            return fx;
        }

        /// <summary>Hacia dónde apunta mientras está pegado (la aguja que crece hacia el siguiente).</summary>
        public void AimChain(Vector2 dir)
        {
            if (dir.sqrMagnitude > 1e-6f) _aimAng = Mathf.Atan2(dir.y, dir.x);
        }

        /// <summary>Sale reventando en la dirección <paramref name="dir"/>.</summary>
        public void ReleaseChain(Vector2 dir)
        {
            AimChain(dir);
            if (_releaseAt >= 0f) return;
            _releaseAt = _t;
            _life = _t + 0.5f;
        }

        private void Init(Kind kind, Vector2 p, Vector2 dir, bool pow, CometPalette pal, float px)
        {
            _kind = kind;
            _p = p;
            _dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            _ang = Mathf.Atan2(_dir.y, _dir.x);
            _pow = pow;
            _pm = pow ? 1.5f : 1f;
            _pal = pal ?? CometPalette.Normal;
            _px = px;
            _t = 0f;
            _follow = null;
            _body = null;
            transform.position = p;
            Draw();
        }

        private void EnsureCanvases()
        {
            if (_back != null) return;
            var mat = _material != null ? _material : new Material(Shader.Find("RedMagic/Fx Vertex Color"));
            _back = new FxCanvas(transform, "__CometHitBack", mat, "Characters", 2);
            _front = new FxCanvas(transform, "__CometHitFront", mat, "Characters", 40);
        }

        void IPooled.OnReturnedToPool()
        {
            _follow = null;
            _body = null;
            _back?.Clear(); _back?.Apply();
            _front?.Clear(); _front?.Apply();
        }

        private void OnDestroy()
        {
            _back?.Destroy();
            _front?.Destroy();
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_t >= _life)
            {
                _pool?.Release(this);
                return;
            }
            Draw();
        }

        private void Draw()
        {
            _back.Clear();
            _front.Clear();
            switch (_kind)
            {
                case Kind.Impact: if (_pow) DrawImpactPow(); else DrawImpact(); break;
                case Kind.Pierce: DrawPierce(); break;
                case Kind.Chain: DrawChain(); break;
            }
            _back.Apply();
            _front.Apply();
        }

        // ------------------------------------------------------------------ impacto normal

        private void DrawImpact()
        {
            float ht = Seg(_t, 0f, 0.5f);
            if (ht <= 0f && _t > 0f || ht >= 1f) return;
            float fade = 1f - EaseIn(ht), e = EaseOut(ht), px = _px;
            Flash(_front, _p, ht, 26f * px, px, _pal);
            RingHalf(_back, _p, _ang, Mathf.Lerp(10f, 54f, e) * px, 0.32f, 0.1f, 6, Mathf.Lerp(5f, 1f, ht) * px, _pal.cyan, fade, false);
            RingHalf(_front, _p, _ang, Mathf.Lerp(10f, 54f, e) * px, 0.32f, 0.1f, 6, Mathf.Lerp(5f, 1f, ht) * px, _pal.white, fade, true);
            var R = new FxRng(41);
            Vector2 u = _dir, n = Perp(u);
            for (int i = 0; i < 14; i++)
            {
                float a = Mathf.PI + (R.Next() - 0.5f) * 2.2f - 0.3f, d = e * (30f + R.Next() * 60f), len = (8f + R.Next() * 14f) * (1f - ht);
                Vector2 local = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.8f), back = new Vector2(Mathf.Cos(a) * len, Mathf.Sin(a) * len * 0.8f);
                Vector2 tip = _p + (u * local.x + n * local.y) * px + Vector2.down * (ht * ht * 18f * px);
                Vector2 tail = tip - (u * back.x + n * back.y) * px;
                Shard(_front, tip, tail, (2.4f * (1f - ht) + 0.6f) * px, i % 3 != 0 ? _pal.light : _pal.white, fade);
            }
        }

        // ------------------------------------------------------------------ impacto potente (upgrade)

        private void DrawImpactPow()
        {
            float hb = Seg(_t, 0f, 0.65f);
            if (hb >= 1f) return;
            float grow = EaseOut(Seg(hb, 0f, 0.35f)), fade = 1f - EaseIn(Seg(hb, 0.3f, 1f)), back = _ang + Mathf.PI, px = _px;
            var R = new FxRng(99);

            float fl = 1f - Seg(hb, 0f, 0.14f);
            if (fl > 0f)
            {
                Glow(_front, _p, (44f + 44f * grow) * px, _pal.white, 0.9f * fl);
                Glow(_front, _p, (70f + 40f * grow) * px, _pal.glowDeep, 0.6f * fl);
                _front.Disc(_p, (14f + 22f * grow) * px, Fade(_pal.white, fl));
            }

            const int NS = 11;
            for (int i = 0; i < NS; i++)
            {
                float a = back + (i / (NS - 1f) - 0.5f) * 2.5f + (R.Next() - 0.5f) * 0.15f, len = (26f + R.Next() * 34f) * grow;
                var c = i % 2 == 1 ? _front : _back;
                Vector2 d = Dir(a);
                Shard(c, _p + d * ((36f + len) * px), _p + d * (14f * px), 12f * px, i % 2 == 1 ? _pal.purple : _pal.blue, fade);
            }
            for (int i = 0; i < NS; i++)
            {
                float a = back + (i / (NS - 1f) - 0.5f) * 2.8f + (R.Next() - 0.5f) * 0.2f;
                float len = (46f + R.Next() * 46f) * EaseOut(Seg(hb, 0f, 0.5f)) * (1f - Seg(hb, 0.35f, 0.75f));
                var c = i % 2 == 1 ? _back : _front;
                Vector2 d = Dir(a);
                Color col = (i % 3) switch { 0 => _pal.white, 1 => _pal.cyan, _ => _pal.purple };
                Shard(c, _p + d * ((12f + len) * px), _p + d * (12f * px), 6.5f * px, col, 0.9f * fade);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = back + (R.Next() - 0.5f) * 2.4f, dd = (10f + R.Next() * 44f) * grow;
                var c = R.Next() < 0.5f ? _back : _front;
                float r = (9f + R.Next() * 8f) * (0.5f + grow * 0.6f) * px;
                Vector2 pos = _p + Dir(a) * (dd * px) + Vector2.up * (hb * 16f * px);
                Glow(c, pos, r * 2.4f, _pal.glowPuff, 0.55f * fade);
                c.Disc(pos, r * 0.7f, Fade(i % 2 == 1 ? _pal.purple : _pal.blue, 0.6f * fade));
                c.Disc(pos + new Vector2(-r, r) * 0.2f, r * 0.3f, Fade(_pal.light, 0.5f * fade));
            }
            for (int k = 0; k < 2; k++)
            {
                float tk = Seg(hb, k * 0.08f, 0.7f + k * 0.1f);
                if (tk <= 0f || tk >= 1f) continue;
                float g = EaseOut(tk), r = (24f + (k == 1 ? 34f : 48f) * g) * px, w = (4.5f * (1f - tk) + 1f) * px;
                RingHalf(_back, _p, _ang, r, 0.32f, 0.1f, 6 + k, w * 3.2f, _pal.purple, 0.35f * (1f - tk), false);
                RingHalf(_front, _p, _ang, r, 0.32f, 0.1f, 6 + k, w * 3.2f, _pal.purple, 0.35f * (1f - tk), true);
                RingHalf(_back, _p, _ang, r, 0.32f, 0.1f, 6 + k, w, k == 1 ? _pal.cyan : _pal.white, 1f - tk, false);
                RingHalf(_front, _p, _ang, r, 0.32f, 0.1f, 6 + k, w, k == 1 ? _pal.cyan : _pal.white, 1f - tk, true);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = back + (R.Next() - 0.5f) * 2.2f + 0.4f, v = 140f + R.Next() * 140f, tt = hb * 0.65f, sz = (3f + R.Next() * 3f) * px, rot = tt * 12f + i;
                var c = i % 2 == 1 ? _back : _front;
                Vector2 pos = _p + (Dir(a) * (v * tt) + Vector2.down * (520f * tt * tt)) * px;
                Vector2 o = Dir(rot) * sz, q = Perp(o);
                c.Triangle(pos + o, pos - o * 0.6f + q * 0.8f, pos - o * 0.6f - q * 0.8f, Fade(i % 3 != 0 ? _pal.blue : _pal.purple, fade));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = back + (R.Next() - 0.5f) * 2.8f, d = 14f + EaseOut(hb) * (36f + R.Next() * 52f), tail = 10f * (1f - hb);
                Vector2 dir = Dir(a);
                Shard(_front, _p + dir * (d * px), _p + dir * ((d - tail) * px), (3f * (1f - hb) + 1f) * px, i % 2 == 1 ? _pal.cyan : _pal.white, 1f - hb);
            }
        }

        // ------------------------------------------------------------------ perforación

        private void DrawPierce()
        {
            float px = _px, pm = _pow ? 1.4f : 1f;
            Color[] pal = _pow
                ? new[] { _pal.white, _pal.blue, _pal.purple, _pal.light, _pal.cyan, _pal.purple }
                : new[] { _pal.white, _pal.light, _pal.cyan, _pal.white, _pal.light };
            float he = Seg(_t, 0f, 0.4f), hx = Seg(_t, _tExit, _tExit + 0.45f);
            Vector2 xe = _p, xx = _exit;

            Fan(_back, _front, xe, _ang, Mathf.RoundToInt(16 * pm), 61, 0f, Mathf.PI * 2f, 40f * pm, 120f * pm, he, 3.4f * Mathf.Min(pm, 1.2f), pal, px);
            Fan(_back, _front, xx, _ang, Mathf.RoundToInt(16 * pm), 71, -1.2f, 1.2f, 50f * pm, 140f * pm, hx, 3.2f * Mathf.Min(pm, 1.2f), pal, px);
            if (_pow)
            {
                if (he > 0f && he < 1f)
                {
                    RingHalf(_back, xe, _ang, Mathf.Lerp(8f, 60f, EaseOut(he)) * px, 0.32f, 0.1f, 31, Mathf.Lerp(6f, 1f, he) * px, _pal.purple, 1f - EaseIn(he), false);
                    RingHalf(_front, xe, _ang, Mathf.Lerp(8f, 60f, EaseOut(he)) * px, 0.32f, 0.1f, 31, Mathf.Lerp(6f, 1f, he) * px, _pal.purple, 1f - EaseIn(he), true);
                }
                if (hx > 0f && hx < 1f)
                {
                    RingHalf(_back, xx, _ang, Mathf.Lerp(8f, 72f, EaseOut(hx)) * px, 0.32f, 0.1f, 32, Mathf.Lerp(6f, 1f, hx) * px, _pal.blue, 1f - EaseIn(hx), false);
                    RingHalf(_front, xx, _ang, Mathf.Lerp(8f, 72f, EaseOut(hx)) * px, 0.32f, 0.1f, 32, Mathf.Lerp(6f, 1f, hx) * px, _pal.blue, 1f - EaseIn(hx), true);
                }
            }

            // hilo larguísimo que cruza el cuerpo
            float uu = Seg(_t, 0f, _tExit + 0.45f);
            if (uu < 1f)
            {
                float grow = EaseOut(Seg(_t, 0f, _tExit + 0.05f)), a = 1f - Seg(uu, 0.5f, 1f);
                Vector2 a0 = xe - _dir * (140f * grow * px), a1 = Vector2.Lerp(xe, xx + _dir * (130f * px), grow);
                _front.Needle(a0, a1, (_pow ? 18f : 12f) * (1f - uu) * px, Fade(_pal.purple, (_pow ? 0.55f : 0.35f) * a));
                _front.Needle(a0, a1, (_pow ? 9f : 6.5f) * (1f - uu) * px, Fade(_pow ? _pal.blue : _pal.cyan, 0.85f * a));
                _front.Needle(a0, a1, 3f * (1f - uu) * px, Fade(_pal.white, a));
            }
            if (he > 0f && he < 0.5f) Lens(_front, xe, _ang, Mathf.Lerp(30f, 60f, he * 2f) * px, 2f * px, _pal.white, 1f - he * 2f, 0f);
            if (hx > 0f && hx < 0.5f) Lens(_front, xx, _ang, Mathf.Lerp(30f, 66f, hx * 2f) * px, -2f * px, _pal.white, 1f - hx * 2f, 0f);
        }

        // ------------------------------------------------------------------ cadena

        private void DrawChain()
        {
            float px = _px, pm = _pm;
            Color[] fp = _pow
                ? new[] { _pal.white, _pal.blue, _pal.purple, _pal.light, _pal.cyan, _pal.purple }
                : new[] { _pal.white, _pal.light, _pal.cyan, _pal.white, _pal.light };
            if (_follow != null) _stickWorld = (Vector2)_follow.position + _followOffset;   // si el enemigo desaparece, se queda donde estaba
            Vector2 s1 = _stickWorld;
            float he = Seg(_t, 0f, 0.35f);

            // llegada: destello, onda y agujas hacia atrás
            if (he < 1f)
            {
                Flash(_front, _p, he, 28f * pm * px, px, _pal);
                RingHalf(_back, _p, _ang, Mathf.Lerp(6f, 46f * pm, EaseOut(he)) * px, 0.32f, 0.1f, 8, Mathf.Lerp(5f, 1f, he) * px, _pal.cyan, 1f - EaseIn(he), false);
                RingHalf(_front, _p, _ang, Mathf.Lerp(6f, 46f * pm, EaseOut(he)) * px, 0.32f, 0.1f, 8, Mathf.Lerp(5f, 1f, he) * px, _pal.white, 1f - EaseIn(he), true);
                Fan(_back, _front, _p, _ang, Mathf.RoundToInt(9 * pm), 11, Mathf.PI - 1f, Mathf.PI + 1f, 30f * pm, 85f * pm, he, 2.8f * Mathf.Min(pm, 1.3f), fp, px);
            }

            // pegado: la energía se acumula en el punto y crece la aguja que apunta al siguiente
            bool released = _releaseAt >= 0f;
            float stickEnd = released ? _releaseAt : _t;
            if (!released || _t < _releaseAt + 0.05f)
            {
                float u = Seg(stickEnd, 0.04f, _stickTime), fl = 0.75f + 0.25f * Mathf.Sin(_t * 90f);
                Glow(_front, s1, (16f + 36f * u) * pm * px, _pal.white, (0.35f + 0.55f * u) * fl);
                Glow(_front, s1, (30f + 50f * u) * pm * px, _pal.glowPurple, 0.5f * u + 0.15f);

                // venas de luz que parpadean, sin salirse del cuerpo
                float maxLen = _body != null ? Mathf.Min(_body.bounds.extents.x, _body.bounds.extents.y) / px : 30f;
                var R = new FxRng(Mathf.FloorToInt(_t / 0.06f) * 13 + 7);
                var pts = FxCanvas.Points();
                for (int i = 0; i < 7; i++)
                {
                    float a = R.Next() * Mathf.PI * 2f, len = Mathf.Min(maxLen, (10f + R.Next() * 22f) * (0.5f + u * 0.7f));
                    Vector2 q = s1;
                    pts.Clear(); pts.Add(q);
                    for (int k = 1; k <= 3; k++) { q += Dir(a + (R.Next() - 0.5f) * 1f) * (len / 3f * px); pts.Add(q); }
                    _front.Stroke(pts, 3.4f * px, Fade(_pal.cyan, 0.4f));
                    _front.Stroke(pts, 1.4f * px, Fade(_pal.white, 0.95f));
                }
                for (int i = 0; i < 8; i++)
                {
                    var Ri = new FxRng(i * 37 + 5);
                    float a = i * 2.39996f + Ri.Next(), cyc = Mathf.Repeat(_t * 3f + Ri.Next(), 1f), d = 10f + 46f * (1f - cyc);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f);
                    _front.Needle(s1 + dir * ((d + 16f) * px), s1 + dir * (d * px), 2.4f * px,
                                  Fade(i % 2 == 1 ? _pal.cyan : _pal.white, Mathf.Sin(Mathf.PI * cyc) * u * 0.9f));
                }
                _front.Needle(s1, s1 + Dir(_aimAng) * ((14f + 46f * u) * px), 3.2f * px, Fade(_pal.white, 0.85f * u));
            }

            // salida: reventón orientado hacia el siguiente
            if (released)
            {
                float hx = Seg(_t, _releaseAt, _releaseAt + 0.45f);
                if (hx > 0f && hx < 1f)
                {
                    float e = EaseOut(hx), f = 1f - EaseIn(hx);
                    Flash(_front, s1, hx, 44f * pm * px, px, _pal);
                    RingHalf(_back, s1, _aimAng, Mathf.Lerp(8f, 74f * pm, e) * px, 0.32f, 0.1f, 15, Mathf.Lerp(7f, 1f, hx) * px, _pal.purple, f, false);
                    RingHalf(_front, s1, _aimAng, Mathf.Lerp(8f, 74f * pm, e) * px, 0.32f, 0.1f, 15, Mathf.Lerp(7f, 1f, hx) * px, _pal.cyan, f, true);
                    Fan(_back, _front, s1, _aimAng, Mathf.RoundToInt(12 * pm), 29, -0.55f, 0.55f, 50f * pm, 150f * pm, hx, 3f * Mathf.Min(pm, 1.3f), fp, px);
                    Fan(_back, _front, s1, _aimAng, Mathf.RoundToInt(6 * pm), 41, Mathf.PI - 0.6f, Mathf.PI + 0.6f, 25f * pm, 60f * pm, hx, 2.4f, fp, px);
                    Lens(_front, s1, _aimAng, Mathf.Lerp(24f, 54f * pm, e) * px, 3f * px, _pal.white, hx < 0.15f ? 1f : 0f, 0f);
                    Lens(_front, s1, _aimAng, Mathf.Lerp(24f, 58f * pm, e) * px, 4f * px, _pal.white, f, Mathf.Lerp(3.5f, 0.5f, hx) * px);
                    Vector2 d = Dir(_aimAng);
                    _front.Needle(s1 - d * (10f * px), s1 + d * (Mathf.Lerp(30f, 130f * pm, e) * px), (5f * f + 1f) * px, Fade(_pal.white, f));
                }
            }
        }
    }
}
