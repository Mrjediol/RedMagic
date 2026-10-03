using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Una tira de fotogramas de un cuerpo por partes andando, sin Play Mode: instancia el prefab lejos de la escena,
    /// lo mueve a velocidad fija y compone cada frame con <see cref="RigDriverCode.Step"/> a paso fijo (determinista),
    /// y fotografía cada <paramref name="cellSeconds"/>. Bajo cada viñeta, una regla de suelo en coordenadas de mundo
    /// (una marca cada 0.25 u): un pie apoyado que no patina se queda sobre la misma marca de una viñeta a la
    /// siguiente. Sirve para afinar <see cref="RigGait"/> sin jugar.
    /// </summary>
    public static class RigFilmstrip
    {
        /// <param name="speed">u/s; positivo = hacia donde mira, negativo = andando hacia atrás.</param>
        /// <param name="stopAt">Segundos a los que se para (para ver cómo recoge los pies); &lt; 0 = no para.</param>
        public static string Walk(string prefabPath, string outPng, float speed = 2.2f, int cells = 12,
                                  float cellSeconds = 0.08f, int facing = 1, float stopAt = -1f, float warmup = 0.6f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return $"no hay prefab en {prefabPath}";

            var origin = new Vector3(9000f, 9000f, 0f);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var camGo = new GameObject("RigFilmstripCam");
            var rt = new RenderTexture(CellW, CellH, 24);
            try
            {
                inst.hideFlags = HideFlags.HideAndDontSave;
                camGo.hideFlags = HideFlags.HideAndDontSave;
                foreach (var body in inst.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
                inst.transform.position = origin;

                var driver = inst.GetComponent<RigDriverCode>();
                if (driver == null) return "el prefab no tiene RigDriverCode";
                Call(driver, "Awake");
                Call(driver, "OnEnable");
                driver.SetFacing(facing);

                var col = inst.GetComponent<Collider2D>();
                Physics2D.SyncTransforms();
                float groundY = col != null ? col.bounds.min.y - origin.y : -1.5f;
                float height = col != null ? col.bounds.size.y : 3f;

                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = height * 0.6f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.19f, 0.22f, 1f);
                cam.targetTexture = rt;

                var strip = new Texture2D(CellW * cells, CellH, TextureFormat.RGB24, false);
                var cell = new Texture2D(CellW, CellH, TextureFormat.RGB24, false);
                const float dt = 1f / 60f;
                float t = -warmup, nextShot = 0f;
                int shot = 0;
                while (shot < cells)
                {
                    float v = stopAt >= 0f && t >= stopAt ? 0f : speed;
                    inst.transform.position += new Vector3(v * facing * dt, 0f, 0f);
                    driver.Step(dt);
                    t += dt;
                    if (t < nextShot) continue;

                    nextShot += cellSeconds;
                    var root = inst.transform.position;
                    cam.transform.position = new Vector3(root.x, root.y + height * 0.05f, root.z - 10f);
                    cam.Render();
                    RenderTexture.active = rt;
                    cell.ReadPixels(new Rect(0, 0, CellW, CellH), 0, 0);
                    RenderTexture.active = null;
                    DrawRuler(cell, cam, root.y + groundY);
                    strip.SetPixels(shot * CellW, 0, CellW, CellH, cell.GetPixels());
                    shot++;
                }

                strip.Apply();
                System.IO.File.WriteAllBytes(outPng, strip.EncodeToPNG());
                Object.DestroyImmediate(strip);
                Object.DestroyImmediate(cell);
                return $"{cells} viñetas, {cellSeconds:0.###} s entre ellas, a {speed} u/s → {outPng}";
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(inst);
            }
        }

        private const int CellW = 420, CellH = 380;

        private static void Call(object target, string method)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var m = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m == null) continue;
                m.Invoke(target, null);
                return;
            }
        }

        /// <summary>La línea del suelo y una marca cada 0.25 u de mundo (cada 1 u, más alta).</summary>
        private static void DrawRuler(Texture2D tex, Camera cam, float groundY)
        {
            var line = new Color(1f, 0.85f, 0.3f);
            int gy = Mathf.RoundToInt(cam.WorldToScreenPoint(new Vector3(cam.transform.position.x, groundY, 0f)).y);
            for (int x = 0; x < tex.width; x++) Plot(tex, x, gy - 1, line);

            float half = cam.orthographicSize * cam.aspect;
            float left = cam.transform.position.x - half, right = cam.transform.position.x + half;
            for (float wx = Mathf.Floor(left * 4f) / 4f; wx <= right; wx += 0.25f)
            {
                int px = Mathf.RoundToInt(cam.WorldToScreenPoint(new Vector3(wx, groundY, 0f)).x);
                int len = Mathf.Abs(wx - Mathf.Round(wx)) < 0.01f ? 14 : 7;
                for (int y = 0; y < len; y++) Plot(tex, px, gy - 2 - y, line);
            }
        }

        private static void Plot(Texture2D tex, int x, int y, Color c)
        {
            if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, c);
        }
    }
}
