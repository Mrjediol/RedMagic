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
        [Tooltip("Retraso de la cola de la cápsula respecto a la cabeza.")]
        [SerializeField] private float tailLag = 0.08f;
        [Tooltip("Tras llegar, lo que tarda la cola en alcanzar a la cabeza.")]
        [SerializeField] private float tailCatchUp = 0.12f;
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
            RestoreTarget();
        }

        private void OnEnable() => EnsureCanvases();

        private void OnDisable()
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

        private void Draw()
        {
            _back.Clear();
            _front.Clear();
            float px = pixel;

            if (departure) DrawDeparture(px);

            if (thread)
            {
                float a = 1f - Seg(SinceEnd, 0.05f, threadFade);
                _back.Line(_origin, _head, 2f * px, Fade(white, a));
            }

            // cola de la cápsula: posición del camino hace "lag" segundos
            float tailTime = Ended ? Mathf.Lerp(_endTime - tailLag, _endTime, Seg(SinceEnd, 0f, tailCatchUp)) : _t - tailLag;
            Vector2 tail = PathAt(Mathf.Max(0f, tailTime));
            float capAlpha = 1f - Seg(SinceEnd, 0.05f, 0.16f);

            if (capsule && capAlpha > 0f)
            {
                var canvas = replaceTarget ? _front : _back;
                float thick = (replaceTarget ? capsuleThickness : capsuleThickness * 0.75f) * px;
                DrawCapsule(canvas, tail, _head, thick, capAlpha);
            }

            if (bowShock) DrawBowShock(px);
            if (wisps) DrawWisps(px);

            _back.Apply();
            _front.Apply();
        }

        private void DrawDeparture(float px)
        {
            float t = _t;
            float grow = EaseOut(Seg(t, 0f, 0.12f));
            float life = Seg(t, 0.05f, departureLife);
            float alpha = 1f - EaseIn(life);
            if (alpha <= 0f) return;
            float width = Mathf.Lerp(7f, 1.2f, life) * px;
            float dash = Seg(t, 0.15f, 0.4f);
            float on = dash > 0f ? Mathf.Max(2f, 14f * (1f - dash)) * px : 0f;
            float off = dash > 0f ? (6f + 26f * dash) * px : 0f;
            var w = Fade(white, alpha);
            float ang = Mathf.Atan2(_dir.y, _dir.x);

            // esfera (delante)
            float sr = (Mathf.Lerp(10f, 30f, grow) + life * 8f) * px;
            if (t < 0.08f) _front.Disc(_origin, sr, w);
            _front.EllipseArc(_origin, sr, sr, 0f, 0f, Mathf.PI * 2f, width, w, on, off);

            // dos anillos perpendiculares: mitad de atrás (lado opuesto al viaje) detrás del personaje
            for (int k = 0; k < 2; k++)
            {
                float r = (k == 0 ? Mathf.Lerp(30f, 70f, grow) : Mathf.Lerp(24f, 56f, grow)) * px;
                var c = _origin + _dir * ((k == 0 ? -12f : 14f) * px);
                _back.EllipseArc(c, r * 0.32f, r, ang, Mathf.PI * 0.5f, Mathf.PI * 1.5f, width, w, on, off);
                _front.EllipseArc(c, r * 0.32f, r, ang, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, width, w, on, off);
            }
        }

        private void DrawCapsule(FxCanvas canvas, Vector2 tail, Vector2 head, float thick, float alpha)
        {
            var d = head - tail;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            var u = d / len;
            var n = new Vector2(-u.y, u.x);

            canvas.StrokeSoft(tail, head + u * thick * 0.3f, thick * 2.4f, Fade(cyan, 0.45f * alpha));

            if (speedLines)
            {
                var R = new Rng(17 + Mathf.FloorToInt(_t * 20f));
                for (int i = 0; i < 7; i++)
                {
                    float off = (R.Next() - 0.5f) * thick * 2.6f;
                    float l0 = R.Next() * len * 0.5f, l1 = Mathf.Min(l0 + len * (0.3f + R.Next() * 0.6f), len + thick);
                    var c = i % 2 == 0 ? lightCyan : cyan;
                    (i < 4 ? canvas : _back).Line(tail + u * l0 + n * off, tail + u * l1 + n * off,
                        (1.5f + R.Next() * 2f) * pixel, Fade(c, alpha), true);
                }
            }

            canvas.Line(tail, head, thick, Fade(cyan, alpha), false);
            canvas.Line(tail + u * thick * 0.1f, head, thick * 0.72f, Fade(lightCyan, alpha), false);
            canvas.Line(tail + u * thick * 0.2f, head, thick * 0.42f, Fade(core, alpha), false);
            canvas.Disc(head, thick * 0.5f, Fade(cyan, alpha));
            canvas.Disc(head, thick * 0.36f, Fade(lightCyan, alpha));
            canvas.Disc(head, thick * 0.21f, Fade(core, alpha));
            canvas.Line(tail + u * len * 0.1f, head + u * thick * 0.4f, 2f * pixel, Fade(white, alpha));
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
            var nose = _head + u * ((replaceTarget ? 8f : 16f) * pixel);
            float splitY = nose.y;
            Vector2 P(float x, float y) => nose + (u * x + n * y) * s;

            _front.Glow(P(30f, 0f), 26f * s, Fade(white, 0.85f * alpha));

            // hebras: arcos parciales de una familia de parábolas x = a - y²/b
            var R = new Rng(11);
            Color[] pal = { white, lightCyan, cyan, blue, lightCyan, white, cyan };
            var pts = new List<Vector2>(24);
            for (int k = 0; k < 9; k++)
            {
                float a = 22f + k * 3.2f + R.Next() * 3f, b = 22f + k * 2.4f, w = 26f + k * 4.5f;
                float ph = R.Next() * 6.28f, sp = 18f + R.Next() * 22f;
                float y0 = -w * (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(_t * sp + ph)));
                float y1 = w * (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(_t * sp * 1.3f + ph + 2f)));
                float width = (k < 3 ? 3f - k * 0.6f : 1f + R.Next() * 2.2f) * s;
                pts.Clear();
                for (int i = 0; i <= 18; i++)
                {
                    float y = Mathf.Lerp(y0, y1, i / 18f);
                    pts.Add(P(a - y * y / b, y));
                }
                StrokeSplit(pts, width, Fade(pal[k % pal.Length], alpha), splitY);
            }

            // gotitas que nacen en la nariz y resbalan por la envolvente hacia atrás
            for (int i = 0; i < 26; i++)
            {
                var Ri = new Rng(31 * 11 + i * 7);
                float side = i % 2 == 1 ? 1f : -1f, rate = 2.2f + Ri.Next() * 2.2f, ph = Ri.Next();
                float st = Mathf.Repeat(_t * rate + ph, 1f);
                float a = 26f + Ri.Next() * 16f, b = 24f + Ri.Next() * 10f, wMax = 34f + Ri.Next() * 18f;
                float y = side * st * wMax;
                float back = st > 0.7f ? (st - 0.7f) * 160f : 0f;
                float x = a - y * y / b - back;
                float len = (9f + Ri.Next() * 10f) * (1f - st * 0.6f) * s;
                float wid = (2.4f + Ri.Next() * 2.5f) * (1f - st * 0.7f) * s;
                float fa = alpha * (1f - st) * (st < 0.08f ? st / 0.08f : 1f);
                // dirección local de la gota: hacia atrás a lo largo de la curva
                var localDir = new Vector2(-2f * Mathf.Abs(y) / b, side);
                var worldDir = (u * localDir.x + n * localDir.y).normalized;
                float ang = Mathf.Atan2(worldDir.y, worldDir.x);        // la gota avanza hacia atrás por la curva
                Color c = (i % 4) switch { 0 => white, 1 => lightCyan, 2 => cyan, _ => blue };
                var pos = P(x, y);
                (pos.y >= splitY ? _back : _front).Teardrop(pos, ang, len, wid, Fade(c, fa));
            }

            // chispas que saltan del borde
            for (int i = 0; i < 12; i++)
            {
                var Ri = new Rng(53 * 11 + i * 13);
                float side = i % 2 == 1 ? 1f : -1f, rate = 3f + Ri.Next() * 3f, st = Mathf.Repeat(_t * rate + Ri.Next(), 1f);
                float y = side * (10f + Ri.Next() * 40f), x = 46f - y * y / 32f;
                float d = st * (20f + Ri.Next() * 30f);
                var pos = P(x - d * 0.8f, y + side * d * 0.6f);
                (pos.y >= splitY ? _back : _front).Disc(pos, (2.2f * (1f - st) + 0.6f) * s, Fade(i % 3 == 0 ? cyan : white, alpha * (1f - st)));
            }
        }

        private void DrawWisps(float px)
        {
            const float spacing = 0.022f, life = 0.55f;
            float until = Ended ? _endTime + 0.15f : _t;
            int count = Mathf.Min(60, Mathf.FloorToInt(until / spacing));
            for (int i = 0; i < count; i++)
            {
                var R = new Rng(3 + i * 17);
                float birth = i * spacing + R.Next() * spacing;
                float l = (_t - birth) / life;
                if (l <= 0f || l >= 1f) continue;
                var at = PathAt(Mathf.Min(birth, Ended ? _endTime : _t));
                float a = R.Next() * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float dist = 34f * (0.3f + EaseOut(l) * (0.7f + R.Next())) * px;
                var pos = at + dir * dist + Vector2.up * (l * 18f * px);
                float sz = (3f + R.Next() * 6f) * (1f - l) * px;
                var c = i % 3 == 0 ? cyan : blue;
                (dir.y > 0f ? _back : _front).Ellipse(pos, sz * 1.6f, sz, a, Fade(c, 1f - l), Fade(c, (1f - l) * 0.6f), 10);
            }
        }

        /// <summary>Traza una polilínea repartiendo sus tramos: por encima de splitY detrás, por debajo delante.</summary>
        private void StrokeSplit(List<Vector2> pts, float width, Color color, float splitY)
        {
            var run = FxCanvas.Points();
            bool? backRun = null;
            for (int i = 0; i < pts.Count; i++)
            {
                bool isBack = pts[i].y >= splitY;
                if (backRun.HasValue && isBack != backRun.Value)
                {
                    run.Add(pts[i]);
                    (backRun.Value ? _back : _front).Stroke(run, width, color);
                    var last = pts[i];
                    run = FxCanvas.Points();
                    run.Add(last);
                }
                else run.Add(pts[i]);
                backRun = isBack;
            }
            if (backRun.HasValue && run.Count > 1) (backRun.Value ? _back : _front).Stroke(run, width, color);
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
