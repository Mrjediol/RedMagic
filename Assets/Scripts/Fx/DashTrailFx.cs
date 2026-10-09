using System.Collections.Generic;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Efecto de un desplazamiento rápido (dash, picado), dibujado por código en dos lienzos
    /// (<see cref="FxCanvas"/>): uno por detrás del personaje y otro por delante. Piezas:
    /// <list type="bullet">
    /// <item><b>Salida</b>: esfera + dos anillos perpendiculares a la dirección, que adelgazan y se
    /// rompen en trazos. Mitad de detrás de cada anillo detrás del personaje.</item>
    /// <item><b>Hilo</b> blanco del origen a la cabeza.</item>
    /// <item><b>Cápsula</b> de luz de la cola a la cabeza, con rayas de velocidad. Con
    /// <see cref="replaceTarget"/> el personaje se oculta y la cápsula es su cuerpo; si no, va
    /// detrás de él como estela.</item>
    /// <item><b>Onda de choque</b> delante de la cabeza, hecha de hebras parciales, gotitas que
    /// resbalan hacia atrás, chispas y un brillo — se lee como "esto hace daño al atravesar".</item>
    /// <item><b>Llamitas</b> azules que se quedan por el camino.</item>
    /// </list>
    /// Uso: <see cref="Begin"/> al empezar (sigue al objetivo), <see cref="End"/> al llegar. Se
    /// devuelve solo al pool cuando todo se ha apagado. Medidas en "px" del boceto × <see cref="pixel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashTrailFx : MonoBehaviour
    {
        [Header("Modo")]
        [Tooltip("El personaje se oculta durante el viaje y la cápsula de luz es su cuerpo. Si no, el personaje se ve y la cápsula va detrás.")]
        [SerializeField] private bool replaceTarget = true;

        [Header("Tamaño")]
        [Tooltip("Unidades de mundo por \"px\" del boceto (todas las medidas de abajo están en px).")]
        [SerializeField] private float pixel = 0.016f;
        [Tooltip("Tamaño de la onda de choque.")]
        [SerializeField] private float shockSize = 0.8f;
        [Tooltip("Grosor de la cápsula (px).")]
        [SerializeField] private float capsuleThickness = 24f;

        [Header("Tiempos (s)")]
        [Tooltip("Lo que dura el destello de salida (esfera + anillos).")]
        [SerializeField] private float departureLife = 0.45f;
        [Tooltip("Tras llegar, lo que tarda la cola (que se queda en el origen) en recogerse hasta la cabeza.")]
        [SerializeField] private float tailCatchUp = 0.16f;
        [Tooltip("Tras llegar, lo que tarda en apagarse el hilo blanco.")]
        [SerializeField] private float threadFade = 0.35f;
        [Tooltip("Seguridad: si nadie llama a End, termina solo.")]
        [SerializeField] private float maxTravel = 1.5f;

        [Header("Partes")]
        [SerializeField] private bool departure = true;
        [SerializeField] private bool thread = true;
        [SerializeField] private bool capsule = true;
        [SerializeField] private bool speedLines = true;
        [SerializeField] private bool bowShock = true;
        [SerializeField] private bool wisps = true;

        [Header("Colores")]
        [SerializeField] private Color white = Color.white;
        [SerializeField] private Color core = new Color(1f, 0.9f, 0.92f);
        [SerializeField] private Color lightCyan = new Color(0.62f, 0.95f, 1f);
        [SerializeField] private Color cyan = new Color(0.16f, 0.76f, 1f);
        [SerializeField] private Color blue = new Color(0.11f, 0.36f, 0.86f);
        [SerializeField] private Color purple = new Color(0.46f, 0.22f, 0.86f);

        [Header("Render")]
        [SerializeField] private Material material;
        [SerializeField] private string sortingLayer = "Characters";
        [Tooltip("Orden de lo de detrás del personaje (el player está en 10).")]
        [SerializeField] private int backOrder = 2;
        [Tooltip("Orden de lo de delante del personaje.")]
        [SerializeField] private int frontOrder = 40;

        private FxCanvas _back, _front;
        private Transform _target;
        private Vector2 _offset, _origin, _head, _dir = Vector2.right;
        private float _t, _endTime = -1f, _shockScale = 1f;
        private bool _running;
        private readonly List<(float t, Vector2 p)> _path = new List<(float, Vector2)>(128);
        private readonly List<SpriteRenderer> _hidden = new List<SpriteRenderer>();
        private static readonly List<DashTrailFx> Replacing = new List<DashTrailFx>();
        private Color _headTint;
        private float _headTintAmount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Replacing.Clear();

        /// <summary>
        /// El efecto que está sustituyendo ahora mismo a <paramref name="target"/> (dash/picado en modo
        /// sustituye), o null. Lo usa el parry para teñir la bola en vez de dibujar la silueta del sprite oculto.
        /// </summary>
        public static DashTrailFx ReplacingFor(Transform target)
        {
            foreach (var fx in Replacing)
                if (fx != null && fx._target == target && fx._running && !fx.Ended) return fx;
            return null;
        }

        /// <summary>Tiñe la bola de la cabeza (borde + aura del color dado). 0 = sin tinte. Hay que llamarlo cada frame que dure.</summary>
        public void SetHeadTint(Color color, float amount)
        {
            _headTint = color;
            _headTintAmount = Mathf.Clamp01(amount);
        }

        private bool Ended => _endTime >= 0f;
        private float SinceEnd => Ended ? _t - _endTime : 0f;

        /// <summary>
        /// Empieza el efecto en la posición de <paramref name="target"/> + <paramref name="offset"/>
        /// y lo sigue hasta <see cref="End"/>. <paramref name="direction"/> es la dirección inicial
        /// (luego se toma del movimiento real).
        /// </summary>
        public void Begin(Transform target, Vector2 offset, Vector2 direction, float shockScale = 1f)
        {
            EnsureCanvases();
            RestoreTarget();
            _target = target;
            _offset = offset;
            _origin = _head = (Vector2)target.position + offset;
            _dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            _shockScale = shockScale;
            _t = 0f;
            _endTime = -1f;
            _running = true;
            _path.Clear();
            _path.Add((0f, _head));

            _headTintAmount = 0f;
            if (replaceTarget && !Replacing.Contains(this)) Replacing.Add(this);
            if (replaceTarget)
            {
                target.GetComponentsInChildren(false, _hidden);
                for (int i = _hidden.Count - 1; i >= 0; i--)
                {
                    if (!_hidden[i].enabled) { _hidden.RemoveAt(i); continue; }
                    _hidden[i].enabled = false;
                }
            }
        }

        /// <summary>El viaje ha terminado: la cola alcanza a la cabeza, el personaje reaparece.</summary>
        public void End()
        {
            if (!_running || Ended) return;
            _endTime = _t;
            Replacing.Remove(this);
            RestoreTarget();
        }

        private void OnEnable() => EnsureCanvases();

        private void OnDisable()
        {
            Replacing.Remove(this);
            OnDisableInner();
        }

        private void OnDisableInner()
        {
            RestoreTarget();
            _running = false;
            _back?.Clear(); _back?.Apply();
            _front?.Clear(); _front?.Apply();
        }

        private void OnDestroy()
        {
            RestoreTarget();
            _back?.Destroy();
            _front?.Destroy();
        }

        private void RestoreTarget()
        {
            foreach (var r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
        }

        private void EnsureCanvases()
        {
            if (_back != null) return;
            var mat = material != null ? material : new Material(Shader.Find("RedMagic/Fx Vertex Color"));
            _back = new FxCanvas(transform, "__DashBack", mat, sortingLayer, backOrder);
            _front = new FxCanvas(transform, "__DashFront", mat, sortingLayer, frontOrder);
        }

        private void LateUpdate()
        {
            if (!_running) return;
            _t += Time.deltaTime;

            if (!Ended)
            {
                if (_target == null || _t > maxTravel) End();
                else
                {
                    var p = (Vector2)_target.position + _offset;
                    var step = p - _head;
                    if (step.sqrMagnitude > 1e-6f) _dir = Vector2.Lerp(_dir, step.normalized, 0.5f).normalized;
                    _head = p;
                    _path.Add((_t, _head));
                }
            }

            Draw();

            float life = Mathf.Max(departureLife, tailCatchUp + 0.2f, threadFade, 0.75f);
            if (Ended && SinceEnd > life && _t > departureLife)
            {
                _running = false;
                _back.Clear(); _back.Apply();
                _front.Clear(); _front.Apply();
                if (Application.isPlaying) PrefabPool.Despawn(gameObject);
            }
        }

        // ------------------------------------------------------------------ dibujo

        private readonly List<Vector2> _pts = new List<Vector2>(64);
        private readonly List<float> _ws = new List<float>(64);
        private readonly List<Color> _cs = new List<Color>(64);

        private void Draw()
        {
            _back.Clear();
            _front.Clear();
            float px = pixel;

            if (departure) DrawDeparture(px);
            if (thread) DrawThread(px);

            // La cola se queda en el origen durante el viaje (la estela mide todo el recorrido) y al
            // llegar se recoge hacia la cabeza.
            float headTime = Ended ? _endTime : _t;
            float tailTime = Ended ? Mathf.Lerp(0f, _endTime, EaseIn(Seg(SinceEnd, 0f, tailCatchUp))) : 0f;
            float capAlpha = 1f - Seg(SinceEnd, tailCatchUp * 0.6f, tailCatchUp + 0.08f);

            if (capsule && capAlpha > 0f)
            {
                var canvas = replaceTarget ? _front : _back;
                float thick = (replaceTarget ? capsuleThickness : capsuleThickness * 0.75f) * px;
                DrawComet(canvas, tailTime, headTime, thick, capAlpha);
            }

            if (bowShock) DrawBowShock(px);
            if (wisps) DrawWisps(px);

            _back.Apply();
            _front.Apply();
        }

        // Anillos de pincel: trazos que adelgazan en las puntas, radio con ruido y tres pasadas
        // (resplandor morado, cian, núcleo blanco). Se rompen en trazos al apagarse.
        private void DrawDeparture(float px)
        {
            float t = _t;
            float grow = EaseOut(Seg(t, 0f, 0.12f));
            float life = Seg(t, 0.05f, departureLife);
            float alpha = 1f - EaseIn(life);
            if (alpha <= 0f) return;
            float width = Mathf.Lerp(7f, 1.6f, life) * px;
            float gaps = Seg(t, 0.12f, 0.4f);                     // 0 = entero, 1 = muy roto
            float ang = Mathf.Atan2(_dir.y, _dir.x);

            // destello inicial
            if (t < 0.1f)
            {
                float f = 1f - t / 0.1f;
                _front.Glow(_origin, 55f * px * (0.6f + grow), Fade(white, 0.8f * f));
                _back.Glow(_origin, 90f * px * (0.6f + grow), Fade(cyan, 0.35f * f));
            }

            // esfera
            float sr = (Mathf.Lerp(10f, 30f, grow) + life * 8f) * px;
            if (t < 0.07f) _front.Disc(_origin, sr, Fade(white, alpha));
            BrushArc(_back, _front, _origin, sr, sr, 0f, 0f, Mathf.PI * 2f, width, alpha, gaps, 5);

            for (int k = 0; k < 2; k++)
            {
                float r = (k == 0 ? Mathf.Lerp(30f, 72f, grow) : Mathf.Lerp(24f, 58f, grow)) * px;
                var c = _origin + _dir * ((k == 0 ? -12f : 14f) * px);
                // mitad de atrás del anillo (lado opuesto al viaje) detrás del personaje
                BrushArc(_back, _back, c, r * 0.32f, r, ang, Mathf.PI * 0.5f, Mathf.PI * 1.5f, width, alpha, gaps, 11 + k);
                BrushArc(_back, _front, c, r * 0.32f, r, ang, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, width, alpha, gaps, 21 + k);
            }
        }

        /// <summary>
        /// Arco de elipse con aspecto de pincelada: radio con ruido, grosor que respira y trazos con
        /// puntas afiladas; tres pasadas de color (morado suave en <paramref name="glow"/>, cian y
        /// blanco en <paramref name="main"/>).
        /// </summary>
        private void BrushArc(FxCanvas glow, FxCanvas main, Vector2 c, float rx, float ry, float rot,
                              float a0, float a1, float width, float alpha, float gaps, int seed)
        {
            var R = new Rng(seed);
            int dashes = gaps <= 0f ? 1 : Mathf.RoundToInt(Mathf.Lerp(3f, 9f, gaps));
            float span = a1 - a0, slot = span / dashes;
            float fill = Mathf.Lerp(1f, 0.35f, gaps);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            bool closed = dashes == 1 && span > Mathf.PI * 1.9f;
            for (int d = 0; d < dashes; d++)
            {
                float s0 = a0 + slot * d + (dashes > 1 ? R.Next() * slot * (1f - fill) : 0f);
                float s1 = s0 + slot * fill;
                for (int pass = 0; pass < 3; pass++)
                {
                    _pts.Clear(); _ws.Clear(); _cs.Clear();
                    const int seg = 16;
                    for (int i = 0; i <= seg; i++)
                    {
                        float u = i / (float)seg;
                        float a = Mathf.Lerp(s0, s1, u);
                        float wob = 1f + Mathf.Sin(a * 7f + seed) * 0.03f + Mathf.Sin(a * 13f + seed * 2f) * 0.02f;
                        var p = new Vector2(Mathf.Cos(a) * rx * wob, Mathf.Sin(a) * ry * wob);
                        _pts.Add(c + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs));
                        float taper = closed ? 1f : Mathf.Sin(Mathf.PI * u);
                        float breathe = 0.75f + 0.25f * Mathf.Sin(a * 5f + seed + _t * 10f);
                        float w = width * Mathf.Max(0.15f, taper) * breathe;
                        _ws.Add(pass == 0 ? w * 3.2f : pass == 1 ? w * 1.7f : w);
                        _cs.Add(pass == 0 ? Fade(purple, 0.35f * alpha) : pass == 1 ? Fade(cyan, 0.9f * alpha) : Fade(white, alpha));
                    }
                    (pass == 0 ? glow : main).StrokeVar(_pts, _ws, _cs, pass == 0);
                }
            }
        }

        private void DrawThread(float px)
        {
            float a = 1f - Seg(SinceEnd, 0.05f, threadFade);
            if (a <= 0f) return;
            _pts.Clear(); _ws.Clear(); _cs.Clear();
            const int seg = 12;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;
                _pts.Add(Vector2.Lerp(_origin, _head, u));
                _ws.Add((0.8f + 1.6f * u) * px);
                _cs.Add(Fade(white, a * (0.4f + 0.6f * u)));
            }
            _back.StrokeVar(_pts, _ws, _cs);
            for (int i = 0; i <= seg; i++) { _ws[i] *= 4f; _cs[i] = Fade(cyan, a * 0.35f * (i / (float)seg)); }
            _back.StrokeVar(_pts, _ws, _cs, true);
        }

        // Cometa: sigue el camino real de la cola a la cabeza; grosor creciente hacia la cabeza con
        // lenguas que se mueven, cinco capas de color de la paleta y gotas que se desprenden.
        private readonly Vector2[] _comet = new Vector2[41];

        private void DrawComet(FxCanvas canvas, float tailTime, float headTime, float thick, float alpha)
        {
            const int N = 40;
            var path = _comet;
            for (int i = 0; i <= N; i++) path[i] = PathAt(Mathf.Lerp(tailTime, headTime, i / (float)N));
            path[N] = _head;
            float len = 0f;
            for (int i = 1; i <= N; i++) len += Vector2.Distance(path[i - 1], path[i]);
            if (len < thick * 0.2f) { canvas.Glow(_head, thick, Fade(lightCyan, alpha)); return; }

            for (int L = 0; L < 5; L++)
            {
                float lw; Color lc; float la; bool soft = false;
                switch (L)
                {
                    case 0: lw = 2.8f; lc = purple; la = 0.35f; soft = true; break;
                    case 1: lw = 1.35f; lc = blue; la = 0.95f; break;
                    case 2: lw = 1.08f; lc = cyan; la = 1f; break;
                    case 3: lw = 0.74f; lc = lightCyan; la = 1f; break;
                    default: lw = 0.42f; lc = core; la = 1f; break;
                }
                _pts.Clear(); _ws.Clear(); _cs.Clear();
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N;                               // 0 cola → 1 cabeza
                    float profile = Mathf.Pow(u, 0.6f);
                    float flame = 1f + 0.22f * Mathf.Sin(u * 23f - _t * 38f + L) + 0.12f * Mathf.Sin(u * 51f + _t * 29f + L * 2f);
                    flame = Mathf.Lerp(1f, flame, 1f - u * 0.7f);         // las lenguas se notan más hacia la cola
                    _pts.Add(path[i]);
                    _ws.Add(thick * lw * profile * flame);
                    float fadeTail = Mathf.SmoothStep(0f, 1f, u * 1.6f);
                    _cs.Add(Fade(lc, alpha * la * fadeTail));
                }
                (L == 0 ? _back : canvas).StrokeVar(_pts, _ws, _cs, soft);
            }

            // tinte del parry: borde y aura del color pedido alrededor de la bola (silueta de la bola)
            if (_headTintAmount > 0f)
            {
                canvas.Glow(_head, thick * 2.6f, Fade(_headTint, 0.7f * _headTintAmount * alpha));
                canvas.Disc(_head, thick * 0.78f, Fade(_headTint, _headTintAmount * alpha));
            }

            // brillo y bola de la cabeza
            canvas.Glow(_head, thick * 1.5f, Fade(white, 0.7f * alpha));
            canvas.Disc(_head, thick * 0.55f, Fade(cyan, alpha));
            canvas.Disc(_head, thick * 0.4f, Fade(lightCyan, alpha));
            canvas.Disc(_head, thick * 0.24f, Fade(white, alpha));
            if (_headTintAmount > 0f) canvas.Disc(_head, thick * 0.55f, Fade(_headTint, 0.35f * _headTintAmount * alpha));

            // rayas de velocidad a los lados, afiladas
            if (speedLines)
            {
                var R = new Rng(17 + Mathf.FloorToInt(_t * 24f));
                for (int i = 0; i < 9; i++)
                {
                    float u = 0.35f + R.Next() * 0.6f;
                    int idx = Mathf.Clamp(Mathf.RoundToInt(u * N), 1, N);
                    var p = path[idx];
                    var tan = (path[idx] - path[idx - 1]).normalized;
                    var nrm = new Vector2(-tan.y, tan.x);
                    float off = (R.Next() - 0.5f) * thick * 3f, l = len * (0.15f + R.Next() * 0.3f);
                    var a = p + nrm * off;
                    Color c = (i % 3) switch { 0 => lightCyan, 1 => cyan, _ => purple };
                    (off > 0f ? _back : canvas).Line(a - tan * l, a + tan * thick * 0.6f, (1.4f + R.Next() * 1.8f) * pixel, Fade(c, alpha * 0.9f), true);
                }
            }

            // gotas que se desprenden de los bordes y se quedan atrás
            for (int i = 0; i < 16; i++)
            {
                var R = new Rng(400 + i * 37);
                float rate = 2.5f + R.Next() * 2f, s = Mathf.Repeat(_t * rate + R.Next(), 1f);
                float u = Mathf.Lerp(0.85f, 0.2f, s);
                int idx = Mathf.Clamp(Mathf.RoundToInt(u * N), 1, N);
                var tan = (path[idx] - path[idx - 1]).normalized;
                var nrm = new Vector2(-tan.y, tan.x);
                float side = i % 2 == 0 ? 1f : -1f;
                var pos = path[idx] + nrm * side * thick * (0.55f + s * 0.9f) * Mathf.Pow(u, 0.6f);
                Color c = (i % 3) switch { 0 => cyan, 1 => blue, _ => purple };
                (side > 0f ? _back : canvas).Teardrop(pos, Mathf.Atan2(-tan.y, -tan.x), thick * (0.6f + R.Next() * 0.5f) * (1f - s),
                    thick * 0.16f * (1f - s * 0.7f), Fade(c, alpha * (1f - s)));
            }
        }

        private void DrawBowShock(float px)
        {
            float shockIn = EaseOut(Seg(_t, 0f, 0.04f));
            float shockOut = Seg(SinceEnd, 0f, 0.12f);
            float alpha = shockIn * (1f - shockOut);
            if (alpha <= 0f) return;
            float s = shockSize * _shockScale * (1f + shockOut * 0.6f) * px;
            var u = _dir;
            var n = new Vector2(-u.y, u.x);
            // pegada a la bola de la cabeza: las hebras de dentro casi la tocan
            var nose = _head + u * ((replaceTarget ? -6f : 10f) * pixel);
            float splitY = nose.y;
            Vector2 P(float x, float y) => nose + (u * (x - 12f) + n * y) * s;

            _front.Glow(P(26f, 0f), 30f * s, Fade(white, 0.8f * alpha));
            _back.Glow(P(30f, 0f), 46f * s, Fade(cyan, 0.3f * alpha));

            // hebras: arcos parciales con puntas afiladas y grosor variable; las de fuera moradas/azules
            var R = new Rng(11);
            Color[] pal = { white, white, lightCyan, cyan, lightCyan, cyan, blue, purple, blue, purple };
            var pts = new List<Vector2>(24);
            for (int k = 0; k < pal.Length; k++)
            {
                float a = 20f + k * 3.4f + R.Next() * 3f, b = 20f + k * 2.6f, w = 24f + k * 4.6f;
                float ph = R.Next() * 6.28f, sp = 18f + R.Next() * 22f;
                float y0 = -w * (0.3f + 0.7f * (0.5f + 0.5f * Mathf.Sin(_t * sp + ph)));
                float y1 = w * (0.3f + 0.7f * (0.5f + 0.5f * Mathf.Sin(_t * sp * 1.3f + ph + 2f)));
                float width = (k < 3 ? 3.4f - k * 0.5f : 1.2f + R.Next() * 2.4f) * s;
                pts.Clear();
                for (int i = 0; i <= 18; i++)
                {
                    float y = Mathf.Lerp(y0, y1, i / 18f);
                    pts.Add(P(a - y * y / b, y));
                }
                StrokeSplitTaper(pts, width, Fade(pal[k], alpha), splitY);
            }

            for (int i = 0; i < 30; i++)
            {
                var Ri = new Rng(31 * 11 + i * 7);
                float side = i % 2 == 1 ? 1f : -1f, rate = 2.2f + Ri.Next() * 2.2f, ph = Ri.Next();
                float st = Mathf.Repeat(_t * rate + ph, 1f);
                float a = 24f + Ri.Next() * 16f, b = 22f + Ri.Next() * 10f, wMax = 34f + Ri.Next() * 20f;
                float y = side * st * wMax;
                float back = st > 0.7f ? (st - 0.7f) * 170f : 0f;
                float x = a - y * y / b - back;
                float len = (9f + Ri.Next() * 12f) * (1f - st * 0.6f) * s;
                float wid = (2.4f + Ri.Next() * 2.5f) * (1f - st * 0.7f) * s;
                float fa = alpha * (1f - st) * (st < 0.08f ? st / 0.08f : 1f);
                var localDir = new Vector2(-2f * Mathf.Abs(y) / b, side);
                var worldDir = (u * localDir.x + n * localDir.y).normalized;
                float ang = Mathf.Atan2(worldDir.y, worldDir.x);
                Color c = (i % 5) switch { 0 => white, 1 => lightCyan, 2 => cyan, 3 => blue, _ => purple };
                var pos = P(x, y);
                (pos.y >= splitY ? _back : _front).Teardrop(pos, ang, len, wid, Fade(c, fa));
            }

            for (int i = 0; i < 14; i++)
            {
                var Ri = new Rng(53 * 11 + i * 13);
                float side = i % 2 == 1 ? 1f : -1f, rate = 3f + Ri.Next() * 3f, st = Mathf.Repeat(_t * rate + Ri.Next(), 1f);
                float y = side * (10f + Ri.Next() * 42f), x = 46f - y * y / 32f;
                float d = st * (20f + Ri.Next() * 34f);
                var pos = P(x - d * 0.8f, y + side * d * 0.6f);
                (pos.y >= splitY ? _back : _front).Disc(pos, (2.4f * (1f - st) + 0.6f) * s,
                    Fade(i % 3 == 0 ? cyan : (i % 3 == 1 ? white : purple), alpha * (1f - st)));
            }
        }

        private void DrawWisps(float px)
        {
            const float spacing = 0.02f, life = 0.6f;
            float until = Ended ? _endTime + 0.15f : _t;
            int count = Mathf.Min(70, Mathf.FloorToInt(until / spacing));
            for (int i = 0; i < count; i++)
            {
                var R = new Rng(3 + i * 17);
                float birth = i * spacing + R.Next() * spacing;
                float l = (_t - birth) / life;
                if (l <= 0f || l >= 1f) continue;
                var at = PathAt(Mathf.Min(birth, Ended ? _endTime : _t));
                float a = R.Next() * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float dist = 30f * (0.3f + EaseOut(l) * (0.7f + R.Next())) * px;
                var pos = at + dir * dist + Vector2.up * (l * 22f * px);
                float sz = (3f + R.Next() * 6f) * (1f - l) * px;
                Color c = (i % 3) switch { 0 => cyan, 1 => blue, _ => purple };
                var canvas = dir.y > 0f ? _back : _front;
                canvas.Teardrop(pos, a + l * 3f, sz * 2.4f, sz, Fade(c, 1f - l));
                canvas.Glow(pos, sz * 2.2f, Fade(c, 0.35f * (1f - l)));
            }
        }

        /// <summary>Polilínea con puntas afiladas y grosor que late, repartida: por encima de splitY detrás, por debajo delante.</summary>
        private void StrokeSplitTaper(List<Vector2> pts, float width, Color color, float splitY)
        {
            int n = pts.Count;
            _pts.Clear(); _ws.Clear(); _cs.Clear();
            bool? backRun = null;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float w = width * Mathf.Max(0.1f, Mathf.Sin(Mathf.PI * u)) * (0.8f + 0.2f * Mathf.Sin(u * 9f + _t * 20f));
                bool isBack = pts[i].y >= splitY;
                if (backRun.HasValue && isBack != backRun.Value)
                {
                    _pts.Add(pts[i]); _ws.Add(w); _cs.Add(color);
                    if (_pts.Count > 1) (backRun.Value ? _back : _front).StrokeVar(_pts, _ws, _cs);
                    _pts.Clear(); _ws.Clear(); _cs.Clear();
                }
                _pts.Add(pts[i]); _ws.Add(w); _cs.Add(color);
                backRun = isBack;
            }
            if (backRun.HasValue && _pts.Count > 1) (backRun.Value ? _back : _front).StrokeVar(_pts, _ws, _cs);
        }

        private Vector2 PathAt(float time)
        {
            if (_path.Count == 0) return _head;
            if (time <= _path[0].t) return _path[0].p;
            for (int i = 1; i < _path.Count; i++)
            {
                if (_path[i].t < time) continue;
                var a = _path[i - 1]; var b = _path[i];
                return Vector2.Lerp(a.p, b.p, Mathf.InverseLerp(a.t, b.t, time));
            }
            return _path[_path.Count - 1].p;
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));
        private static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static float EaseIn(float t) => t * t * t;

        /// <summary>Aleatorio determinista por semilla (mismo patrón cada frame, como en el boceto).</summary>
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
