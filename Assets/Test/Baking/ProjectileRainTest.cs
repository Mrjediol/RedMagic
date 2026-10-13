using System.Collections.Generic;
using System.Reflection;
using RedMagic.Pipeline;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.BakingTest
{
    /// <summary>
    /// PRUEBA DE ESTRÉS del baking de proyectiles (todo en <c>Assets/Test/Baking</c>, se borra con su menú). Hace llover
    /// <see cref="count"/> proyectiles desde el techo hasta el suelo y vuelven a caer en bucle. Teclas (en Play):
    /// <b>1</b> = proyectiles originales (15 texturas sueltas), <b>2</b> = los de hoja única, <b>0</b> = parar,
    /// <b>+ / -</b> = más o menos proyectiles. Cada uno arranca en un fotograma distinto de su animación: así se ve el
    /// peor caso del original (muchas texturas distintas a la vez), que es justo lo que la hoja única arregla.
    /// Los proyectiles son los prefabs reales con todo apagado menos el animador de sprites: sólo se mide el dibujo.
    /// </summary>
    public class ProjectileRainTest : MonoBehaviour
    {
        [SerializeField] private GameObject[] originals;
        [SerializeField] private GameObject[] baked;
        [SerializeField] private int count = 100;
        [SerializeField] private float minX = 74f, maxX = 92f, topY = 17f, bottomY = 2f;
        [SerializeField] private float speed = 7f, scale = 0.5f;

        public void Configure(GameObject[] originalPrefabs, GameObject[] bakedPrefabs, float left, float right, float top, float bottom)
        {
            originals = originalPrefabs; baked = bakedPrefabs; minX = left; maxX = right; topY = top; bottomY = bottom;
        }

        private static readonly FieldInfo TimerField = typeof(SpriteStateMachine).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<Transform> _rain = new List<Transform>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly HashSet<int> _textures = new HashSet<int>(), _sprites = new HashSet<int>();
        private int _distinctTextures, _distinctSprites;
        private float _scanTimer;
        private int _mode;          // 0 parado, 1 originales, 2 hoja única
        private float _fpsSmooth = 60f, _msWindow, _msAverage;
        private int _framesWindow;
        private float _windowTimer;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / Mathf.Max(dt, 0.0001f), 0.05f);
            _msWindow += dt * 1000f; _framesWindow++; _windowTimer += dt;
            if (_windowTimer >= 1f) { _msAverage = _msWindow / Mathf.Max(1, _framesWindow); _msWindow = 0f; _framesWindow = 0; _windowTimer = 0f; }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetMode(1);
                if (keyboard.digit2Key.wasPressedThisFrame) SetMode(2);
                if (keyboard.digit0Key.wasPressedThisFrame) SetMode(0);
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) { count = Mathf.Min(400, count + 25); Rebuild(); }
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) { count = Mathf.Max(5, count - 25); Rebuild(); }
            }

            // comprobación de que la prueba hace lo que dice: cuántas texturas / sprites distintos se están dibujando
            _scanTimer += dt;
            if (_scanTimer >= 0.25f)
            {
                _scanTimer = 0f;
                _textures.Clear(); _sprites.Clear();
                foreach (var r in _renderers)
                {
                    if (r == null || r.sprite == null) continue;
                    _textures.Add(r.sprite.texture.GetInstanceID());
                    _sprites.Add(r.sprite.GetInstanceID());
                }
                _distinctTextures = _textures.Count; _distinctSprites = _sprites.Count;
            }

            float step = speed * Time.deltaTime;
            foreach (var t in _rain)
            {
                var p = t.position;
                p.y -= step;
                if (p.y < bottomY) { p.y = topY + Random.value * 4f; p.x = Mathf.Lerp(minX, maxX, Random.value); }
                t.position = p;
            }
        }

        private void SetMode(int mode)
        {
            _mode = mode;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var t in _rain) if (t != null) Destroy(t.gameObject);
            _rain.Clear();
            _renderers.Clear();
            _distinctTextures = _distinctSprites = 0;
            var prefabs = _mode == 1 ? originals : _mode == 2 ? baked : null;
            if (prefabs == null || prefabs.Length == 0) return;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefabs[i % prefabs.Length], transform);
                go.transform.localScale = Vector3.one * scale;
                go.transform.position = new Vector3(Mathf.Lerp(minX, maxX, Random.value), Mathf.Lerp(bottomY, topY + 4f, Random.value), 0f);

                // sólo el animador de sprites: sin fuego, sin física, sin colisión (se mide únicamente el dibujo)
                foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) if (!(b is SpriteStateMachine)) b.enabled = false;
                foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
                foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

                // cada uno en un fotograma distinto
                var sm = go.GetComponentInChildren<SpriteStateMachine>();
                if (sm != null && TimerField != null) TimerField.SetValue(sm, Random.value * 0.65f);
                _rain.Add(go.transform);
                _renderers.AddRange(go.GetComponentsInChildren<SpriteRenderer>(true));
            }
        }

        private void OnGUI()
        {
            var box = new Rect(Screen.width - 470f, 10f, 460f, 170f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string mode = _mode == 1 ? "ORIGINALES (texturas sueltas)" : _mode == 2 ? "HOJA ÚNICA (horneado)" : "parado";
            string text = $"LLUVIA DE PROYECTILES   [1] orig  [2] hoja  [0] parar  [+/-] cantidad\n" +
                          $"Modo: {mode}   Proyectiles: {(_mode == 0 ? 0 : count)}\n" +
                          $"TEXTURAS distintas en pantalla: {_distinctTextures}   (sprites distintos: {_distinctSprites})
" +
                          $"{_fpsSmooth:0} FPS   {_msAverage:0.00} ms/frame (media 1 s)";
#if UNITY_EDITOR
            text += $"\nBatches: {UnityEditor.UnityStats.batches}   SetPass: {UnityEditor.UnityStats.setPassCalls}   Tris: {UnityEditor.UnityStats.triangles}";
#endif
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            style.normal.textColor = _mode == 1 ? new Color(1f, 0.65f, 0.35f) : _mode == 2 ? new Color(0.45f, 1f, 0.6f) : Color.white;
            GUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f), text, style);
        }
    }
}
