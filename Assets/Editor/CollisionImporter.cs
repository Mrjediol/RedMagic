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

    [MenuItem("Tools/RedMagic/Map Tracer/Import Map JSON as Prefab")]
    public static void ImportMap()
    {
        string jsonPath = EditorUtility.OpenFilePanel("Selecciona Map JSON", "", "json");
        if (string.IsNullOrEmpty(jsonPath)) return;
        MapData map;
        try { map = JsonUtility.FromJson<MapData>(File.ReadAllText(jsonPath)); }
        catch (Exception exception) { EditorUtility.DisplayDialog("Map Importer", "No se pudo leer el JSON:\n" + exception.Message, "OK"); return; }
        if (map == null || map.canvas == null || map.canvas.width <= 0 || map.canvas.height <= 0)
        { EditorUtility.DisplayDialog("Map Importer", "No es un Map JSON válido de RedMagic Map Tracer.", "OK"); return; }
        int groundLayer = LayerMask.NameToLayer("Ground"), platformLayer = LayerMask.NameToLayer("Platform");
        if (groundLayer < 0 || platformLayer < 0)
        { EditorUtility.DisplayDialog("Map Importer", "Faltan las layers 'Ground' y/o 'Platform' en Project Settings > Tags and Layers.", "OK"); return; }
        string name = Path.GetFileNameWithoutExtension(jsonPath);
        string prefabPath = EditorUtility.SaveFilePanelInProject("Guardar prefab del escenario", name, "prefab", "Elige la ubicación del prefab.");
        if (string.IsNullOrEmpty(prefabPath)) return;

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
                visual.transform.localPosition = new Vector3((piece.x - map.canvas.width * .5f + pivot.x * scaleX) / ppu, (map.canvas.height * .5f - piece.y - sprite.rect.height * scaleY + pivot.y * scaleY) / ppu, 0);
                visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.sortingOrder = piece.type == "background" ? 0 : piece.type == "platform" ? 10 : 20;
            }
            Transform collisions = new GameObject("Collisions").transform; collisions.SetParent(root.transform, false);
            AddColliders("Ground", map.collisions?.groundLines, collisions, groundLayer, map);
            AddColliders("Platform", map.collisions?.platformLines, collisions, platformLayer, map);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath); AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorUtility.DisplayDialog("Map Importer", $"Prefab creado. {missing} sprites no se encontraron; importa sus PNG en Assets y usa nombres únicos.", "OK");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform CreateContainer(string name, Transform parent)
    { GameObject go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }

    private static void AddColliders(string type, List<LineData> lines, Transform parent, int layer, MapData map)
    {
        if (lines == null) return; int index = 0;
        foreach (LineData lineData in lines)
        {
            List<PointData> line = lineData?.points;
            if (line == null || line.Count < 2) continue;
            GameObject go = new GameObject($"{type}_Edge_{index++}"); go.layer = layer; go.transform.SetParent(parent, false);
            EdgeCollider2D edge = go.AddComponent<EdgeCollider2D>(); const float ppu = 100f; Vector2[] points = new Vector2[line.Count];
            for (int i = 0; i < line.Count; i++) points[i] = new Vector2((line[i].x - map.canvas.width * .5f) / ppu, (map.canvas.height * .5f - line[i].y) / ppu);
            edge.points = points;
        }
    }

    private static Sprite FindSprite(string fileName, string assetName)
    {
        string stem = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(fileName) ? assetName : fileName);
        foreach (string guid in AssetDatabase.FindAssets(stem + " t:Sprite"))
        { string path = AssetDatabase.GUIDToAssetPath(guid); if (Path.GetFileNameWithoutExtension(path).Equals(stem, StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<Sprite>(path); }
        return null;
    }
}
