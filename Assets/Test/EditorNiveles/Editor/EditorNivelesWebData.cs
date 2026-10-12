using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.EditorNiveles
{
    /// <summary>
    /// Escribe <c>Web/RedMagicWeb/assets/niveles/datos-unity.json</c> para la pestaña 🧱 Niveles de la web: los tiles
    /// que se pueden pintar y, por bioma, los valores del <see cref="LevelBiome"/> y las capas de su fondo de parallax
    /// (texturas por su ruta del proyecto; las sirve el servidor del taller). Así la vista previa dibuja con lo mismo que
    /// Unity sin copiar números a mano. Se regenera solo al guardar un bioma o su fondo (<see cref="Watcher"/>) y al importar.
    /// </summary>
    public static class EditorNivelesWebData
    {
        [Serializable] private class Data { public TileInfo[] tiles; public BiomeInfo[] biomes; }
        [Serializable] private class TileInfo { public string name, layer; }

        [Serializable]
        private class BiomeInfo
        {
            public string name, rockTexture;
            public float rockTileUnits, cornerRadius, floorWidth, wallWidth, ceilingWidth, widthVariation, outlineWidth, innerGlowWidth;
            public Color rockShadow, outline, innerGlow;
            public Color[] floorBands, ceilingBands;
            public float[] bandFractions;
            public LayerInfo[] layers;
        }

        [Serializable]
        private class LayerInfo
        {
            public string texture, repeat, anchor, align;
            public float speed, positionX, size, heightOffset, travel, verticalSpeed, opacity, fade;
            public Color fogColor;
            public bool inFront, overTerrain, sitOnGround;
            public int sortingOrder;
        }

        public static string FilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Web", "RedMagicWeb", "assets", "niveles", "datos-unity.json"));

        public static void Write(bool log = false)
        {
            var data = new Data { tiles = Tiles(), biomes = Biomes() };
            string json = JsonUtility.ToJson(data, true);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, json);
            if (log) Debug.Log($"[EditorNiveles] {data.biomes.Length} biomas y {data.tiles.Length} tiles → {FilePath}");
        }

        /// <summary>Roca (terreno) y todo tile llamado "Plataforma…" (plataformas): el tipo nuevo aparece solo en la web.</summary>
        private static TileInfo[] Tiles()
        {
            var list = new List<TileInfo> { new TileInfo { name = LevelSketchFile.RockTile, layer = LevelSketchFile.TerrainLayer } };
            var platforms = AssetDatabase.FindAssets("t:TileBase", new[] { LevelSetup.BiomesRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null && t.name.StartsWith(LevelSketchFile.PlatformTile))
                .Select(t => t.name).Distinct()
                .OrderBy(n => n == LevelSketchFile.PlatformTile ? "" : n);
            foreach (var n in platforms) list.Add(new TileInfo { name = n, layer = LevelSketchFile.PlatformsLayer });
            return list.ToArray();
        }

        private static BiomeInfo[] Biomes()
        {
            return AssetDatabase.FindAssets("t:LevelBiome")
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelBiome>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b != null)
                .Select(b => new BiomeInfo
                {
                    name = b.name, rockTexture = WebPath(b.rockTexture), rockTileUnits = b.rockTileUnits,
                    rockShadow = b.rockShadow, cornerRadius = b.cornerRadius, floorWidth = b.floorWidth, wallWidth = b.wallWidth,
                    ceilingWidth = b.ceilingWidth, widthVariation = b.widthVariation, floorBands = b.floorBands,
                    ceilingBands = b.ceilingBands, bandFractions = b.bandFractions, outline = b.outline,
                    outlineWidth = b.outlineWidth, innerGlow = b.innerGlow, innerGlowWidth = b.innerGlowWidth,
                    layers = Layers(b.background),
                }).ToArray();
        }

        private static LayerInfo[] Layers(GameObject background)
        {
            if (background == null) return new LayerInfo[0];
            var list = new List<LayerInfo>();
            foreach (var layer in background.GetComponentsInChildren<ParallaxBackgroundLayer>(true))
            {
                var so = new SerializedObject(layer);
                string Enum(string p) { var sp = so.FindProperty(p); return sp.enumNames[sp.enumValueIndex]; }
                float F(string p) => so.FindProperty(p).floatValue;
                bool B(string p) => so.FindProperty(p).boolValue;
                list.Add(new LayerInfo
                {
                    texture = WebPath(so.FindProperty("texture").objectReferenceValue), repeat = Enum("repeat"),
                    anchor = Enum("anchor"), align = Enum("align"), speed = F("speed"), positionX = F("positionX"),
                    size = F("size"), heightOffset = F("heightOffset"), travel = F("travel"), verticalSpeed = F("verticalSpeed"),
                    opacity = F("opacity"), fade = F("fade"), fogColor = so.FindProperty("fogColor").colorValue,
                    inFront = B("inFront"), overTerrain = B("overTerrain"), sitOnGround = B("sitOnGround"),
                    sortingOrder = so.FindProperty("sortingOrder").intValue,
                });
            }
            return list.ToArray();
        }

        /// <summary>Ruta del asset en el proyecto (Assets/…).</summary>
        private static string WebPath(UnityEngine.Object asset) =>
            asset == null ? null : AssetDatabase.GetAssetPath(asset);

        /// <summary>Guardar un bioma, su fondo o un tile de <see cref="LevelSetup.BiomesRoot"/> rehace el archivo.</summary>
        private class Watcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var p in imported)
                    if (p.StartsWith(LevelSetup.BiomesRoot + "/") && (p.EndsWith(".asset") || p.EndsWith(".prefab")))
                    {
                        EditorApplication.delayCall -= Rewrite;
                        EditorApplication.delayCall += Rewrite;
                        return;
                    }
            }

            private static void Rewrite() => Write();
        }
    }
}
