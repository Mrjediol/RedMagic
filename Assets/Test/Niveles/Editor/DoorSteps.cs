using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEngine;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Escalones de piedra helada entre el hueco oscuro de la puerta y el suelo, con los colores y el contorno de la
    /// puerta (<c>capa7.png</c>). Cada escalón es un PNG propio y un objeto hijo de la capa de la puerta con
    /// <see cref="LevelEndsAttachment"/>: se mueve con la puerta, se coloca a mano por separado y sale en espejo en la
    /// puerta del otro extremo. Relanzable: rehace las imágenes y los dos hijos (vuelven a su sitio de partida).
    /// Menú: <b>Tools ▸ RedMagic ▸ Niveles ▸ Crear escalones de la puerta</b>.
    /// </summary>
    public static class DoorSteps
    {
        private const string Folder = LevelSetup.BiomesRoot + "/Hielo/Fondo";
        private const string PrefabPath = Folder + "/Fondo_Hielo.prefab";
        private const float ViewHeight = 20f; // alto de la cámara en unidades (orthographicSize 10)

        // paleta muestreada de capa7.png
        private static readonly Color Outline = new Color32(6, 14, 28, 255);
        private static readonly Color Dark = new Color32(55, 100, 145, 255);
        private static readonly Color Light = new Color32(145, 206, 236, 255);
        private static readonly Color Tread = new Color32(184, 228, 248, 255);
        private static readonly Color Shine = new Color32(222, 244, 252, 255);

        /// <summary>Un escalón, en px de la imagen de la puerta (y hacia abajo).</summary>
        private struct Step { public string name, file; public float x0, y0, x1, y1; public int blocks, order; }

        // Sitio de partida medido sobre capa7.png (790 × 1110): hueco oscuro entre x≈262 y x≈525, suelo = borde inferior.
        private static readonly Step[] Steps =
        {
            new Step { name = "Escalón abajo", file = "escalon_abajo.png", x0 = 228, y0 = 1050, x1 = 600, y1 = 1114, blocks = 3, order = 2 },
            new Step { name = "Escalón arriba", file = "escalon_arriba.png", x0 = 258, y0 = 996, x1 = 534, y1 = 1058, blocks = 2, order = 1 },
        };
        private const float Radius = 10f, OutlineW = 7f, LineW = 5f, TreadFrac = 0.32f, EndW = 22f;

        [MenuItem("Tools/RedMagic/Niveles/Crear escalones de la puerta")]
        public static void Create()
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ParallaxBackgroundLayer door = null;
                foreach (var l in contents.GetComponentsInChildren<ParallaxBackgroundLayer>(true))
                    if (l.name != "Escalones" && new SerializedObject(l).FindProperty("sitOnGround").boolValue) door = l;
                var doorSo = door != null ? new SerializedObject(door) : null;
                var doorTex = doorSo?.FindProperty("texture").objectReferenceValue as Texture2D;
                if (doorTex == null)
                {
                    EditorUtility.DisplayDialog("Escalones", "No encuentro la capa de la puerta (la que tiene 'Sit On Ground') en " + PrefabPath, "Vale");
                    return;
                }
                // mismo tamaño en pantalla que la puerta: px de su imagen por unidad
                float ppu = doorTex.height / (ViewHeight * doorSo.FindProperty("size").floatValue / 100f);

                // lo de la versión anterior (una capa entera para los dos escalones)
                var oldLayer = door.transform.parent.Find("Escalones");
                if (oldLayer != null) Object.DestroyImmediate(oldLayer.gameObject);
                AssetDatabase.DeleteAsset(Folder + "/escalones.png");

                var unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                foreach (var s in Steps)
                {
                    var sprite = SaveSprite(s, ppu);
                    var old = door.transform.Find(s.name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var go = new GameObject(s.name);
                    go.transform.SetParent(door.transform, false);
                    // la capa se coloca en la esquina inferior izquierda de la puerta: el sitio es relativo a ella
                    go.transform.localPosition = new Vector3((s.x0 + s.x1) * 0.5f / ppu, (doorTex.height - s.y1) / ppu, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    if (unlit != null) sr.sharedMaterial = unlit; // como la puerta: sin luz
                    var att = new SerializedObject(go.AddComponent<LevelEndsAttachment>());
                    att.FindProperty("orderOffset").intValue = s.order;
                    att.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[DoorSteps] {Steps.Length} escalones hijos de '{door.name}' en {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>PNG recortado al escalón, pivote en el centro de su base.</summary>
        private static Sprite SaveSprite(Step s, float ppu)
        {
            int x0 = Mathf.FloorToInt(s.x0) - 1, y0 = Mathf.FloorToInt(s.y0) - 1;
            int w = Mathf.CeilToInt(s.x1) + 1 - x0, h = Mathf.CeilToInt(s.y1) + 1 - y0;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            const int ss = 3; // supermuestreo: bordes suaves
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var c = At(s, x0 + x + (sx + 0.5f) / ss, y0 + y + (sy + 0.5f) / ss);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a); // premultiplicado para promediar bien
                }
                sum /= ss * ss;
                if (sum.a > 0f) px[(h - 1 - y) * w + x] = new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a);
            }
            tex.SetPixels(px);
            string path = Folder + "/" + s.file;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.spritePixelsPerUnit = ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePivot = new Vector2(0.5f, (y0 + h - s.y1) / h); // centro de la base del escalón
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Color de un escalón en un punto (px de la imagen de la puerta, y hacia abajo).</summary>
        private static Color At(Step s, float x, float y)
        {
            float cx = (s.x0 + s.x1) * 0.5f, cy = (s.y0 + s.y1) * 0.5f;
            float qx = Mathf.Abs(x - cx) - ((s.x1 - s.x0) * 0.5f - Radius), qy = Mathf.Abs(y - cy) - ((s.y1 - s.y0) * 0.5f - Radius);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - Radius;
            if (d > 0f) return Color.clear;
            if (d > -OutlineW) return Outline;

            float top = s.y0 + OutlineW, bottom = s.y1 - OutlineW, treadEnd = top + (bottom - top) * TreadFrac;
            if (y < treadEnd - LineW * 0.5f) return y < top + 4f ? Shine : Tread;   // pisada, con brillo arriba
            if (y < treadEnd + LineW * 0.5f) return Outline;                         // arista pisada / frente

            float left = s.x0 + OutlineW, right = s.x1 - OutlineW;
            float k = Mathf.InverseLerp(treadEnd, bottom, y);                        // degradado del frente
            // laterales del escalón (vistos de lado): cara oscura en los dos extremos, como los bloques de la puerta
            if (x < left + EndW || x > right - EndW)
            {
                if (Mathf.Abs(x - (left + EndW)) < LineW * 0.5f || Mathf.Abs(x - (right - EndW)) < LineW * 0.5f) return Outline;
                return Opaque(Color.Lerp(Dark, Dark * 0.75f, k * 0.5f));
            }
            float fl = left + EndW, bw = (right - EndW - fl) / s.blocks;
            int bi = Mathf.Clamp(Mathf.FloorToInt((x - fl) / bw), 0, s.blocks - 1);
            float bx = fl + bi * bw;
            if (bi > 0 && x - bx < LineW * 0.5f) return Outline;                     // junta entre bloques
            if (bi < s.blocks - 1 && bx + bw - x < LineW * 0.5f) return Outline;
            var face = Opaque(Color.Lerp(Light, Color.Lerp(Light, Dark, 0.45f), k * k));
            if (y - treadEnd < 6f && x - bx > 8f && bx + bw - x > 8f) face = Color.Lerp(face, Shine, 0.5f); // luz bajo la arista
            return face;
        }

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
    }
}
