// MapImporter: importa los JSON producidos por MapTracer.html como prefabs modulares.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MapImporter
{
    [Serializable] private class CanvasData { public int width; public int height; }
    [Serializable] private class PointData { public float x; public float y; }
    [Serializable] private class PieceData { public string assetName; public string fileName; public string type; public float x; public float y; public float scaleX; public float scaleY; }
    [Serializable] private class LineData { public List<PointData> points; }
    [Serializable] private class CollisionData { public List<LineData> groundLines; public List<LineData> platformLines; }
    [Serializable] private class MapData { public string format; public CanvasData canvas; public List<PieceData> instances; public CollisionData collisions; }

    private readonly struct ImportResult
    {
        public readonly bool Success; public readonly string Error; public readonly int Missing;
        public ImportResult(bool success, string error, int missing) { Success = success; Error = error; Missing = missing; }
    }

    [MenuItem("Tools/Web/Map Tracer/Import Map JSON as Prefab")]
    public static void ImportMap()
    {
        string jsonPath = EditorUtility.OpenFilePanel("Selecciona Map JSON", "", "json");
        if (string.IsNullOrEmpty(jsonPath)) return;
        string name = Path.GetFileNameWithoutExtension(jsonPath);
        string prefabPath = EditorUtility.SaveFilePanelInProject("Guardar prefab del escenario", name, "prefab", "Elige la ubicación del prefab.");
        if (string.IsNullOrEmpty(prefabPath)) return;

        ImportResult result = ImportOneMap(jsonPath, prefabPath);
        if (!result.Success) { EditorUtility.DisplayDialog("Map Importer", result.Error, "OK"); return; }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorUtility.DisplayDialog("Map Importer", $"Prefab creado. {result.Missing} sprites no se encontraron; importa sus PNG en Assets y usa nombres únicos.", "OK");
    }

    // EditorUtility has no native multi-file picker, so batch import works off a FOLDER instead:
    // pick a folder, import every *.json directly inside it in one pass. Each map's prefab is
    // saved as "<folder>/<jsonName>.prefab", next to the JSON it came from.
    [MenuItem("Tools/Web/Map Tracer/Import Map JSONs (Batch)...")]
    public static void ImportMapsBatch()
    {
        string folder = EditorUtility.OpenFolderPanel("Selecciona carpeta con Map JSONs", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;
        string[] jsonFiles = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
        if (jsonFiles.Length == 0) { EditorUtility.DisplayDialog("Map Importer (Batch)", "No se encontraron archivos .json en esa carpeta.", "OK"); return; }

        int created = 0, failed = 0, totalMissing = 0;
        List<string> warnings = new List<string>();
        foreach (string jsonPath in jsonFiles)
        {
            string name = Path.GetFileNameWithoutExtension(jsonPath);
            string prefabPath = ToProjectRelativePath(Path.Combine(folder, name + ".prefab"));
            if (prefabPath == null) { failed++; warnings.Add($"{name}: la carpeta debe estar dentro de Assets/."); continue; }

            ImportResult result = ImportOneMap(jsonPath, prefabPath);
            if (!result.Success) { failed++; warnings.Add($"{name}: {result.Error}"); continue; }
            created++;
            totalMissing += result.Missing;
            if (result.Missing > 0) warnings.Add($"{name}: {result.Missing} sprite(s) no encontrados.");
        }

        AssetDatabase.SaveAssets();
        string summary = $"{created} mapa(s) importados, {failed} fallidos, {totalMissing} sprites totales no encontrados.";
        if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
        EditorUtility.DisplayDialog("Map Importer (Batch)", summary, "OK");
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
        return "Assets" + full.Substring(dataPath.Length);
    }

    /// <summary>Shared import logic behind both the single-file and batch menu items. Reads, validates
    /// and builds one map's prefab at <paramref name="prefabPath"/>. Callers are responsible for
    /// AssetDatabase.SaveAssets() — ImportMap() calls it once per import, ImportMapsBatch() once
    /// for the whole batch.</summary>
    private static ImportResult ImportOneMap(string jsonPath, string prefabPath)
    {
        MapData map;
        try { map = JsonUtility.FromJson<MapData>(File.ReadAllText(jsonPath)); }
        catch (Exception exception) { return new ImportResult(false, "No se pudo leer el JSON:\n" + exception.Message, 0); }
        if (map == null || map.canvas == null || map.canvas.width <= 0 || map.canvas.height <= 0)
            return new ImportResult(false, "No es un Map JSON válido de RedMagic Map Tracer.", 0);
        int groundLayer = LayerMask.NameToLayer("Ground"), platformLayer = LayerMask.NameToLayer("Platform");
        if (groundLayer < 0 || platformLayer < 0)
            return new ImportResult(false, "Faltan las layers 'Ground' y/o 'Platform' en Project Settings > Tags and Layers.", 0);

        string name = Path.GetFileNameWithoutExtension(jsonPath);
        GameObject root = new GameObject(name);
        try
        {
            Transform background = CreateContainer("Background", root.transform);
            Transform platforms = CreateContainer("Platforms", root.transform);
            Transform border = CreateContainer("Border", root.transform);
            int missing = 0;
            foreach (PieceData piece in map.instances ?? new List<PieceData>())
            {
                Sprite sprite = FindSprite(piece.fileName, piece.assetName);
                if (sprite == null) { missing++; continue; }
                Transform parent = piece.type == "background" ? background : piece.type == "platform" ? platforms : border;
                GameObject visual = new GameObject(piece.assetName);
                visual.transform.SetParent(parent, false);
                float ppu = sprite.pixelsPerUnit; Vector2 pivot = sprite.pivot;
                float scaleX = Mathf.Approximately(piece.scaleX, 0f) ? 1f : piece.scaleX;
                float scaleY = Mathf.Approximately(piece.scaleY, 0f) ? 1f : piece.scaleY;
                // piece.x/piece.y anchor the TOP-LEFT of the RAW, UNTRIMMED image MapTracer.html
                // traced against (its canvas is sized to the uploaded PNG's own im.width/im.height —
                // see MapTracer.html's startTrace()/drawAsset()). sprite.rect, however, is relative
                // to Unity's imported texture and can be SMALLER than and OFFSET from that raw image
                // whenever the sprite was imported in Multiple mode with alpha-trimmed sub-sprites
                // (as the Map art under Assets/Art/Map/ actually is — verified: frame_003.png's raw
                // texture is 493x221 but its imported rect is x=9,y=8,w=475,h=204, i.e. trimmed 9px
                // off the top and 8px off the bottom, asymmetrically). Using rect.height/pivot alone
                // (as if rect == the full raw image) silently shifted every trimmed piece by its own
                // top/bottom trim difference — small, but exactly the "a few pixels off" collider
                // misalignment this was reported as, since AddColliders' points are computed purely
                // in raw-canvas space and have nothing to compensate with. sprite.texture.height is
                // the untrimmed raw dimension (Unity doesn't resize textures on import, only the
                // sprite rect within them), and rect.x/rect.y fold the trim's own offset back in.
                float rawHeight = sprite.texture.height;
                visual.transform.localPosition = new Vector3(
                    (piece.x - map.canvas.width * .5f + (pivot.x + sprite.rect.x) * scaleX) / ppu,
                    (map.canvas.height * .5f - piece.y - (rawHeight - sprite.rect.y) * scaleY + pivot.y * scaleY) / ppu, 0);
                visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.sortingOrder = piece.type == "background" ? 0 : piece.type == "platform" ? 10 : 20;
            }
            Transform collisions = new GameObject("Collisions").transform; collisions.SetParent(root.transform, false);
            AddColliders("Ground", map.collisions?.groundLines, collisions, groundLayer, map);
            AddColliders("Platform", map.collisions?.platformLines, collisions, platformLayer, map);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return new ImportResult(true, null, missing);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform CreateContainer(string name, Transform parent)
    { GameObject go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }

    private static void AddColliders(string type, List<LineData> lines, Transform parent, int layer, MapData map)
    {
        if (lines == null) return; int index = 0;

        // Collider points are already in the SAME absolute map-canvas pixel space as piece.x/piece.y
        // (MapTracer.html's sceneLines() bakes each traced point as point*scale + piece.x/y before
        // export), so unlike ImportMap's piece placement above they need no per-sprite pivot/height
        // adjustment of their own — a traced line can span several pieces and the exported JSON
        // (RedMagicMap/1) no longer records which piece each point came from, so there is nothing
        // to adjust per-point against even if we wanted to.
        //
        // What they DO need is the same px→unit scale the ground/platform art was actually placed
        // with. This is NOT a fixed 100-px/unit convention in this project — verified against the
        // real vendored art MapTracer pieces are built from: Assets/Brackeys/2D Mega Pack/Platforms
        // and /Environment (Platform_Blue.png, Dirt_Mid.png, ...) import at 10 px/unit, and
        // Assets/Cainos/.../Texture/TX Tileset Ground.png imports at 32 px/unit — neither is 100, so
        // a hardcoded 100 silently produced colliders scaled ~10x/~3x off from the ground/platform
        // sprites they should hug. Instead, read pixelsPerUnit from an actual piece of the matching
        // type in THIS map (border pieces feed groundLines, platform pieces feed platformLines — see
        // MapTracer.html's sceneLines()) so colliders always match whatever PPU that map's own art
        // was imported at.
        //
        // Known limitation (inherited from the export format, not fixable here): if a single map
        // mixes ground/platform pieces at different PPUs, every line of that collider type still
        // shares ONE ppu (the first matching piece found) — the JSON has no per-point piece
        // reference to do better. Keep ground/platform art for one map at a consistent PPU.
        float ppu = FindRepresentativePpu(map, type == "Ground" ? "border" : "platform");

        foreach (LineData lineData in lines)
        {
            List<PointData> line = lineData?.points;
            if (line == null || line.Count < 2) continue;
            GameObject go = new GameObject($"{type}_Edge_{index++}"); go.layer = layer; go.transform.SetParent(parent, false);
            EdgeCollider2D edge = go.AddComponent<EdgeCollider2D>(); Vector2[] points = new Vector2[line.Count];
            for (int i = 0; i < line.Count; i++) points[i] = new Vector2((line[i].x - map.canvas.width * .5f) / ppu, (map.canvas.height * .5f - line[i].y) / ppu);
            edge.points = points;
        }
    }

    /// <summary>Pixels-per-unit of the first placed piece of <paramref name="pieceType"/> ("border"/"platform")
    /// whose sprite actually resolves — see AddColliders' comment for why colliders must match this
    /// instead of a hardcoded value. Falls back to 100 (with a loud warning) only when the map has no
    /// resolvable piece of that type to read a real PPU from.</summary>
    private static float FindRepresentativePpu(MapData map, string pieceType)
    {
        foreach (PieceData piece in map.instances ?? new List<PieceData>())
        {
            if (piece.type != pieceType) continue;
            Sprite sprite = FindSprite(piece.fileName, piece.assetName);
            if (sprite != null) return sprite.pixelsPerUnit;
        }
        Debug.LogWarning($"Map Importer: no se encontró ninguna pieza '{pieceType}' con sprite resoluble para leer su pixelsPerUnit real; " +
                          "las colisiones de ese tipo usarán 100 por defecto y podrían no coincidir con el arte.");
        return 100f;
    }

    // internal, not private: MapPiecesImporter below (a different DTO shape / prefab layout — see
    // its own header comment for why it isn't folded into ImportOneMap) reuses sprite resolution
    // and the folder-panel-to-project-path conversion rather than duplicating them.
    internal static Sprite FindSprite(string fileName, string assetName)
    {
        string stem = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(fileName) ? assetName : fileName);
        foreach (string guid in AssetDatabase.FindAssets(stem + " t:Sprite"))
        { string path = AssetDatabase.GUIDToAssetPath(guid); if (Path.GetFileNameWithoutExtension(path).Equals(stem, StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<Sprite>(path); }
        return null;
    }

    internal static string ToProjectRelativePathPublic(string absolutePath) => ToProjectRelativePath(absolutePath);
}

// Imports the web tool's "⬇ Exportar piezas seleccionadas" / per-piece "⬇ Exportar" output from
// the Map Tracer's Biblioteca panel (modules/map-export.js's buildPiecesJson, format
// "RedMagicMapPieces/1") — a list of STANDALONE pieces (no canvas, no instance placement), each
// becoming its own prefab. Deliberately a separate class from MapImporter: the JSON shape and the
// resulting prefab layout are both different (one piece = one prefab, no Background/Platforms/
// Border containers, no map.canvas to center against — see ImportOnePiece's comment for how the
// centering math adapts), so unifying the two entry points would just be an if/else in disguise.
// What IS shared (sprite lookup, folder→project-path conversion) is reused from MapImporter above
// instead of duplicated.
public static class MapPiecesImporter
{
    [Serializable] private class PointData { public float x; public float y; }
    [Serializable] private class LineData { public List<PointData> points; }
    [Serializable] private class PieceEntry
    {
        public string assetName; public string fileName; public string type;
        public float width; public float height; public List<LineData> lines;
    }
    [Serializable] private class PiecesData { public string format; public List<PieceEntry> pieces; }

    [MenuItem("Tools/Web/Map Tracer/Import Map Pieces as Prefabs...")]
    public static void ImportPieces()
    {
        string jsonPath = EditorUtility.OpenFilePanel("Selecciona Map Pieces JSON", "", "json");
        if (string.IsNullOrEmpty(jsonPath)) return;

        PiecesData data;
        try { data = JsonUtility.FromJson<PiecesData>(File.ReadAllText(jsonPath)); }
        catch (Exception exception) { EditorUtility.DisplayDialog("Map Pieces Importer", "No se pudo leer el JSON:\n" + exception.Message, "OK"); return; }
        if (data == null || data.pieces == null || data.pieces.Count == 0)
        { EditorUtility.DisplayDialog("Map Pieces Importer", "No es un Map Pieces JSON válido de RedMagic Map Tracer (RedMagicMapPieces/1).", "OK"); return; }
        int groundLayer = LayerMask.NameToLayer("Ground"), platformLayer = LayerMask.NameToLayer("Platform");
        if (groundLayer < 0 || platformLayer < 0)
        { EditorUtility.DisplayDialog("Map Pieces Importer", "Faltan las layers 'Ground' y/o 'Platform' en Project Settings > Tags and Layers.", "OK"); return; }

        string folder = EditorUtility.SaveFolderPanel("Carpeta para los prefabs de piezas", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;
        string projectFolder = MapImporter.ToProjectRelativePathPublic(folder);
        if (projectFolder == null) { EditorUtility.DisplayDialog("Map Pieces Importer", "La carpeta debe estar dentro de Assets/.", "OK"); return; }

        int created = 0, missingTotal = 0;
        List<string> warnings = new List<string>();
        foreach (PieceEntry piece in data.pieces)
        {
            Sprite sprite = MapImporter.FindSprite(piece.fileName, piece.assetName);
            if (sprite == null) { missingTotal++; warnings.Add($"{piece.assetName}: sprite no encontrado."); continue; }

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{projectFolder}/{piece.assetName}.prefab");
            GameObject root = new GameObject(piece.assetName);
            try
            {
                BuildPiecePrefab(root, piece, sprite, groundLayer, platformLayer);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                created++;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        AssetDatabase.SaveAssets();
        string summary = $"{created} pieza(s) importadas como prefab, {missingTotal} sprite(s) no encontrados.";
        if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
        EditorUtility.DisplayDialog("Map Pieces Importer", summary, "OK");
    }

    private static void BuildPiecePrefab(GameObject root, PieceEntry piece, Sprite sprite, int groundLayer, int platformLayer)
    {
        float ppu = sprite.pixelsPerUnit; Vector2 pivot = sprite.pivot;
        float rawHeight = sprite.texture.height;

        // Same trim-compensation math as MapImporter.ImportOneMap (see its long comment above), but
        // there is no map.canvas and no instance offset here — a standalone piece is placed at its
        // OWN local origin, so map.canvas.width/height (the map-wide centering term) is simply
        // replaced by this piece's own raw width/height, and piece.x/piece.y (an instance's map
        // placement) drop out entirely (equivalent to always 0).
        GameObject visual = new GameObject("Sprite");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(
            (-piece.width * .5f + pivot.x + sprite.rect.x) / ppu,
            (piece.height * .5f - (rawHeight - sprite.rect.y) + pivot.y) / ppu, 0);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sortingOrder = piece.type == "background" ? 0 : piece.type == "platform" ? 10 : 20;

        if (piece.type == "background" || piece.lines == null || piece.lines.Count == 0) return;
        int layer = piece.type == "platform" ? platformLayer : groundLayer;
        Transform collisions = new GameObject("Collisions").transform; collisions.SetParent(root.transform, false);
        int index = 0;
        foreach (LineData lineData in piece.lines)
        {
            List<PointData> line = lineData?.points;
            if (line == null || line.Count < 2) continue;
            GameObject go = new GameObject($"Edge_{index++}"); go.layer = layer; go.transform.SetParent(collisions, false);
            EdgeCollider2D edge = go.AddComponent<EdgeCollider2D>(); Vector2[] points = new Vector2[line.Count];
            // Piece-local points are in the SAME raw top-left-origin space the tracing canvas used
            // (piece.width x piece.height — MapTracer's startTrace()/MapTracer's map-canvas centering
            // in MapImporter.AddColliders, here with the piece's own dimensions standing in for the
            // map's), with no pivot adjustment — matching AddColliders, which never adjusts for pivot
            // either since collider points are already in that raw-image frame, not sprite-pivot space.
            for (int i = 0; i < line.Count; i++)
                points[i] = new Vector2((line[i].x - piece.width * .5f) / ppu, (piece.height * .5f - line[i].y) / ppu);
            edge.points = points;
        }
    }
}
