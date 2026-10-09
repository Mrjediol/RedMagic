using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Estallido de impacto dibujado por código (<see cref="FxCanvas"/>), con parte por detrás y
    /// por delante del personaje. Dos usos con el mismo componente:
    /// <list type="bullet">
    /// <item><b>Suelo</b> (<see cref="ground"/>): destello, polvo con púas, dos ondas a ras de
    /// suelo (mitad lejana detrás), chispas, escombros con gravedad y temblor de cámara.</item>
    /// <item><b>Enemigo</b> (sin <see cref="ground"/>): versión pequeña sin polvo — destello, anillo
    /// completo, esquirlas cian sesgadas hacia la dirección del golpe y chispas.</item>
    /// </list>
    /// <see cref="Play"/> lo lanza; se devuelve solo al pool. Medidas en "px" × <see cref="pixel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactBurstFx : MonoBehaviour
    {
        [Header("Tipo")]
        [Tooltip("Impacto contra el suelo (polvo, púas, ondas a ras de suelo). Si no, impacto contra un enemigo.")]
        [SerializeField] private bool ground = true;

        [Header("Tamaño y tiempo")]
        [SerializeField] private float pixel = 0.016f;
        [SerializeField] private float duration = 0.8f;

        [Header("Piezas")]
        [SerializeField, Range(0, 20)] private int spikes = 11;
        [SerializeField, Range(0, 20)] private int puffs = 12;
        [SerializeField, Range(0, 30)] private int sparks = 16;
        [SerializeField, Range(0, 20)] private int debris = 10;

        [Header("Cámara")]
        [SerializeField] private float shakeAmplitude = 0.35f;
        [SerializeField] private float shakeDuration = 0.25f;

        [Header("Colores")]
        [SerializeField] private Color white = Color.white;
        [SerializeField] private Color lightCyan = new Color(0.62f, 0.95f, 1f);
        [SerializeField] private Color cyan = new Color(0.16f, 0.76f, 1f);
        [SerializeField] private Color blue = new Color(0.11f, 0.36f, 0.86f);
        [SerializeField] private Color dust = new Color(0.85f, 0.8f, 0.71f);
        [SerializeField] private Color dustDark = new Color(0.72f, 0.65f, 0.55f);
        [SerializeField] private Color rock = new Color(0.3f, 0.32f, 0.38f);

        [Header("Render")]
        [SerializeField] private Material material;
        [SerializeField] private string sortingLayer = "Characters";
        [SerializeField] private int backOrder = 2;
        [SerializeField] private int frontOrder = 40;

        private FxCanvas _back, _front;
        private Vector2 _pos, _dir = Vector2.right;
        private float _t, _power = 1f;
        private bool _running;

        /// <summary>Lanza el impacto en <paramref name="position"/> (suelo: los pies). <paramref name="direction"/> sesga las esquirlas del golpe a enemigo.</summary>
        public void Play(Vector2 position, Vector2 direction, float power = 1f)
        {
            EnsureCanvases();
            _pos = position;
            _dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            _power = Mathf.Max(0.1f, power);
            _t = 0f;
            _running = true;
            if (ground && shakeAmplitude > 0f && Application.isPlaying)
                CameraFollow.ShakeAll(shakeAmplitude * _power, shakeDuration);
            Draw();
        }

        private void OnEnable() => EnsureCanvases();

        private void OnDisable()
        {
            _running = false;
            _back?.Clear(); _back?.Apply();
            _front?.Clear(); _front?.Apply();
        }

        private void OnDestroy()
        {
            _back?.Destroy();
            _front?.Destroy();
        }

        private void EnsureCanvases()
        {
            if (_back != null) return;
            var mat = material != null ? material : new Material(Shader.Find("RedMagic/Fx Vertex Color"));
            _back = new FxCanvas(transform, "__ImpactBack", mat, sortingLayer, backOrder);
            _front = new FxCanvas(transform, "__ImpactFront", mat, sortingLayer, frontOrder);
        }

        private void LateUpdate()
        {
            if (!_running) return;
            _t += Time.deltaTime;
            if (_t >= duration)
            {
                _running = false;
                _back.Clear(); _back.Apply();
                _front.Clear(); _front.Apply();
                if (Application.isPlaying) PrefabPool.Despawn(gameObject);
                return;
            }
            Draw();
        }

        private void Draw()
        {
            _back.Clear();
            _front.Clear();
            float t = _t / duration;                    // 0..1
            if (ground) DrawGround(t); else DrawHit(t);
            _back.Apply();
            _front.Apply();
        }

        // ------------------------------------------------------------------ suelo

        private void DrawGround(float t)
        {
            float px = pixel * _power;
            float grow = EaseOut(Seg(t, 0f, 0.35f)), fade = 1f - EaseIn(Seg(t, 0.3f, 1f));
            var R = new Rng(99);

            // destello inicial
            float flash = 1f - Seg(t, 0f, 0.12f);
            if (flash > 0f)
            {
                _front.Glow(_pos + Vector2.up * 20f * px, (60f + 60f * grow) * px, Fade(white, 0.9f * flash));
                _front.Disc(_pos + Vector2.up * 10f * px, (22f + 30f * grow) * px, Fade(white, flash));
            }

            // púas de polvo (detrás), en abanico hacia arriba
            for (int i = 0; i < spikes; i++)
            {
                float a = Mathf.PI * (0.08f + 0.84f * (i / Mathf.Max(1f, spikes - 1f))) + (R.Next() - 0.5f) * 0.15f;
                float len = (50f + R.Next() * 60f) * grow;
                var dirv = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dirv.y, dirv.x);
                var b = _pos + dirv * 18f * px;
                _back.Triangle(b + side * 8f * px, b + dirv * (28f + len) * px, b - side * 8f * px,
                               Fade(i % 2 == 0 ? dust : dustDark, fade));
            }

            // puffs: los altos detrás, los bajos delante; suben y se hinchan
            for (int i = 0; i < puffs; i++)
            {
                bool high = i < puffs / 2;
                float spread = high ? 110f : 160f;
                var p = _pos + new Vector2((R.Next() - 0.5f) * spread * grow, (high ? 18f + R.Next() * 30f : 4f + R.Next() * 10f) * grow + t * 20f) * px;
                float r = (14f + R.Next() * 14f) * (0.5f + grow * 0.7f) * px;
                var c = i % 2 == 0 ? dust : dustDark;
                (high ? _back : _front).Disc(p, r, Fade(c, fade), Fade(c, fade * 0.7f), 18);
            }

            // dos ondas a ras de suelo: mitad lejana detrás, cercana delante
            for (int k = 0; k < 2; k++)
            {
                float tk = Seg(t, k * 0.08f, 0.7f + k * 0.1f);
                if (tk <= 0f || tk >= 1f) continue;
                float g = EaseOut(tk);
                float rx = (30f + (k == 0 ? 170f : 120f) * g) * px, ry = (6f + 18f * g) * px;
                float w = (5f * (1f - tk) + 1f) * px;
                var c = Fade(k == 0 ? white : cyan, 1f - tk);
                _back.EllipseArc(_pos, rx, ry, 0f, 0f, Mathf.PI, w, c);
                _front.EllipseArc(_pos, rx, ry, 0f, Mathf.PI, Mathf.PI * 2f, w, c);
            }

            // escombros con gravedad
            for (int i = 0; i < debris; i++)
            {
                float a = Mathf.PI * (0.15f + R.Next() * 0.7f);
                float v = 160f + R.Next() * 160f;
                float tt = t * duration;
                var p = _pos + new Vector2(Mathf.Cos(a) * v * tt, Mathf.Sin(a) * v * tt - 520f * tt * tt) * px;
                if (p.y < _pos.y) continue;
                float s = (4f + R.Next() * 5f) * px;
                float rot = tt * 12f + i;
                var o = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * s;
                var q = new Vector2(-o.y, o.x);
                (i % 2 == 0 ? _back : _front).Triangle(p + o, p + q * 0.8f, p - o - q * 0.5f, Fade(rock, fade));
            }

            // chispas
            for (int i = 0; i < sparks; i++)
            {
                float a = Mathf.PI * (0.1f + R.Next() * 0.8f), d = (20f + EaseOut(t) * (70f + R.Next() * 90f)) * px;
                var p = _pos + new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.75f);
                var tail = p - new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * 10f * px * (1f - t);
                _front.Line(tail, p, (3f * (1f - t) + 1f) * px, Fade(i % 2 == 0 ? cyan : white, 1f - t), true);
            }
        }

        // ------------------------------------------------------------------ enemigo

        private void DrawHit(float t)
        {
            float px = pixel * _power;
            float grow = EaseOut(Seg(t, 0f, 0.3f));
            var R = new Rng(57);
            float ang = Mathf.Atan2(_dir.y, _dir.x);

            float flash = 1f - Seg(t, 0f, 0.15f);
            if (flash > 0f)
            {
                _front.Glow(_pos, (36f + 30f * grow) * px, Fade(white, flash));
                _front.Disc(_pos, (12f + 10f * grow) * px, Fade(white, flash));
            }

            // anillo completo, un poco aplastado en la dirección del golpe; mitad de arriba detrás
            float rt = Seg(t, 0f, 0.6f);
            if (rt < 1f)
            {
                float r = (14f + 46f * EaseOut(rt)) * px, w = (4f * (1f - rt) + 1f) * px;
                var c = Fade(lightCyan, 1f - rt);
                _back.EllipseArc(_pos, r, r * 0.8f, 0f, 0f, Mathf.PI, w, c);
                _front.EllipseArc(_pos, r, r * 0.8f, 0f, Mathf.PI, Mathf.PI * 2f, w, c);
            }

            // esquirlas cian: triángulos finos hacia fuera, sesgados hacia la dirección del golpe
            for (int i = 0; i < spikes; i++)
            {
                float a = ang + (R.Next() - 0.5f) * Mathf.PI * 1.4f;
                var dv = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var sd = new Vector2(-dv.y, dv.x);
                float st = Seg(t, 0f, 0.55f);
                if (st >= 1f) continue;
                float d0 = (8f + 40f * EaseOut(st)) * px, len = (16f + R.Next() * 16f) * (1f - st) * px;
                var b = _pos + dv * d0;
                (dv.y > 0f ? _back : _front).Triangle(b + sd * 3f * px, b + dv * len, b - sd * 3f * px,
                    Fade(i % 3 == 0 ? white : (i % 3 == 1 ? cyan : blue), 1f - st));
            }

            for (int i = 0; i < sparks; i++)
            {
                float a = ang + (R.Next() - 0.5f) * Mathf.PI * 1.8f, d = (10f + EaseOut(t) * (40f + R.Next() * 40f)) * px;
                var p = _pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                _front.Disc(p, (2.5f * (1f - t) + 0.5f) * px, Fade(i % 2 == 0 ? white : cyan, 1f - t));
            }
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));
        private static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static float EaseIn(float t) => t * t * t;

        private struct Rng
        {
            private uint _s;
            public Rng(int seed) { _s = (uint)(seed * 2654435761u) | 1u; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
