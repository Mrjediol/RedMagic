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
        [SerializeField] private int sortingOrder;

        private MeshRenderer _main, _mirror;
        private MaterialPropertyBlock _block;
        private Material _material;
        private Level _level;
        private static Mesh _quad;

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
        }

        private void OnDisable()
        {
            if (_main != null) _main.enabled = false;
            if (_mirror != null) _mirror.enabled = false;
        }

        private void OnDestroy()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
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
            var cam = Camera.main;
            if (_level == null) _level = GetComponentInParent<Level>();
            if (cam == null || !cam.orthographic || texture == null || _level == null || _main == null)
            {
                if (_main != null) _main.enabled = false;
                if (_mirror != null) _mirror.enabled = false;
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

            string layer = inFront ? "Characters" : "BackGround";
            int order = inFront ? 50 + sortingOrder : -100 + sortingOrder;
            Color fog = new Color(fogColor.r, fogColor.g, fogColor.b, fade);

            if (repeat == ParallaxRepeat.LevelEnds)
            {
                float left = viewLeft + (-camLeft * speed + positionX);
                float right = viewLeft + (viewW + (level.width - viewW) * speed - camLeft * speed - positionX);
                Draw(_main, new Vector3(left, worldBottom), new Vector3(w, h, 1f), new Vector4(0f, 1f, 2f, 0f), fog, layer, order);
                Draw(_mirror, new Vector3(right - w, worldBottom), new Vector3(w, h, 1f), new Vector4(0f, 1f, 2f, 1f), fog, layer, order);
            }
            else
            {
                float u0 = (camLeft * speed - positionX) / w;
                Draw(_main, new Vector3(viewLeft, worldBottom), new Vector3(viewW, h, 1f),
                     new Vector4(u0, viewW / w, repeat == ParallaxRepeat.Mirror ? 1f : 0f, 0f), fog, layer, order);
                _mirror.enabled = false;
            }
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
