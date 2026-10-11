using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEngine;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Escalones de piedra helada entre el hueco oscuro de la puerta y el suelo, con los colores y el contorno de la
    /// puerta (<c>capa7.png</c>). Se dibujan por código en un PNG del MISMO tamaño que el de la puerta y se añaden al
    /// fondo del bioma como la capa "Escalones", encima de la puerta y con <c>alignWith</c> = la puerta: así quedan
    /// encajados aunque se mueva o se cambie de tamaño la puerta. Relanzable (rehace la imagen y la capa).
    /// Menú: <b>Tools ▸ RedMagic ▸ Niveles ▸ Crear escalones de la puerta</b>.
    /// </summary>
    public static class DoorSteps
    {
        private const string PrefabPath = LevelSetup.BiomesRoot + "/Hielo/Fondo/Fondo_Hielo.prefab";
        private const string PngPath = LevelSetup.BiomesRoot + "/Hielo/Fondo/escalones.png";
        private const string LayerName = "Escalones";

        // paleta muestreada de capa7.png
        private static readonly Color Outline = new Color32(6, 14, 28, 255);
        private static readonly Color Dark = new Color32(55, 100, 145, 255);
        private static readonly Color Light = new Color32(145, 206, 236, 255);
        private static readonly Color Tread = new Color32(184, 228, 248, 255);
        private static readonly Color Shine = new Color32(222, 244, 252, 255);

        /// <summary>Un escalón, en px de la imagen de la puerta (y hacia abajo).</summary>
        private struct Step { public float x0, y0, x1, y1; public int blocks; }

        // Medido sobre capa7.png (790 × 1110): hueco oscuro entre x≈262 y x≈525, acaba en y≈1010; suelo = borde inferior.
        private static readonly Step[] Steps =
        {
            new Step { x0 = 228, y0 = 1050, x1 = 600, y1 = 1114, blocks = 3 }, // abajo (delante)
            new Step { x0 = 258, y0 = 996, x1 = 534, y1 = 1058, blocks = 2 }, // arriba (detrás)
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
                    if (l.name != LayerName && new SerializedObject(l).FindProperty("sitOnGround").boolValue) door = l;
                var doorTex = door != null ? new SerializedObject(door).FindProperty("texture").objectReferenceValue as Texture2D : null;
                if (doorTex == null)
                {
                    EditorUtility.DisplayDialog("Escalones", "No encuentro la capa de la puerta (la que tiene 'Sit On Ground') en " + PrefabPath, "Vale");
                    return;
                }

                File.WriteAllBytes(PngPath, Draw(doorTex.width, doorTex.height).EncodeToPNG());
                AssetDatabase.ImportAsset(PngPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(PngPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);

                var old = door.transform.parent.Find(LayerName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var steps = Object.Instantiate(door.gameObject, door.transform.parent);
                steps.name = LayerName;
                steps.transform.SetSiblingIndex(door.transform.GetSiblingIndex() + 1);
                var so = new SerializedObject(steps.GetComponent<ParallaxBackgroundLayer>());
                so.FindProperty("texture").objectReferenceValue = tex;
                so.FindProperty("sortingOrder").intValue = so.FindProperty("sortingOrder").intValue + 1;
                so.FindProperty("castShadow").boolValue = false;
                so.FindProperty("alignWith").objectReferenceValue = door;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[DoorSteps] {PngPath} + capa '{LayerName}' en {PrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static Texture2D Draw(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            const int ss = 3; // supermuestreo: bordes suaves
            int yMin = h, yMax = 0, xMin = w, xMax = 0;
            foreach (var s in Steps)
            {
                xMin = Mathf.Min(xMin, (int)s.x0 - 2); xMax = Mathf.Max(xMax, (int)s.x1 + 2);
                yMin = Mathf.Min(yMin, (int)s.y0 - 2); yMax = Mathf.Max(yMax, (int)s.y1 + 2);
            }
            for (int y = Mathf.Max(0, yMin); y < Mathf.Min(h, yMax); y++)
            for (int x = Mathf.Max(0, xMin); x < Mathf.Min(w, xMax); x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var c = At(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a); // premultiplicado para promediar bien
                }
                sum /= ss * ss;
                if (sum.a > 0f) px[(h - 1 - y) * w + x] = new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Color en un punto (px de la imagen, y hacia abajo): el escalón de delante tapa al de detrás.</summary>
        private static Color At(float x, float y)
        {
            foreach (var s in Steps)
            {
                float cx = (s.x0 + s.x1) * 0.5f, cy = (s.y0 + s.y1) * 0.5f;
                float qx = Mathf.Abs(x - cx) - ((s.x1 - s.x0) * 0.5f - Radius), qy = Mathf.Abs(y - cy) - ((s.y1 - s.y0) * 0.5f - Radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - Radius;
                if (d > 0f) continue;
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
                    return Color.Lerp(Dark, Dark * 0.75f, k * 0.5f).WithAlpha(1f);
                }
                float fl = left + EndW, bw = (right - EndW - fl) / s.blocks;
                int bi = Mathf.Clamp(Mathf.FloorToInt((x - fl) / bw), 0, s.blocks - 1);
                float bx = fl + bi * bw;
                if (bi > 0 && x - bx < LineW * 0.5f) return Outline;                     // junta entre bloques
                if (bi < s.blocks - 1 && bx + bw - x < LineW * 0.5f) return Outline;
                var face = Color.Lerp(Light, Color.Lerp(Light, Dark, 0.45f), k * k).WithAlpha(1f);
                if (y - treadEnd < 6f && x - bx > 8f && bx + bw - x > 8f) face = Color.Lerp(face, Shine, 0.5f); // luz bajo la arista
                return face;
            }
            return Color.clear;
        }
    }

    internal static class ColorAlpha
    {
        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
