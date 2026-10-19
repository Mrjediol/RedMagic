using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RedMagic.Arsenal.Editor
{
    /// <summary>
    /// Banco de pruebas de los efectos del arsenal <b>sin Play</b> (proceso de <c>docs/fx-proceso-bocetos.md</c> ▸ 4): escena
    /// aditiva temporal con cámara propia, luz global, suelo (capa Ground) y muñecos con <see cref="Health"/>; el mundo de
    /// entidades se avanza a mano (<see cref="Run"/>) y <see cref="Shot"/> guarda un PNG. Nunca toca las escenas abiertas:
    /// al terminar (<see cref="Dispose"/>) se cierra sin guardar.
    /// </summary>
    public sealed class ArsenalPreviewStage : IDisposable
    {
        public readonly ArsenalWorld World;
        private readonly Scene _scene, _prev;
        private readonly Camera _cam;
        private readonly List<Health> _enemies = new List<Health>();
        private readonly Sprite _dummy;
        public const float Dt = 1f / 60f;
        /// <summary>Capa libre donde va todo lo visible del banco: la cámara sólo ve esta (no la escena que tengas abierta).</summary>
        public const int Layer = 31;

        public ArsenalPreviewStage(Vector2 camCenter, float orthoSize = 4.5f)
        {
            _prev = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);

            var camGo = new GameObject("PreviewCam");
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = orthoSize;
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.1f, 0.11f, 0.17f);
            camGo.transform.position = new Vector3(camCenter.x, camCenter.y, -10f);
            _cam.cullingMask = 1 << Layer;

            var lightGo = new GameObject("PreviewLight");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var lso = new SerializedObject(light);
            var layers = lso.FindProperty("m_ApplyToSortingLayers");
            if (layers != null)
            {
                layers.arraySize = SortingLayer.layers.Length;
                for (int i = 0; i < SortingLayer.layers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            _dummy = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Test/Arsenal/Dummy.png");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholder/Square.png");
            var ground = new GameObject("Ground") { layer = ArsenalTargets.GroundLayer };
            ground.transform.position = new Vector3(camCenter.x, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 1f);
            ground.AddComponent<BoxCollider2D>();
            var groundArt = new GameObject("GroundArt") { layer = Layer };
            groundArt.transform.SetParent(ground.transform, false);
            var gr = groundArt.AddComponent<SpriteRenderer>(); gr.sprite = square; gr.color = new Color(0.17f, 0.24f, 0.32f);

            float top = camCenter.y + orthoSize;
            ArsenalWorld.ViewTopOverride = () => top;
            World = ArsenalWorld.CreatePreview();
            World.SetCanvasLayer(Layer);
            ArsenalTargets.PreviewHealths = _enemies;
        }

        /// <summary>Muñeco (elipse del tamaño de los de la web) con su centro en <paramref name="c"/>.</summary>
        public Health Dummy(Vector2 c, bool fly = false, float hp = 120f)
        {
            float rx = (fly ? 22 : 27) * ArsenalDraw.PX, ry = (fly ? 28 : 39) * ArsenalDraw.PX;
            var go = new GameObject("Dummy");
            go.transform.position = c;
            var body = new GameObject("Body") { layer = Layer };
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(rx * 2f, ry * 2f / 1.5f, 1f);
            var sr = body.AddComponent<SpriteRenderer>(); sr.sprite = _dummy; sr.color = new Color(0.42f, 0.23f, 0.36f);
            var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit != null) sr.sharedMaterial = new Material(unlit);
            sr.sortingLayerName = "Characters"; sr.sortingOrder = 8;
            var col = go.AddComponent<CapsuleCollider2D>(); col.size = new Vector2(rx * 2f, ry * 2f);
            var h = go.AddComponent<Health>();
            var so = new SerializedObject(h);
            so.FindProperty("maxHealth").floatValue = hp; so.FindProperty("currentHealth").floatValue = hp;
            so.FindProperty("invulnerabilityDuration").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            _enemies.Add(h);
            Physics2D.SyncTransforms();
            return h;
        }

        /// <summary>Avanza <paramref name="seconds"/> a 60 fps; <paramref name="each"/> recibe el tiempo dentro del tramo antes de cada paso.</summary>
        public void Run(float seconds, Action<float> each = null)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (int i = 0; i < n; i++)
            {
                each?.Invoke(i * Dt);
                Physics2D.SyncTransforms();
                World.Step(Dt);
            }
        }

        /// <summary>Dibuja y guarda un PNG de <paramref name="w"/>×<paramref name="h"/> en <paramref name="path"/>.</summary>
        public void Shot(string path, int w = 1280, int h = 720)
        {
            World.Render();
            var rt = new RenderTexture(w, h, 24);
            _cam.targetTexture = rt; _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; _cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }

        public void Dispose()
        {
            ArsenalTargets.PreviewHealths = null;
            ArsenalWorld.ViewTopOverride = null;
            if (World != null) { World.Clear(); UnityEngine.Object.DestroyImmediate(World.gameObject); }
            if (_prev.IsValid() && _prev.isLoaded) SceneManager.SetActiveScene(_prev);
            EditorSceneManager.CloseScene(_scene, true);
            ArsenalRuntime.Clear();
        }
    }
}
