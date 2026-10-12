using UnityEngine;

namespace RedMagic.Levels
{
    public enum ParallaxVerticalAnchor
    {
        [Tooltip("Fondo: la imagen se reparte por la altura del nivel (arriba del todo = parte alta de la imagen).")]
        Proportional,
        [Tooltip("Cuelga del techo del nivel y se mueve con el mundo (estalactitas).")]
        Ceiling,
        [Tooltip("Se apoya en el suelo del nivel y se mueve con el mundo (primer plano, pared, puerta).")]
        Floor,
    }

    public enum ParallaxAlign { Top, Center, Bottom }

    public enum ParallaxRepeat
    {
        [Tooltip("Se repite a lo ancho.")] Repeat,
        [Tooltip("Se repite a lo ancho, una copia sí y otra en espejo (oculta costuras).")] Mirror,
        [Tooltip("Una vez al principio del nivel y en espejo al final (pared y puerta).")] LevelEnds,
    }

    /// <summary>
    /// Una capa del fondo, con la misma lógica que la página de prueba <c>Web/ParallaxTest/parallax-test.html</c>
    /// (de donde se importan los valores). El tamaño y las alturas son relativos al alto de la pantalla; las
    /// velocidades, al movimiento de la cámara por el nivel. Necesita un <see cref="Level"/> en un padre: de él
    /// salen el ancho y el alto que usa el anclaje proporcional y los extremos.
    ///
    /// Se dibuja con una malla propia (no SpriteRenderer) para poder repetir en espejo y atenuar hacia el color de
    /// la niebla desde el Inspector. Se ejecuta después de <c>CameraFollow</c>.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public class ParallaxBackgroundLayer : MonoBehaviour
    {
        [Tooltip("Imagen de la capa (con transparencia).")]
        [SerializeField] private Texture2D texture;
        [SerializeField] private Shader shader;

        [Header("Horizontal")]
        [Tooltip("Fracción del movimiento de la cámara que sigue la capa. 0 = quieta (lejos), 1 = como el mundo, >1 = primer plano.")]
        [SerializeField] private float speed = 0.1f;
        [SerializeField] private ParallaxRepeat repeat = ParallaxRepeat.Repeat;
        [Tooltip("Desplazamiento horizontal en unidades. En 'extremos del nivel' la derecha se mueve en espejo.")]
        [SerializeField] private float positionX;

        [Header("Vertical")]
        [Tooltip("Alto de la imagen, en % del alto de la pantalla.")]
        [Range(5f, 600f)] [SerializeField] private float size = 100f;
        [Tooltip("Subir/bajar la imagen, en % de media pantalla (positivo = arriba).")]
        [Range(-100f, 100f)] [SerializeField] private float heightOffset;
        [SerializeField] private ParallaxVerticalAnchor anchor = ParallaxVerticalAnchor.Proportional;
        [SerializeField] private ParallaxAlign align = ParallaxAlign.Center;
        [Tooltip("Proporcional: cuánto de la imagen se recorre de arriba abajo del nivel (1 = entera).")]
        [Range(0f, 1.5f)] [SerializeField] private float travel = 1f;
        [Tooltip("Techo/suelo: fracción del movimiento vertical de la cámara (≈0.9 = casi pegada al mundo).")]
        [Range(0f, 1.2f)] [SerializeField] private float verticalSpeed = 0.9f;

        [Header("Aspecto")]
        [Range(0f, 1f)] [SerializeField] private float opacity = 1f;
        [Tooltip("Mezcla con el color de la niebla: aleja la capa y evita que se confunda con el terreno.")]
        [Range(0f, 0.9f)] [SerializeField] private float fade;
        [SerializeField] private Color fogColor = new Color32(18, 52, 92, 255);
        [Tooltip("Delante del jugador (capa Characters) en vez de en el fondo.")]
        [SerializeField] private bool inFront;
        [Tooltip("Encima del terreno pero detrás del jugador (puertas, objetos apoyados en el suelo).")]
        [SerializeField] private bool overTerrain;
        [Tooltip("Sólo en 'extremos del nivel': la base se coloca sola sobre el suelo del nivel en cada extremo (Altura = ajuste fino).")]
        [SerializeField] private bool sitOnGround;
        [SerializeField] private int sortingOrder;

        [Header("Sombra (sólo en 'extremos del nivel')")]
        [Tooltip("Sombra de la propia silueta, desplazada y difuminada, sobre lo que hay detrás (la pared).")]
        [SerializeField] private bool castShadow;
        [SerializeField] private Color shadowColor = new Color(0.01f, 0.04f, 0.12f, 0.55f);
        [Tooltip("Desplazamiento de la sombra en unidades. x negativo = hacia el borde del nivel (la luz viene del centro).")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(-0.55f, 0.1f);
        [Tooltip("Difuminado de la sombra, en unidades.")]
        [Range(0f, 1.5f)] [SerializeField] private float shadowSoftness = 0.35f;
        [Tooltip("Sombra de contacto en el suelo (elipse bajo la base). 0 = sin ella.")]
        [Range(0f, 2f)] [SerializeField] private float groundShadowWidth = 1.3f;
        [Range(0f, 1.5f)] [SerializeField] private float groundShadowHeight = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float groundShadowOpacity = 0.6f;



        /// <summary>'Extremos del nivel' y dibujada este frame: esquina inferior izquierda de la copia izquierda.</summary>
        public Vector2 LeftEnd { get; private set; }
        /// <summary>Esquina inferior derecha de la copia derecha (la de espejo).</summary>
        public Vector2 RightEnd { get; private set; }
        public bool HasEnds { get; private set; }
        public string SortingLayerName { get; private set; } = LevelSorting.Layer;
        public int SortingOrder { get; private set; }

        private MeshRenderer _main, _mirror, _shadow, _shadowMirror, _ground, _groundMirror;
        private MaterialPropertyBlock _block;
        private Material _material, _shadowMaterial;
        private Level _level;
        private static Mesh _quad;

        /// <summary>Encima del terreno y con sombras en pared y suelo: lo que necesita una puerta.</summary>
        public void ConfigureAsDoor()
        {
            overTerrain = true; inFront = false; castShadow = true; sitOnGround = true; heightOffset = 0f;
            // Sombra nítida hacia el interior del nivel, sobre la pared (no un halo alrededor de la puerta).
            shadowColor = new Color(0.01f, 0.04f, 0.12f, 0.5f); shadowOffset = new Vector2(-0.55f, 0.1f); shadowSoftness = 0.12f;
            groundShadowWidth = 1.6f; groundShadowHeight = 0.5f; groundShadowOpacity = 0.35f;
        }

        public void Configure(Texture2D tex, Shader sh, float spd, ParallaxRepeat rep, float posX, float sz, float hOffset,
                              ParallaxVerticalAnchor anc, ParallaxAlign al, float trav, float vSpeed,
                              float op, float fd, bool front, int order)
        {
            texture = tex; shader = sh; speed = spd; repeat = rep; positionX = posX; size = sz; heightOffset = hOffset;
            anchor = anc; align = al; travel = trav; verticalSpeed = vSpeed; opacity = op; fade = fd; inFront = front;
            sortingOrder = order;
        }

        private void OnEnable()
        {
            _block ??= new MaterialPropertyBlock();
            _main = Part("Capa");
            _mirror = Part("Capa (espejo)");
            _shadow = Part("Sombra");
            _shadowMirror = Part("Sombra (espejo)");
            _ground = Part("Sombra suelo");
            _groundMirror = Part("Sombra suelo (espejo)");
        }

        private void OnDisable() => HideAll();

        private void HideAll()
        {
            foreach (var r in new[] { _main, _mirror, _shadow, _shadowMirror, _ground, _groundMirror })
                if (r != null) r.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (var m in new[] { _material, _shadowMaterial })
            {
                if (m == null) continue;
                if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
        }

        private MeshRenderer Part(string partName)
        {
            var t = transform.Find(partName);
            if (t == null)
            {
                var go = new GameObject(partName) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                t = go.transform;
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
            }
            t.GetComponent<MeshFilter>().sharedMesh = Quad();
            return t.GetComponent<MeshRenderer>();
        }

        private static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "ParallaxQuad", hideFlags = HideFlags.HideAndDontSave };
            _quad.SetVertices(new[] { new Vector3(0, 0), new Vector3(1, 0), new Vector3(1, 1), new Vector3(0, 1) });
            _quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.RecalculateBounds();
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f); // se mueve con la cámara: nunca descartar
            return _quad;
        }

