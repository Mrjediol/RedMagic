using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RedMagic.Levels
{
    /// <summary>
    /// Puerta de energía que cierra un hueco de la sala. Ocupa SIEMPRE celdas enteras (<see cref="cellsWide"/> ×
    /// <see cref="cellsHigh"/>; 1 celda = 1 unidad) y se monta con piezas del arte en vez de estirar la imagen:
    /// tapas fijas con los cristales centrados (se alargan por los lados con un trozo liso), laterales de runas que se
    /// repiten en vertical según el alto y una ranura central que se ensancha. Dentro de la ranura fluye la energía
    /// (<c>RedMagic/Fx Door Energy</c>); la imagen encendida (cristales y runas) se funde sobre la apagada.
    /// <b>Cerrada</b> = encendida, con energía, bloquea siempre. <b>Abierta</b> = apagada y sin colisión.
    /// El origen es la esquina inferior izquierda del hueco, alineada con la cuadrícula del <see cref="Level"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public class LevelDoor : MonoBehaviour
    {
        public const int MinWide = 1, MaxWide = 4, MinHigh = 2, MaxHigh = 10;
        public const string BodyName = "Cuerpo", LitName = "Encendida", EnergyName = "Energia", HaloName = "Halo", LightName = "Luz";

        [Header("Tamaño (celdas enteras)")]
        [SerializeField, Range(MinWide, MaxWide)] private int cellsWide = 1;
        [SerializeField, Range(MinHigh, MaxHigh)] private int cellsHigh = 4;

        [Header("Estado")]
        [Tooltip("Al empezar: abierta (apagada, se puede pasar) o cerrada (encendida, bloquea).")]
        [SerializeField] private bool startOpen = false;
        [SerializeField, Min(0.05f)] private float closeSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.35f;

        [Header("Arte")]
        [SerializeField] private Texture2D offTexture;
        [SerializeField] private Texture2D onTexture;
        [SerializeField] private Shader energyShader;
        [SerializeField] private Color energyColor = new Color(0.2f, 0.8f, 1f, 1f);

        [Header("Regiones del arte (px, desde arriba-izquierda de la imagen)")]
        [Tooltip("Alto de la tapa de arriba (cristales y collar).")]
        [SerializeField] private int topCapPx = 122;
        [SerializeField] private int bottomCapPx = 120;
        [Tooltip("Ancho de los extremos de las tapas (a cada lado de los cristales).")]
        [SerializeField] private int capEndPx = 120;
        [Tooltip("Trozo liso de la tapa que se estira cuando la puerta es más ancha que la imagen.")]
        [SerializeField] private int capFillerPx = 25;
        [Tooltip("Ranura: de x izquierda a x derecha.")]
        [SerializeField] private int slotLeftPx = 118;
        [SerializeField] private int slotRightPx = 230;
        [Tooltip("Dónde empieza la ranura (desde arriba) y dónde acaba, atravesando los collares de las tapas.")]
        [SerializeField] private int slotTopPx = 97;
        [SerializeField] private int slotBottomPx = 842;
        [Tooltip("Cuánto mide en celdas un repetido de laterales (alto de la zona de runas / ancho de la imagen).")]
        [SerializeField, Min(0.5f)] private float shaftRepeatCells = 2f;

        public int CellsWide => cellsWide;
        public int CellsHigh => cellsHigh;
        public bool IsOpen => _target <= 0f;
        public float Progress => _progress;
        /// <summary>Terminó de abrirse / de cerrarse (la energía llegó abajo).</summary>
        public event Action Opened, Closed;

        private float _progress, _target;       // 1 = cerrada y encendida
        private bool _built;
        private Mesh _bodyMesh, _litMesh, _energyMesh, _haloMesh;
        private Material _bodyMaterial, _litMaterial, _energyMaterial, _haloMaterial;
        private List<Color> _litColors = new List<Color>(), _haloColors = new List<Color>();
        private int _litVerts;

        // ------------------------------------------------------------------ ciclo de vida

        private void OnEnable()
        {
            _target = startOpen ? 0f : 1f;
            _progress = _target;
            Rebuild();
        }

        private void OnDestroy()
        {
            Level.DestroySafe(_bodyMesh); Level.DestroySafe(_litMesh); Level.DestroySafe(_energyMesh); Level.DestroySafe(_haloMesh);
            Level.DestroySafe(_bodyMaterial); Level.DestroySafe(_litMaterial); Level.DestroySafe(_energyMaterial); Level.DestroySafe(_haloMaterial);
        }

        private void OnValidate()
        {
            cellsWide = Mathf.Clamp(cellsWide, MinWide, MaxWide);
            cellsHigh = Mathf.Clamp(cellsHigh, MinHigh, MaxHigh);
            _built = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (!_built) Rebuild();
            if (Mathf.Approximately(_progress, _target)) return;
            float seconds = _target > _progress ? closeSeconds : openSeconds;
            _progress = Application.isPlaying ? Mathf.MoveTowards(_progress, _target, Time.deltaTime / seconds) : _target;
            ApplyProgress();
            if (Mathf.Approximately(_progress, _target)) (_target > 0f ? Closed : Opened)?.Invoke();
        }

        // ------------------------------------------------------------------ API

        [ContextMenu("Cerrar")] public void Close() => SetClosed(true, false);
        [ContextMenu("Abrir")] public void Open() => SetClosed(false, false);

        /// <summary>Cierra o abre; <paramref name="instant"/> salta la animación.</summary>
        public void SetClosed(bool closed, bool instant)
        {
            _target = closed ? 1f : 0f;
            if (instant || !Application.isPlaying) _progress = _target;
            ApplyProgress();
        }

        /// <summary>Desde código (importador, menús).</summary>
        public void Configure(int wide, int high, bool open, Texture2D off, Texture2D on, Shader energy)
        {
            cellsWide = Mathf.Clamp(wide, MinWide, MaxWide);
            cellsHigh = Mathf.Clamp(high, MinHigh, MaxHigh);
            startOpen = open; offTexture = off; onTexture = on; energyShader = energy;
            _target = open ? 0f : 1f; _progress = _target;
            Rebuild();
        }

        // ------------------------------------------------------------------ construcción

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            _built = true;
            float W = cellsWide, H = cellsHigh;
            BuildCollider(W, H);
            if (offTexture == null) return;

            float ppc = offTexture.width; // píxeles por celda: la imagen mide 1 celda de ancho
            float texW = offTexture.width, texH = offTexture.height;

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            BuildPieces(W, H, ppc, texW, texH, verts, uvs, tris);

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            _bodyMesh = Level.Assign(_bodyMesh, "DoorBody", verts, tris, uvs, null);
            _bodyMaterial = Level.GetMaterial(ref _bodyMaterial, shader, "DoorBody");
            _bodyMaterial.mainTexture = offTexture;
            Level.SetupRenderer(Child(BodyName), _bodyMesh, _bodyMaterial, LevelSorting.OverTerrain);

            if (onTexture != null)
            {
                _litVerts = verts.Count;
                _litMesh = Level.Assign(_litMesh, "DoorLit", verts, tris, uvs, null);
                _litMaterial = Level.GetMaterial(ref _litMaterial, shader, "DoorLit");
                _litMaterial.mainTexture = onTexture;
                Level.SetupRenderer(Child(LitName), _litMesh, _litMaterial, LevelSorting.OverTerrain + 1);
            }

            BuildEnergy(W, H, ppc);
            BuildHalo(W, H);
            ApplyProgress();
        }

        private void BuildCollider(float W, float H)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) gameObject.layer = ground;
            if (!TryGetComponent<BoxCollider2D>(out var box)) box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(W, H);
            box.offset = new Vector2(W * 0.5f, H * 0.5f);
        }

        private void BuildPieces(float W, float H, float ppc, float texW, float texH,
                                 List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            void Piece(float x0, float y0, float x1, float y1, float px0, float py0, float px1, float py1)
            {
                if (x1 - x0 < 1e-4f || y1 - y0 < 1e-4f) return;
                float u0 = px0 / texW, u1 = px1 / texW, v0 = 1f - py1 / texH, v1 = 1f - py0 / texH;
                int s = verts.Count;
                verts.Add(new Vector3(x0, y0)); verts.Add(new Vector3(x1, y0)); verts.Add(new Vector3(x1, y1)); verts.Add(new Vector3(x0, y1));
                uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v0)); uvs.Add(new Vector2(u1, v1)); uvs.Add(new Vector2(u0, v1));
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }

            float topH = topCapPx / ppc, botH = bottomCapPx / ppc;
            float end = capEndPx / ppc;
            float centerPx = texW - 2f * capEndPx;
            float filler = Mathf.Max(0f, (W - texW / ppc) * 0.5f);
            float centreStart = end + filler, centreEnd = centreStart + centerPx / ppc;

            // tapas: extremo, trozo liso estirado, cristales (centro), trozo liso, extremo
            void Cap(float y0, float y1, float py0, float py1)
            {
                Piece(0f, y0, end, y1, 0f, py0, capEndPx, py1);
                Piece(end, y0, centreStart, y1, capEndPx - capFillerPx, py0, capEndPx, py1);
                Piece(centreStart, y0, centreEnd, y1, capEndPx, py0, capEndPx + centerPx, py1);
                Piece(centreEnd, y0, W - end, y1, capEndPx + centerPx, py0, capEndPx + centerPx + capFillerPx, py1);
                Piece(W - end, y0, W, y1, texW - capEndPx, py0, texW, py1);
            }
            Cap(H - topH, H, 0f, topCapPx);
            Cap(0f, botH, texH - bottomCapPx, texH);

            // cuerpo: laterales de runas repetidos en vertical + ranura lisa estirada
            float shaftBottom = botH, shaftTop = H - topH, shaftLen = shaftTop - shaftBottom;
            int repeats = Mathf.Max(1, Mathf.RoundToInt(shaftLen / shaftRepeatCells));
            float seg = shaftLen / repeats;
            float leftW = slotLeftPx / ppc, rightW = (texW - slotRightPx) / ppc;
            float shaftPy0 = topCapPx, shaftPy1 = texH - bottomCapPx;
            for (int k = 0; k < repeats; k++)
            {
                float y0 = shaftBottom + k * seg, y1 = y0 + seg;
                Piece(0f, y0, leftW, y1, 0f, shaftPy0, slotLeftPx, shaftPy1);
                Piece(W - rightW, y0, W, y1, slotRightPx, shaftPy0, texW, shaftPy1);
            }
            float midPy0 = shaftPy0 + (shaftPy1 - shaftPy0) * 0.35f, midPy1 = shaftPy0 + (shaftPy1 - shaftPy0) * 0.65f;
            Piece(leftW, shaftBottom, W - rightW, shaftTop, slotLeftPx + 8f, midPy0, slotRightPx - 8f, midPy1);
        }

        private void BuildEnergy(float W, float H, float ppc)
        {
            var existing = transform.Find(EnergyName);
            if (energyShader == null) { if (existing != null) Level.DestroySafe(existing.gameObject); return; }

            float leftW = slotLeftPx / ppc, rightW = (offTexture.width - slotRightPx) / ppc, inset = 0.012f;
            float x0 = leftW + inset, x1 = W - rightW - inset;
            float y0 = (offTexture.height - slotBottomPx) / ppc, y1 = H - slotTopPx / ppc;

            var verts = new List<Vector3> { new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x1, y1), new Vector3(x0, y1) };
            var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _energyMesh = Level.Assign(_energyMesh, "DoorEnergy", verts, new List<int> { 0, 2, 1, 0, 3, 2 }, uvs, null);
            _energyMaterial = Level.GetMaterial(ref _energyMaterial, energyShader, "DoorEnergy");
            _energyMaterial.SetColor("_Color", energyColor);
            _energyMaterial.SetVector("_Size", new Vector4(x1 - x0, y1 - y0, 0f, 0f));
            Level.SetupRenderer(Child(EnergyName), _energyMesh, _energyMaterial, LevelSorting.OverTerrain + 2);
        }

        /// <summary>Resplandor que sale del marco hacia fuera (cinta con la alfa de dentro a fuera), sobre el terreno.</summary>
        private void BuildHalo(float W, float H)
        {
            var shader = Shader.Find("RedMagic/Fx Vertex Color");
            if (shader == null) return;
            const float Out = 0.7f;
            float ix0 = 0.08f, iy0 = 0.08f, ix1 = W - 0.08f, iy1 = H - 0.08f;
            var v = new List<Vector3>
            {
                new Vector3(ix0, iy0), new Vector3(ix1, iy0), new Vector3(ix1, iy1), new Vector3(ix0, iy1),
                new Vector3(ix0 - Out, iy0 - Out), new Vector3(ix1 + Out, iy0 - Out), new Vector3(ix1 + Out, iy1 + Out), new Vector3(ix0 - Out, iy1 + Out),
            };
            var t = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4, c = 4 + (i + 1) % 4, d = 4 + i;
                t.Add(a); t.Add(c); t.Add(b); t.Add(a); t.Add(d); t.Add(c);
            }
            _haloColors.Clear();
            for (int i = 0; i < 8; i++) _haloColors.Add(Color.clear);
            _haloMesh = Level.Assign(_haloMesh, "DoorHalo", v, t, null, _haloColors);
            _haloMaterial = Level.GetMaterial(ref _haloMaterial, shader, "DoorHalo");
            Level.SetupRenderer(Child(HaloName), _haloMesh, _haloMaterial, LevelSorting.OverTerrain + 3);

            var go = Child(LightName);
            if (!go.TryGetComponent<Light2D>(out var l)) l = go.gameObject.AddComponent<Light2D>();
            go.localPosition = new Vector3(W * 0.5f, H * 0.5f);
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.45f, 0.85f, 1f);
            l.pointLightOuterRadius = 2.5f + H * 0.5f;
            l.pointLightInnerRadius = 0f;
        }

        /// <summary>Aplica el progreso: colisión, encendido, energía, halo y luz.</summary>
        private void ApplyProgress()
        {
            if (TryGetComponent<BoxCollider2D>(out var box)) box.enabled = _progress > 0.001f || _target > 0f;

            float lit = Mathf.SmoothStep(0f, 1f, _progress);
            if (_litMesh != null && _litVerts > 0)
            {
                _litColors.Clear();
                var c = new Color(1f, 1f, 1f, lit);
                for (int i = 0; i < _litVerts; i++) _litColors.Add(c);
                _litMesh.SetColors(_litColors);
            }
            if (_energyMaterial != null)
            {
                _energyMaterial.SetFloat("_Progress", _progress);
                _energyMaterial.SetFloat("_Intensity", _progress > 0.001f ? 1f : 0f);
            }
            if (_haloMesh != null)
            {
                _haloColors.Clear();
                var inner = new Color(0.35f, 0.85f, 1f, 0.32f * lit);
                for (int i = 0; i < 4; i++) _haloColors.Add(inner);
                for (int i = 0; i < 4; i++) _haloColors.Add(new Color(inner.r, inner.g, inner.b, 0f));
                _haloMesh.SetColors(_haloColors);
            }
            var light = transform.Find(LightName);
            if (light != null && light.TryGetComponent<Light2D>(out var l)) l.intensity = 0.8f * lit;
        }

        private Transform Child(string childName)
        {
            var t = transform.Find(childName);
            if (t != null) return t;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // el hueco que ocupa, en celdas enteras
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position + new Vector3(cellsWide * 0.5f, cellsHigh * 0.5f), new Vector3(cellsWide, cellsHigh));
        }
#endif
    }
}
