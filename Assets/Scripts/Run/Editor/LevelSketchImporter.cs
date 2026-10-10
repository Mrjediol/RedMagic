using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Convierte un boceto de la web (pestaña "Boceto de nivel", <c>*.levelsketch.json</c>) en un
    /// prefab de sección: terreno visible + colisión + fondo + entrada y salida. 1 celda = 1 unidad
    /// de Unity; el origen del nivel es su esquina inferior izquierda.
    ///
    /// Terreno = relleno (una malla de rectángulos con la textura repetida en coordenadas de mundo)
    /// + tiras de borde (sprites Tiled girados según hacia dónde mira cada borde) + colisión
    /// (BoxCollider2D por rectángulo fundidos en un CompositeCollider2D, capa Ground). Los bordes
    /// del mapa NO llevan tira: la cámara se limita al rectángulo del nivel (objeto "BG"), así que
    /// nunca se ve lo de fuera.
    ///
    /// El prefab resultante vale como "Base Section Prefab" de <see cref="WorldSceneGenerator"/>.
    /// Menú: <b>Tools > RedMagic > Niveles > Importar boceto...</b>.
    /// </summary>
    public static class LevelSketchImporter
    {
        private const string OutputRoot = "Assets/Levels";
        private const string PlaceholderKitPath = "Assets/Levels/_Kits/Placeholder/PlaceholderKit.asset";
        private const int FillOrder = 2, EdgeOrder = 3, BackgroundOrder = -100;

        [Serializable]
        private class Sketch
        {
            public int version;
            public string name;
            public int width;
            public int height;
            public float cellSize;
            public int playerCells;
            public string[] rows;
        }

        private struct Rect2Int
        {
            public int x, y, w, h; // x,y = esquina superior izquierda en celdas (fila 0 = arriba)
        }

        [MenuItem("Tools/RedMagic/Niveles/Importar boceto...")]
        private static void Import()
        {
            string json = EditorUtility.OpenFilePanel("Boceto de nivel", Application.dataPath, "json");
            if (string.IsNullOrEmpty(json)) return;

            var kit = Selection.activeObject as LevelKit ?? GetOrCreatePlaceholderKit();
            var prefab = Build(File.ReadAllText(json), kit);
            if (prefab != null) EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("Tools/RedMagic/Niveles/Crear kit de arte")]
        private static void CreateKit()
        {
            var kit = ScriptableObject.CreateInstance<LevelKit>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Levels/_Kits/NewLevelKit.asset");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(kit, path);
            Selection.activeObject = kit;
        }

        // ------------------------------------------------------------------ construcción

        public static GameObject Build(string sketchJson, LevelKit kit)
        {
            kit = kit != null ? kit : GetOrCreatePlaceholderKit();
            var sketch = JsonUtility.FromJson<Sketch>(sketchJson);
            if (sketch == null || sketch.rows == null || sketch.rows.Length != sketch.height)
            {
                Debug.LogError("[LevelSketchImporter] JSON no válido: faltan filas o no cuadran con 'height'.");
                return null;
            }

            int w = sketch.width, h = sketch.height;
            var solid = new bool[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    solid[y, x] = x < sketch.rows[y].Length && sketch.rows[y][x] == '#';

            string name = string.IsNullOrWhiteSpace(sketch.name) ? "Nivel" : sketch.name.Trim();
            string folder = $"{OutputRoot}/{name}";
            Directory.CreateDirectory(folder);

            var rects = MergeRects(solid, w, h);
            var root = new GameObject(name);
            try
            {
                BuildBackground(root.transform, w, h);
                BuildFill(root.transform, rects, h, kit, folder, name);
                BuildEdges(root.transform, solid, w, h, kit);
                BuildCollision(root.transform, rects, h);
                BuildEntryExit(root.transform, solid, w, h, Mathf.Max(1, sketch.playerCells));

                string prefabPath = $"{folder}/{name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[LevelSketchImporter] '{name}' ({w}x{h}, {rects.Count} bloques) → {prefabPath}");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Funde las celdas sólidas en el menor número razonable de rectángulos (voraz).</summary>
        private static List<Rect2Int> MergeRects(bool[,] solid, int w, int h)
        {
            var used = new bool[h, w];
            var rects = new List<Rect2Int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!solid[y, x] || used[y, x]) continue;
                int rw = 1;
                while (x + rw < w && solid[y, x + rw] && !used[y, x + rw]) rw++;
                int rh = 1;
                while (y + rh < h && RowFree(solid, used, y + rh, x, rw)) rh++;
                for (int yy = y; yy < y + rh; yy++)
                    for (int xx = x; xx < x + rw; xx++) used[yy, xx] = true;
                rects.Add(new Rect2Int { x = x, y = y, w = rw, h = rh });
            }
            return rects;
        }

        private static bool RowFree(bool[,] solid, bool[,] used, int y, int x, int rw)
        {
            for (int i = 0; i < rw; i++)
                if (!solid[y, x + i] || used[y, x + i]) return false;
            return true;
        }

        private static void BuildBackground(Transform parent, int w, int h)
        {
            // "BG" lo lee CameraBounds: su renderer define el rectángulo al que se limita la cámara.
            var bg = new GameObject("BG");
            bg.transform.SetParent(parent, false);
            bg.transform.localPosition = new Vector3(w * 0.5f, h * 0.5f, 0f);
            bg.transform.localScale = new Vector3(w, h, 1f);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateWhiteSprite();
            sr.color = new Color(0.063f, 0.098f, 0.106f);
            sr.sortingOrder = BackgroundOrder;
        }

        private static void BuildFill(Transform parent, List<Rect2Int> rects, int h, LevelKit kit,
                                      string folder, string name)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var tris = new List<int>();
            float inv = 1f / Mathf.Max(0.5f, kit.fillTileUnits);

            foreach (var r in rects)
            {
                float x0 = r.x, x1 = r.x + r.w, y1 = h - r.y, y0 = h - r.y - r.h;
                int i = verts.Count;
                verts.Add(new Vector3(x0, y0)); verts.Add(new Vector3(x0, y1));
                verts.Add(new Vector3(x1, y1)); verts.Add(new Vector3(x1, y0));
                uvs.Add(new Vector2(x0, y0) * inv); uvs.Add(new Vector2(x0, y1) * inv);
                uvs.Add(new Vector2(x1, y1) * inv); uvs.Add(new Vector2(x1, y0) * inv);
                for (int k = 0; k < 4; k++) cols.Add(Color.white);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            var mesh = new Mesh { name = name + "_Fill" };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, $"{folder}/{name}_Fill.mesh");

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            var mat = new Material(shader) { name = name + "_Fill" };
            mat.mainTexture = kit.fill;
            AssetDatabase.CreateAsset(mat, $"{folder}/{name}_Fill.mat");

            var go = new GameObject("Fill");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = FillOrder;
        }

        private enum Facing { Up, Down, Left, Right } // hacia dónde está el aire respecto al sólido

        private static void BuildEdges(Transform parent, bool[,] solid, int w, int h, LevelKit kit)
        {
            var edges = new GameObject("Edges");
            edges.transform.SetParent(parent, false);

            // Suelos (aire encima) y techos (aire debajo): corridas horizontales por fila.
            for (int y = 0; y < h; y++)
            {
                RunsAlong(w, x => solid[y, x] && y > 0 && !solid[y - 1, x],
                    (start, len) => PlaceStrip(edges.transform, kit, Facing.Up, start, h - y, len));
                RunsAlong(w, x => solid[y, x] && y < h - 1 && !solid[y + 1, x],
                    (start, len) => PlaceStrip(edges.transform, kit, Facing.Down, start, h - y - 1, len));
            }
            // Paredes: aire a la izquierda / derecha, corridas verticales por columna.
            for (int x = 0; x < w; x++)
            {
                RunsAlong(h, y => solid[y, x] && x > 0 && !solid[y, x - 1],
                    (start, len) => PlaceStrip(edges.transform, kit, Facing.Left, x, h - start - len, len));
                RunsAlong(h, y => solid[y, x] && x < w - 1 && !solid[y, x + 1],
                    (start, len) => PlaceStrip(edges.transform, kit, Facing.Right, x + 1, h - start - len, len));
            }
        }

        private static void RunsAlong(int count, Func<int, bool> on, Action<int, int> emit)
        {
            for (int i = 0; i < count; i++)
            {
                if (!on(i)) continue;
                int j = i;
                while (j + 1 < count && on(j + 1)) j++;
                emit(i, j - i + 1);
                i = j;
            }
        }

        /// <summary>
        /// Coloca una tira. (a, b) son coordenadas de mundo del arranque de la corrida: para
        /// Up/Down, a = x inicial y b = y de la línea del borde; para Left/Right, a = x de la línea
        /// del borde y b = y inicial (la más baja) de la corrida.
        /// </summary>
        private static void PlaceStrip(Transform parent, LevelKit kit, Facing facing, float a, float b, int len)
        {
            var sprite = facing switch
            {
                Facing.Down => kit.ceilingEdge != null ? kit.ceilingEdge : kit.floorEdge,
                Facing.Left or Facing.Right => kit.wallEdge != null ? kit.wallEdge : kit.floorEdge,
                _ => kit.floorEdge,
            };
            if (sprite == null) return;

            float t = sprite.bounds.size.y;
            float o = kit.edgeOverhang;
            // La tira se dibuja con su exterior arriba; se coloca con el exterior hacia el aire.
            Vector3 center;
            float angle;
            switch (facing)
            {
                case Facing.Up:    center = new Vector3(a + len * 0.5f, b - t * 0.5f + o); angle = 0f; break;
                case Facing.Down:  center = new Vector3(a + len * 0.5f, b + t * 0.5f - o); angle = 180f; break;
                case Facing.Left:  center = new Vector3(a + t * 0.5f - o, b + len * 0.5f); angle = 90f; break;
                default:           center = new Vector3(a - t * 0.5f + o, b + len * 0.5f); angle = -90f; break;
            }

            var go = new GameObject("Edge" + facing);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(len, t);
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = EdgeOrder;
        }

        private static void BuildCollision(Transform parent, List<Rect2Int> rects, int h)
        {
            var terrain = new GameObject("Collision");
            terrain.transform.SetParent(parent, false);
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) terrain.layer = ground;

            var body = terrain.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = terrain.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;

            foreach (var r in rects)
            {
                var child = new GameObject("Block");
                child.transform.SetParent(terrain.transform, false);
                if (ground >= 0) child.layer = ground;
                child.transform.localPosition = new Vector3(r.x + r.w * 0.5f, h - r.y - r.h * 0.5f);
                var box = child.AddComponent<BoxCollider2D>();
                box.size = new Vector2(r.w, r.h);
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }

        private static void BuildEntryExit(Transform parent, bool[,] solid, int w, int h, int playerCells)
        {
            bool hasLeft = TryFindStand(solid, w, h, playerCells, true, out var left);
            bool hasRight = TryFindStand(solid, w, h, playerCells, false, out var right);

            var entry = new GameObject("SectionEntry");
            entry.transform.SetParent(parent, false);
            entry.transform.localPosition = hasLeft ? left : new Vector3(2f, h * 0.5f);
            entry.AddComponent<SectionEntry>();

            var exit = new GameObject("SectionExit");
            exit.transform.SetParent(parent, false);
            exit.transform.localPosition = hasRight ? right : new Vector3(w - 2f, h * 0.5f);
            var trigger = exit.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, playerCells * 2f);
            exit.AddComponent<SectionExit>();
        }

        /// <summary>
        /// Primer sitio (desde la izquierda o la derecha) con suelo y sitio de pie para el jugador;
        /// devuelve el centro del cuerpo (suelo + mitad del alto del jugador).
        /// </summary>
        private static bool TryFindStand(bool[,] solid, int w, int h, int playerCells, bool fromLeft, out Vector3 pos)
        {
            for (int i = 0; i < w; i++)
            {
                int x = fromLeft ? i : w - 1 - i;
                for (int y = h - 1; y >= playerCells; y--) // fila del suelo, de abajo arriba
                {
                    if (!solid[y, x]) continue;
                    bool free = true;
                    for (int k = 1; k <= playerCells && free; k++) free = !solid[y - k, x];
                    if (!free) continue;
                    pos = new Vector3(x + 0.5f, h - y + playerCells * 0.5f);
                    return true;
                }
            }
            pos = default;
            return false;
        }

        // ------------------------------------------------------------------ kit provisional

        private static LevelKit GetOrCreatePlaceholderKit()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelKit>(PlaceholderKitPath);
            if (existing != null) return existing;

            string dir = Path.GetDirectoryName(PlaceholderKitPath);
            Directory.CreateDirectory(dir);

            var fill = SaveTexture($"{dir}/fill.png", 64, 64, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                return Color.Lerp(new Color(0.34f, 0.27f, 0.2f), new Color(0.45f, 0.36f, 0.26f), n);
            }, repeat: true, ppu: 16, sprite: false) as Texture2D;

            var strip = SaveTexture($"{dir}/edge.png", 64, 16, (x, y) =>
                y >= 10 ? new Color(0.42f, 0.7f, 0.3f) : new Color(0.3f, 0.5f, 0.2f),
                repeat: true, ppu: 64, sprite: true) as Sprite;

            var kit = ScriptableObject.CreateInstance<LevelKit>();
            kit.fill = fill;
            kit.floorEdge = strip;
            AssetDatabase.CreateAsset(kit, PlaceholderKitPath);
            return kit;
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            string dir = Path.GetDirectoryName(PlaceholderKitPath);
            Directory.CreateDirectory(dir);
            string path = $"{dir}/white.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                   SaveTexture(path, 4, 4, (x, y) => Color.white, repeat: false, ppu: 1, sprite: true) as Sprite;
        }

        private static UnityEngine.Object SaveTexture(string path, int w, int h, Func<int, int, Color> pixel,
                                                      bool repeat, int ppu, bool sprite)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            if (sprite)
            {
                importer.spritePixelsPerUnit = ppu;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return sprite ? (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Sprite>(path)
                          : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