        private void LateUpdate()
        {
            HasEnds = false;
            var cam = Camera.main;
            if (_level == null) _level = GetComponentInParent<Level>();
            if (cam == null || !cam.orthographic || texture == null || _level == null || _main == null)
            {
                HideAll();
                return;
            }

            if (_material == null || _material.shader != shader)
            {
                var sh = shader != null ? shader : Shader.Find("RedMagic/Parallax Layer");
                _material = new Material(sh) { name = "Parallax " + name, hideFlags = HideFlags.DontSave };
            }
            _material.mainTexture = texture;

            // Todo en unidades; la "vista" se mide desde su esquina superior izquierda, y hacia abajo.
            float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
            Vector3 c = cam.transform.position;
            float viewLeft = c.x - viewW * 0.5f, viewTop = c.y + viewH * 0.5f;
            Rect level = _level.WorldRect;
            float camLeft = viewLeft - level.xMin;          // cámara dentro del nivel, en horizontal
            float camDown = level.yMax - viewTop;           // cuánto ha bajado la cámara desde el techo
            float camMax = Mathf.Max(0f, level.height - viewH);

            float h = viewH * size / 100f, w = h * texture.width / texture.height;
            float yOff = -heightOffset * viewH / 200f;
            float y; // borde superior de la imagen, medido desde el borde superior de la vista (hacia abajo)
            switch (anchor)
            {
                case ParallaxVerticalAnchor.Ceiling:
                    y = yOff - camDown * verticalSpeed;
                    break;
                case ParallaxVerticalAnchor.Floor:
                    y = viewH - h + yOff + (camMax - camDown) * verticalSpeed;
                    break;
                default:
                    float k = camMax > 0f ? camDown / camMax : 0f, d = viewH - h;
                    y = align == ParallaxAlign.Top ? yOff + d * k * travel
                      : align == ParallaxAlign.Bottom ? d + yOff - d * (1f - k) * travel
                      : d * 0.5f + yOff + d * (k - 0.5f) * travel;
                    break;
            }
            float worldBottom = viewTop - y - h;

            string layer = inFront ? LevelSorting.FrontLayer : LevelSorting.Layer;
            int order = inFront ? LevelSorting.Front + sortingOrder
                      : overTerrain ? LevelSorting.OverTerrain + sortingOrder * 4
                      : LevelSorting.Background + sortingOrder * 4;
            bool shadows = castShadow && repeat == ParallaxRepeat.LevelEnds;
            if (!shadows) { _shadow.enabled = _shadowMirror.enabled = _ground.enabled = _groundMirror.enabled = false; }
            Color fog = new Color(fogColor.r, fogColor.g, fogColor.b, fade);

            if (repeat == ParallaxRepeat.LevelEnds)
            {
                float left = viewLeft + (-camLeft * speed + positionX);
                float right = viewLeft + (viewW + (level.width - viewW) * speed - camLeft * speed - positionX);
                float bottomLeft = worldBottom, bottomRight = worldBottom;
                if (sitOnGround)
                {
                    float fine = heightOffset * viewH / 200f;
                    bottomLeft = level.yMin + _level.GroundTop(Mathf.FloorToInt(left + w * 0.5f - level.xMin)) + fine;
                    bottomRight = level.yMin + _level.GroundTop(Mathf.FloorToInt(right - w * 0.5f - level.xMin)) + fine;
                }
                LeftEnd = new Vector2(left, bottomLeft); RightEnd = new Vector2(right, bottomRight);
                HasEnds = true; SortingLayerName = layer; SortingOrder = order;
                FollowLeftEnd();
                Draw(_main, new Vector3(left, bottomLeft), new Vector3(w, h, 1f), new Vector4(0f, 1f, 2f, 0f), fog, layer, order);
                Draw(_mirror, new Vector3(right - w, bottomRight), new Vector3(w, h, 1f), new Vector4(0f, 1f, 2f, 1f), fog, layer, order);
                if (shadows)
                {
                    if (_shadowMaterial == null)
                        _shadowMaterial = new Material(Shader.Find("RedMagic/Parallax Shadow")) { name = "Sombra " + name, hideFlags = HideFlags.DontSave };
                    float m = shadowSoftness;
                    var blur = new Vector2(m / (w + 2f * m), m / (h + 2f * m));
                    var size = new Vector3(w + 2f * m, h + 2f * m, 1f);
                    // En la capa del fondo, justo por encima de la pared: así cae sobre la pared y el terreno la tapa
                    // por debajo de la línea del suelo (sin manchar el suelo ni rodear la puerta).
                    int wallOrder = LevelSorting.Background + sortingOrder * 4;
                    Shadow(_shadow, new Vector3(left - m + shadowOffset.x, bottomLeft - m + shadowOffset.y), size, new Vector4(0f, 1f, 2f, 0f), blur, 0f, shadowColor, LevelSorting.Layer, wallOrder);
                    Shadow(_shadowMirror, new Vector3(right - w - m - shadowOffset.x, bottomRight - m + shadowOffset.y), size, new Vector4(0f, 1f, 2f, 1f), blur, 0f, shadowColor, LevelSorting.Layer, wallOrder);

                    var gc = new Color(shadowColor.r, shadowColor.g, shadowColor.b, groundShadowOpacity);
                    float gw = w * groundShadowWidth, gh = groundShadowHeight;
                    bool ground = groundShadowWidth > 0f && groundShadowOpacity > 0f;
                    if (ground)
                    {
                        // Sobre el suelo, desplazada hacia el mismo lado que la sombra de la pared.
                        float side = 0.5f + Mathf.Sign(shadowOffset.x) * 0.3f;
                        Shadow(_ground, new Vector3(left + w * side - gw * 0.5f, bottomLeft - gh * 0.65f), new Vector3(gw, gh, 1f), Vector4.zero, Vector2.zero, 1f, gc, layer, order - 1);
                        Shadow(_groundMirror, new Vector3(right - w * side - gw * 0.5f, bottomRight - gh * 0.65f), new Vector3(gw, gh, 1f), Vector4.zero, Vector2.zero, 1f, gc, layer, order - 1);
                    }
                    else _ground.enabled = _groundMirror.enabled = false;
                }

            }
            else
            {
                float u0 = (camLeft * speed - positionX) / w;
                Draw(_main, new Vector3(viewLeft, worldBottom), new Vector3(viewW, h, 1f),
                     new Vector4(u0, viewW / w, repeat == ParallaxRepeat.Mirror ? 1f : 0f, 0f), fog, layer, order);
                _mirror.enabled = false;
            }
        }

