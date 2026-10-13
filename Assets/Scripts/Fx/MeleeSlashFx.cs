using System;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Tajo melee procedural: varias capas de media luna (shader <c>RedMagic/Melee Slash Layer</c>)
    /// que barren el mismo arco con retardo, grosor y radio distintos — núcleo blanco, lenguas cian,
    /// estela morada. Todo el look vive en los campos de este componente (una variante = un prefab).
    ///
    /// Independiente de quién ataca: se coloca en el origen del golpe, se rota hacia la dirección y
    /// <see cref="Play"/> lo dispara. <c>power</c> escala tamaño y grosor (mejoras de build).
    /// Pooling: sale de <see cref="PrefabPool"/>, se reinicia en <c>OnEnable</c> y se devuelve solo.
    /// Las capas son hijos ocultos creados en código (no se serializan) para que editar la lista en
    /// el Inspector se vea al instante, también en modo edición con <see cref="previewLoop"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MeleeSlashFx : MonoBehaviour
    {
        public enum ShardStyle { Rombo = 0, Rayas = 1, Chispas = 2, Trozos = 3 }

        [Serializable]
        public sealed class Layer
        {
            public string label = "Capa";
            public Color color = Color.white;
            [Tooltip("Radio del borde exterior (1 = worldRadius).")]
            [Range(0.1f, 1f)] public float radius = 0.85f;
            [Tooltip("Grosor máximo de la media luna, en la misma escala que el radio.")]
            [Range(0f, 0.8f)] public float thickness = 0.25f;
            [Tooltip("Retardo respecto al inicio del tajo, en fracción de la duración.")]
            [Range(0f, 0.6f)] public float delay;
            [Tooltip("Cuánto se afila hacia la cabeza (bajo = cabeza roma).")]
            [Range(0.05f, 3f)] public float headTaper = 0.35f;
            [Tooltip("Cuánto se afila hacia la cola (alto = cola larga y fina).")]
            [Range(0.05f, 4f)] public float tailTaper = 1.6f;
            [Tooltip("Deshilachado de la cola en lenguas (0 = cola limpia).")]
            [Range(0f, 1f)] public float fraySpread = 0.25f;
            [Range(1f, 20f)] public float frayBands = 6f;
            [Range(0f, 0.5f)] public float outerJag = 0.05f;
            [Range(0f, 1f)] public float innerJag = 0.2f;
            [Tooltip("Huecos que se abren por donde la capa ya ha pasado.")]
            [Range(0f, 1f)] public float breakup = 0.3f;
            [Tooltip("Esquirlas sueltas fuera del borde (0 = ninguna).")]
            [Range(0, 40)] public int shards;
            [Range(0f, 1f)] public float shardDrift = 0.25f;
            [Tooltip("Rombo: esquirlas sueltas · Rayas: líneas de velocidad pegadas al borde · Chispas: salen despedidas de la cabeza · Trozos: se desprenden de la cola.")]
            public ShardStyle shardStyle = ShardStyle.Rombo;
            public float seed;
        }

        [Header("Forma")]
        [Tooltip("Radio del tajo en unidades de mundo con power = 1.")]
        [SerializeField] private float worldRadius = 2.5f;
        [Tooltip("Apertura del arco en grados.")]
        [SerializeField, Range(30f, 340f)] private float arcDegrees = 220f;
        [Tooltip("Ángulo donde empieza el barrido (0 = derecha, 90 = arriba), antes de rotar el objeto.")]
        [SerializeField] private float startAngle = 110f;
        [Tooltip("Barrido en sentido horario (si no, antihorario).")]
        [SerializeField] private bool clockwise = true;
        [Tooltip("Se abre desde el centro hacia los dos lados (salpicaduras, anillos). Entonces Start Angle es el centro del arco.")]
        [SerializeField] private bool symmetric;
        [Tooltip("Escala X/Y de la forma antes de rotar (1,1 = círculo). Se multiplica por el stretch de Play.")]
        [SerializeField] private Vector2 shapeStretch = Vector2.one;

        [Header("Tiempo")]
        [Tooltip("Duración total de una capa, en segundos.")]
        [SerializeField] private float duration = 0.32f;
        [Tooltip("Fracción de la duración en la que la cabeza recorre el arco.")]
        [SerializeField, Range(0.1f, 1f)] private float headPortion = 0.45f;
        [Tooltip("Fracción en la que la cola empieza a seguir a la cabeza.")]
        [SerializeField, Range(0f, 0.9f)] private float tailStart = 0.2f;
        [Tooltip("Fracción final en la que la capa se desvanece.")]
        [SerializeField, Range(0f, 0.6f)] private float fadePortion = 0.2f;

        [Header("Capas (de atrás hacia delante)")]
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();

        [Header("Render")]
        [SerializeField] private Shader shader;
        [SerializeField] private string sortingLayer = "Characters";
        [Tooltip("Orden de la capa de más atrás; cada capa siguiente suma 1.")]
        [SerializeField] private int baseSortingOrder = 40;

        [Header("Profundidad")]
        [Tooltip("Parte el efecto en dos: lo de debajo de Split Y se dibuja delante (Base Sorting Order) y lo de encima detrás (Back Sorting Order). Así un anillo en los pies rodea al personaje.")]
        [SerializeField] private bool depthSplit;
        [Tooltip("Altura del corte en radios del efecto, antes de rotar (0 = centro; -0.5 = más abajo).")]
        [SerializeField, Range(-1.5f, 1.5f)] private float splitY;
        [Tooltip("Orden de la mitad de detrás (por debajo del personaje: el player está en 10).")]
        [SerializeField] private int backSortingOrder = 2;

        [Header("Preview")]
        [Tooltip("Repite el tajo en bucle (escena de prueba / modo edición). En juego, false.")]
        [SerializeField] private bool previewLoop;
        [SerializeField] private float previewGap = 0.5f;

        private const string ChildName = "__SlashLayer";
        private static Mesh _quad;

        private MeshRenderer[] _renderers;
        private Material[] _materials;
        private float _time;
        private float _power = 1f;
        private bool _mirrorX, _mirrorY;
        private Vector2 _stretch = Vector2.one;
        private bool _interrupted;
        private float _interruptLeft, _interruptDuration = 1f;
        private bool _playing;
        private double _lastEditorTime;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int ArcStartId = Shader.PropertyToID("_ArcStart");
        private static readonly int ArcSpanId = Shader.PropertyToID("_ArcSpan");
        private static readonly int HeadId = Shader.PropertyToID("_Head");
        private static readonly int TailId = Shader.PropertyToID("_Tail");
        private static readonly int HeadTaperId = Shader.PropertyToID("_HeadTaper");
        private static readonly int TailTaperId = Shader.PropertyToID("_TailTaper");
        private static readonly int FraySpreadId = Shader.PropertyToID("_FraySpread");
        private static readonly int FrayFreqId = Shader.PropertyToID("_FrayFreq");
        private static readonly int OuterJagId = Shader.PropertyToID("_OuterJag");
        private static readonly int InnerJagId = Shader.PropertyToID("_InnerJag");
        private static readonly int BreakupId = Shader.PropertyToID("_Breakup");
        private static readonly int ShardsId = Shader.PropertyToID("_Shards");
        private static readonly int ShardDriftId = Shader.PropertyToID("_ShardDrift");
        private static readonly int ShardStyleId = Shader.PropertyToID("_ShardStyle");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int SymmetricId = Shader.PropertyToID("_Symmetric");
        private static readonly int DepthSideId = Shader.PropertyToID("_DepthSide");
        private static readonly int SplitYId = Shader.PropertyToID("_SplitY");

        /// <summary>Un renderer por capa, o dos (delante + detrás) con <see cref="depthSplit"/>.</summary>
        private int RendererCount => depthSplit ? layers.Length * 2 : layers.Length;

        /// <summary>Duración total del tajo (última capa incluida), en segundos.</summary>
        public float TotalDuration
        {
            get
            {
                float maxDelay = 0f;
                foreach (var l in layers) maxDelay = Mathf.Max(maxDelay, l.delay);
                return duration * (1f + maxDelay);
            }
        }

        /// <summary>Reinicia el tajo. <paramref name="power"/> 1 = normal; &gt;1 más grande y grueso.</summary>
        public void Play(float power = 1f) => Play(power, false, false);

        /// <summary>
        /// Reinicia el tajo espejado: <paramref name="mirrorX"/> para quien mira a la izquierda,
        /// <paramref name="mirrorY"/> invierte el sentido del barrido (un tajo descendente pasa a ascendente).
        /// La rotación la pone quien lo lanza en el transform.
        /// </summary>
        public void Play(float power, bool mirrorX, bool mirrorY) => Play(power, mirrorX, mirrorY, Vector2.one);

        /// <summary>
        /// Como el anterior, con <paramref name="stretch"/>: escala X/Y del arco antes de rotar
        /// (p. ej. (1.6, 0.7) = barrido horizontal ancho y aplanado). La zona de daño se deforma igual.
        /// </summary>
        public void Play(float power, bool mirrorX, bool mirrorY, Vector2 stretch)
        {
            _power = Mathf.Max(0.1f, power);
            _mirrorX = mirrorX;
            _mirrorY = mirrorY;
            _stretch = new Vector2(Mathf.Max(0.1f, stretch.x), Mathf.Max(0.1f, stretch.y));
            _interrupted = false;
            _time = 0f;
            _playing = true;
            ApplyScale();
            UpdateLayers();
        }

        /// <summary>
        /// Corta el tajo: deja de hacer daño y se desvanece en <paramref name="fade"/> segundos.
        /// Lo usa el combo al sacar el siguiente golpe, para que no haya dos tajos a la vez.
        /// </summary>
        public void Interrupt(float fade = 0.06f)
        {
            if (!_playing || _interrupted) return;
            _interrupted = true;
            _interruptDuration = Mathf.Max(0.001f, fade);
            _interruptLeft = _interruptDuration;
        }

        /// <summary>Radio exterior del tajo en mundo con la potencia actual.</summary>
        public float CurrentWorldRadius => worldRadius * _power * MaxLayerRadius;

        /// <summary>Fracción del arco que la cabeza ya ha barrido (0..1).</summary>
        public float HeadProgress => _playing ? HeadAt(_time / Mathf.Max(duration, 1e-4f)) : 0f;

        /// <summary>True mientras la cabeza todavía avanza: la ventana de daño natural del tajo.</summary>
        public bool IsSweeping => _playing && !_interrupted && _time < duration * headPortion;

        /// <summary>
        /// ¿Cae <paramref name="worldPoint"/> dentro de lo que el tajo ya ha barrido? Misma
        /// geometría que el shader (arco, radio y grosor de las capas, espejos y rotación del
        /// transform), así que la zona de daño coincide con lo que se ve.
        /// </summary>
        public bool Covers(Vector2 worldPoint, float radialMargin = 0.1f)
        {
            if (!_playing) return false;
            Vector2 p = transform.InverseTransformPoint(worldPoint);   // en radios (1 = worldRadius·power)
            float r = p.magnitude;
            float outer = MaxLayerRadius + radialMargin;
            float inner = Mathf.Max(0f, MinInnerRadius - radialMargin - 0.25f);
            if (r > outer || r < inner) return false;

            float start = clockwise ? 180f - startAngle : startAngle;
            float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg - start;
            ang = Mathf.Repeat(ang, 360f);
            if (symmetric) return Mathf.Abs(Mathf.DeltaAngle(0f, ang)) / (arcDegrees * 0.5f) <= HeadProgress + 0.02f;
            float t = ang / arcDegrees;
            return t <= HeadProgress + 0.02f;
        }

        private float MaxLayerRadius
        {
            get { float m = 0f; foreach (var l in layers) m = Mathf.Max(m, l.radius); return m > 0f ? m : 1f; }
        }

        private float MinInnerRadius
        {
            get { float m = 1f; foreach (var l in layers) m = Mathf.Min(m, l.radius - l.thickness); return m; }
        }

        private float HeadAt(float local)
        {
            float headT = Mathf.Clamp01(local / headPortion);
            return 1f - (1f - headT) * (1f - headT) * (1f - headT);
        }

        private void OnEnable()
        {
            // Una sola vez: el efecto sale del pool y reconstruir aquí creaba GameObjects y Materiales en cada golpe.
            if (_renderers == null || _renderers.Length != RendererCount || _renderers[0] == null) BuildLayers();
            _lastEditorTime = Now();
            Play(_power);
        }

        private void OnDisable() => _playing = false;

        private void OnDestroy() => DestroyLayers();

        private void OnValidate()
        {
            // Cambiar el nº de capas en el Inspector reconstruye en el siguiente Update.
            if (_renderers != null && _renderers.Length != RendererCount) _renderers = null;
        }

        private void Update()
        {
            if (_renderers == null || _renderers.Length != RendererCount) BuildLayers();
#if UNITY_EDITOR
            // En modo edición Update sólo corre si algo cambia: se pide el siguiente frame.
            if (!Application.isPlaying && (_playing || previewLoop))
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif

            float dt;
            if (Application.isPlaying) dt = Time.deltaTime;
            else
            {
                double now = Now();
                dt = Mathf.Min((float)(now - _lastEditorTime), 0.1f);
                _lastEditorTime = now;
            }

            if (!_playing)
            {
                if (!previewLoop) return;
                _time += dt;
                if (_time >= previewGap) Play(_power);
                return;
            }

            _time += dt;
            ApplyScale();
            UpdateLayers();

            if (_interrupted) _interruptLeft -= dt;
            if (_time >= TotalDuration || (_interrupted && _interruptLeft <= 0f))
            {
                _playing = false;
                _time = 0f;
                SetAlpha(0f);
                if (!previewLoop && Application.isPlaying) PrefabPool.Despawn(gameObject);
            }
        }

        private void ApplyScale()
        {
            float s = worldRadius * _power;
            float sx = (clockwise ? -s : s) * (_mirrorX ? -1f : 1f);
            transform.localScale = new Vector3(sx * _stretch.x * shapeStretch.x, (_mirrorY ? -s : s) * _stretch.y * shapeStretch.y, 1f);
        }

        private void UpdateLayers()
        {
            if (_materials == null) return;
            // Con escala X negativa el quad se espeja: el ángulo de inicio se espeja también para
            // que startAngle siga significando lo mismo en pantalla.
            float start = (clockwise ? 180f - startAngle : startAngle) * Mathf.Deg2Rad;
            float span = (symmetric ? arcDegrees * 0.5f : arcDegrees) * Mathf.Deg2Rad;
            float thicknessMul = Mathf.Lerp(1f, _power, 0.5f);

            for (int k = 0; k < _materials.Length; k++)
            {
                int i = k % layers.Length;
                bool back = k >= layers.Length;
                var l = layers[i];
                var m = _materials[k];
                float local = duration > 0f ? (_time / duration) - l.delay : 1f;


                float head = HeadAt(local);                                      // ease-out cúbico
                float tailT = Mathf.Clamp01((local - tailStart) / Mathf.Max(1f - tailStart, 1e-3f));
                float tail = tailT * tailT;                                      // ease-in
                float fade = fadePortion > 0f ? Mathf.Clamp01((1f - local) / fadePortion) : 1f;
                bool visible = local > 0f && local < 1f;

                m.SetColor(ColorId, l.color);
                m.SetFloat(RadiusId, l.radius);
                m.SetFloat(ThicknessId, Mathf.Min(l.thickness * thicknessMul, l.radius));
                m.SetFloat(ArcStartId, start);
                m.SetFloat(ArcSpanId, span);
                m.SetFloat(HeadId, head);
                m.SetFloat(TailId, tail);
                m.SetFloat(HeadTaperId, l.headTaper);
                m.SetFloat(TailTaperId, l.tailTaper);
                m.SetFloat(FraySpreadId, l.fraySpread);
                m.SetFloat(FrayFreqId, l.frayBands);
                m.SetFloat(OuterJagId, l.outerJag);
                m.SetFloat(InnerJagId, l.innerJag);
                m.SetFloat(BreakupId, l.breakup);
                m.SetFloat(ShardsId, l.shards);
                m.SetFloat(ShardDriftId, l.shardDrift);
                m.SetFloat(ShardStyleId, (float)l.shardStyle);
                m.SetFloat(SeedId, l.seed);
                float cut = _interrupted ? Mathf.Clamp01(_interruptLeft / _interruptDuration) : 1f;
                m.SetFloat(AlphaId, visible ? fade * cut : 0f);
                m.SetFloat(SymmetricId, symmetric ? 1f : 0f);
                m.SetFloat(DepthSideId, depthSplit ? (back ? -1f : 1f) : 0f);
                m.SetFloat(SplitYId, splitY);
            }
        }

        private void SetAlpha(float a)
        {
            if (_materials == null) return;
            foreach (var m in _materials) if (m != null) m.SetFloat(AlphaId, a);
        }

        private void BuildLayers()
        {
            DestroyLayers();
            var sh = shader != null ? shader : Shader.Find("RedMagic/Melee Slash Layer");
            if (sh == null) return;

            if (layers.Length == 0) { _renderers = Array.Empty<MeshRenderer>(); _materials = Array.Empty<Material>(); return; }
            _renderers = new MeshRenderer[RendererCount];
            _materials = new Material[RendererCount];
            for (int k = 0; k < RendererCount; k++)
            {
                var go = new GameObject(ChildName) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                mr.sharedMaterial = mat;
                mr.sortingLayerName = sortingLayer;
                int i = k % layers.Length;
                mr.sortingOrder = (k >= layers.Length ? backSortingOrder : baseSortingOrder) + i;
                _renderers[k] = mr;
                _materials[k] = mat;
            }
        }

        private void DestroyLayers()
        {
            // Tras recompilar en modo edición se pierden las referencias pero los hijos ocultos
            // siguen ahí: se limpian por nombre.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name == ChildName) Kill(c.gameObject);
            }
            if (_materials != null) foreach (var m in _materials) if (m != null) Kill(m);
            _renderers = null;
            _materials = null;
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private static double Now()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.timeAsDouble;
        }

        private static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "SlashQuad", hideFlags = HideFlags.HideAndDontSave };
                _quad.vertices = new[]
                {
                    new Vector3(-1.5f, -1.5f), new Vector3(1.5f, -1.5f), new Vector3(-1.5f, 1.5f), new Vector3(1.5f, 1.5f)
                };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
