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
        [Serializable]
        private class TileInfo
        {
            public string name, layer;
            /// <summary>Plataforma de arte: sprite de cada pieza (extremos, centro, suelta), ruta Assets/….</summary>
            public string left, center, right, single;
            /// <summary>Puerta: imagen apagada / encendida, cómo empieza y el tamaño admitido en celdas.</summary>
            public string doorOff, doorOn;
            public bool startOpen;
            public int minWide, maxWide, minHigh, maxHigh;
        }

        [Serializable]
        private class BiomeInfo
        {
            public string name, rockTexture, backgroundName;
            public float rockTileUnits, cornerRadius, floorWidth, wallWidth, ceilingWidth, widthVariation, outlineWidth, innerGlowWidth;
            public Color rockShadow, outline, innerGlow;
            public Color[] floorBands, ceilingBands;
            public float[] bandFractions;
            public LayerInfo[] layers;
        }

        [Serializable]
        private class LayerInfo
        {
            public string name, texture, repeat, anchor, align;
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

        /// <summary>
        /// Roca (terreno), todo tile llamado "Plataforma…" (plataformas), y todo <see cref="ArtPlatformTile"/> y
        /// <see cref="DoorTile"/> de <see cref="LevelSetup.BiomesRoot"/>: el tipo nuevo aparece solo en la web.
        /// </summary>
        private static TileInfo[] Tiles()
        {
            var list = new List<TileInfo> { new TileInfo { name = LevelSketchFile.RockTile, layer = LevelSketchFile.TerrainLayer } };
            var all = AssetDatabase.FindAssets("t:TileBase", new[] { LevelSetup.BiomesRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .GroupBy(t => t.name).Select(g => g.First())
                .OrderBy(t => t.name == LevelSketchFile.PlatformTile ? "" : t.name)
                .ToList();
            foreach (var t in all.Where(t => t.name.StartsWith(LevelSketchFile.PlatformTile) && !(t is ArtPlatformTile) && !(t is DoorTile)))
                list.Add(new TileInfo { name = t.name, layer = LevelSketchFile.PlatformsLayer });
            foreach (var t in all.OfType<ArtPlatformTile>())
                list.Add(new TileInfo
                {
                    name = t.name, layer = LevelSketchFile.ArtPlatformsLayer,
                    left = WebPath(t.left), center = WebPath(t.center), right = WebPath(t.right), single = WebPath(t.single),
                });
            foreach (var t in all.OfType<DoorTile>())
            {
                var info = new TileInfo
                {
                    name = t.name, layer = LevelSketchFile.DoorsLayer, startOpen = t.startOpen,
                    minWide = LevelDoor.MinWide, maxWide = LevelDoor.MaxWide, minHigh = LevelDoor.MinHigh, maxHigh = LevelDoor.MaxHigh,
                };
                if (t.doorPrefab != null)
                {
                    var so = new SerializedObject(t.doorPrefab);
                    info.doorOff = WebPath(so.FindProperty("offTexture").objectReferenceValue);
                    info.doorOn = WebPath(so.FindProperty("onTexture").objectReferenceValue);
                }
                list.Add(info);
            }
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
                    layers = Layers(b.background), backgroundName = b.background != null ? b.background.name : null,
                }).ToArray();
        }

        private static LayerInfo[] Layers(GameObject background)
        {
            if (background == null) return new LayerInfo[0];
            var list = new List<LayerInfo>();
            // sólo las capas de primer nivel (las hijas de una capa, como los escalones, no son capas)
            foreach (var layer in background.GetComponentsInChildren<ParallaxBackgroundLayer>(true).Where(l => l.transform.parent == background.transform))
            {
                var so = new SerializedObject(layer);
                string Enum(string p) { var sp = so.FindProperty(p); return sp.enumNames[sp.enumValueIndex]; }
                float F(string p) => so.FindProperty(p).floatValue;
                bool B(string p) => so.FindProperty(p).boolValue;
                list.Add(new LayerInfo
                {
                    name = layer.name, texture = WebPath(so.FindProperty("texture").objectReferenceValue), repeat = Enum("repeat"),
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