        /// <summary>
        /// Si lleva hijos <see cref="LevelEndsAttachment"/> (p. ej. los escalones de la puerta), el objeto de la capa se
        /// coloca en la esquina inferior izquierda de su copia izquierda: así los hijos se mueven con ella y se pueden
        /// colocar a mano dentro. Sólo cuando se mueve con el mundo (velocidad 1): si no, cambiaría cada frame.
        /// </summary>
        private void FollowLeftEnd()
        {
            if (!Mathf.Approximately(speed, 1f) || GetComponentInChildren<LevelEndsAttachment>(true) == null) return;
            var p = new Vector3(LeftEnd.x, LeftEnd.y, transform.position.z);
            if ((transform.position - p).sqrMagnitude > 1e-8f) transform.position = p;
        }

        private void Shadow(MeshRenderer r, Vector3 position, Vector3 scale, Vector4 uv, Vector2 blur, float mode, Color color, string layer, int order)
        {
            r.enabled = true;
            r.transform.position = new Vector3(position.x, position.y, transform.position.z);
            r.transform.localScale = scale;
            r.sharedMaterial = _shadowMaterial;
            r.sortingLayerName = layer;
            r.sortingOrder = order;
            _block.Clear();
            _block.SetTexture("_MainTex", texture);
            _block.SetVector("_UV", uv);
            _block.SetVector("_Blur", blur);
            _block.SetFloat("_Mode", mode);
            _block.SetColor("_Color", color);
            r.SetPropertyBlock(_block);
        }

        private void Draw(MeshRenderer r, Vector3 position, Vector3 scale, Vector4 uv, Color fog, string layer, int order)
        {
            r.enabled = true;
            r.transform.position = new Vector3(position.x, position.y, transform.position.z);
            r.transform.localScale = scale;
            r.sharedMaterial = _material;
            r.sortingLayerName = layer;
            r.sortingOrder = order;
            _block.Clear();
            _block.SetTexture("_MainTex", texture);
            _block.SetVector("_UV", uv);
            _block.SetColor("_Fog", fog);
            _block.SetFloat("_Alpha", opacity);
            r.SetPropertyBlock(_block);
        }
    }
}
