using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Impact frames: durante un instante (tiempo real) el fondo se oscurece y el enemigo golpeado se dibuja
    /// en blanco plano por encima. Genérico: cualquier ataque llama a <see cref="Play"/> con el objetivo.
    ///
    /// El fondo es un rectángulo del tamaño de la vista en la capa <c>Characters</c> (orden
    /// <see cref="OverlayOrder"/>: tapa fondo, terreno, jugador y enemigos, pero no los efectos de delante,
    /// que van en 40). El enemigo en blanco son <b>copias</b> de sus SpriteRenderers con el material de
    /// silueta (<c>Fx_SpriteSilhouette</c>), no el enemigo en sí: el rig y el HitFlash cambian sus renderers
    /// cada frame y no hay que pelearse con ellos. Se crea solo la primera vez (DontDestroyOnLoad).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactFrames : MonoBehaviour
    {
        public const int OverlayOrder = 34;
        private static readonly Color Dark = new Color(10f / 255f, 6f / 255f, 36f / 255f, 0.62f);

        private static ImpactFrames _instance;

        private SpriteRenderer _overlay;
        private readonly List<SpriteRenderer> _copies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _sources = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _scratch = new List<SpriteRenderer>();
        private float _until = -1f;
        private int _minOrder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>
        /// Un impact frame sobre <paramref name="target"/> (su raíz: se copian todos sus sprites visibles)
        /// durante <paramref name="duration"/> segundos reales. Un segundo impacto encima lo sustituye.
        /// </summary>
        public static void Play(Component target, float duration, Material silhouette)
        {
            if (target == null || duration <= 0f) return;
            if (_instance == null)
            {
                var go = new GameObject("[ImpactFrames]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ImpactFrames>();
            }
            _instance.Show(target, duration, silhouette);
        }

        private void Show(Component target, float duration, Material silhouette)
        {
            EnsureOverlay();
            _until = Time.unscaledTime + duration;

            _sources.Clear();
            target.GetComponentsInChildren(false, _scratch);
            _minOrder = int.MaxValue;
            foreach (var r in _scratch)
            {
                if (r == null || !r.enabled || r.sprite == null || r == _overlay) continue;
                _sources.Add(r);
                _minOrder = Mathf.Min(_minOrder, r.sortingOrder);
            }

            while (_copies.Count < _sources.Count)
            {
                var go = new GameObject("Silhouette");
                go.transform.SetParent(transform, false);
                _copies.Add(go.AddComponent<SpriteRenderer>());
            }
            for (int i = 0; i < _copies.Count; i++)
            {
                bool used = i < _sources.Count;
                _copies[i].enabled = used;
                if (!used) continue;
                if (silhouette != null) _copies[i].sharedMaterial = silhouette;
                _copies[i].color = Color.white;
            }

            _overlay.enabled = true;
            SyncCopies();
        }

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            var go = new GameObject("Overlay");
            go.transform.SetParent(transform, false);
            _overlay = go.AddComponent<SpriteRenderer>();
            var tex = Texture2D.whiteTexture;
            _overlay.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            _overlay.sortingLayerName = "Characters";
            _overlay.sortingOrder = OverlayOrder;
            _overlay.color = Dark;
            _overlay.enabled = false;
        }

        private void LateUpdate()
        {
            if (_overlay == null || !_overlay.enabled) return;
            if (Time.unscaledTime >= _until)
            {
                _overlay.enabled = false;
                foreach (var c in _copies) if (c != null) c.enabled = false;
                _sources.Clear();
                return;
            }
            SyncCopies();
        }

        private void SyncCopies()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                float h = cam.orthographic ? cam.orthographicSize * 2f : 30f, w = h * cam.aspect;
                var p = cam.transform.position;
                _overlay.transform.position = new Vector3(p.x, p.y, 0f);
                _overlay.transform.localScale = new Vector3(w * 1.2f, h * 1.2f, 1f);
            }

            for (int i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                var copy = _copies[i];
                if (src == null) { copy.enabled = false; continue; }
                var t = src.transform;
                copy.transform.SetPositionAndRotation(t.position, t.rotation);
                copy.transform.localScale = t.lossyScale;
                copy.sprite = src.sprite;
                copy.flipX = src.flipX;
                copy.flipY = src.flipY;
                copy.drawMode = src.drawMode;
                if (src.drawMode != SpriteDrawMode.Simple) copy.size = src.size;
                copy.sortingLayerName = "Characters";
                copy.sortingOrder = OverlayOrder + 1 + Mathf.Clamp(src.sortingOrder - _minOrder, 0, 4);
            }
        }
    }
}
