using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.Fx.EditorTools
{
    /// <summary>
    /// Genera las variantes del tajo melee (<see cref="MeleeSlashFx"/>) como prefabs en
    /// <c>Assets/Prefabs/Fx/Player/Slash/</c> y una escena de prueba que las repite en bucle
    /// (<c>Assets/Scenes/Test/SlashPreview.unity</c>) para elegir una a ojo. Sobrescribe los prefabs:
    /// es un punto de partida, el ajuste fino se hace después en el Inspector del prefab elegido.
    /// </summary>
    public static class MeleeSlashPack
    {
        private const string Folder = "Assets/Prefabs/Fx/Player/Slash";
        private const string ScenePath = "Assets/Scenes/Test/SlashPreview.unity";

        private static readonly Color White = new Color(1f, 1f, 1f);
        private static readonly Color LightCyan = new Color(0.62f, 0.95f, 1f);
        private static readonly Color Cyan = new Color(0.16f, 0.76f, 1f);
        private static readonly Color DeepBlue = new Color(0.10f, 0.36f, 0.86f);
        private static readonly Color Purple = new Color(0.46f, 0.22f, 0.86f);

        // Mismos nombres que los campos serializados de MeleeSlashFx → JsonUtility.FromJsonOverwrite.
        [Serializable]
        private sealed class Preset
        {
            public float worldRadius = 2.5f;
            public float arcDegrees = 220f;
            public float startAngle = 110f;
            public bool clockwise = true;
            public float duration = 0.32f;
            public float headPortion = 0.45f;
            public float tailStart = 0.2f;
            public float fadePortion = 0.2f;
            public MeleeSlashFx.Layer[] layers;
            public bool previewLoop;
        }

        [MenuItem("Tools/RedMagic/FX/Tajo melee · Generar variantes + escena de prueba")]
        public static void Generate()
        {
            EnsureFolder(Folder);
            var shader = Shader.Find("RedMagic/Melee Slash Layer");
            if (shader == null) { Debug.LogError("[MeleeSlashPack] Falta el shader RedMagic/Melee Slash Layer."); return; }

            var variants = new (string name, Preset preset)[]
            {
                ("Fx_Slash_A_Viento", Exterior(Viento(), MeleeSlashFx.ShardStyle.Chispas, 22, 14)),
                ("Fx_Slash_B_Luna", Luna()),
                ("Fx_Slash_C_Tormenta", Exterior(Tormenta(), MeleeSlashFx.ShardStyle.Chispas, 26, 16)),
                ("Fx_Slash_D_Filo", Filo()),
            };

            var prefabs = new GameObject[variants.Length];
            for (int i = 0; i < variants.Length; i++)
                prefabs[i] = BuildPrefab(variants[i].name, variants[i].preset, shader);

            BuildScene(prefabs, ScenePath, new[]
            {
                new Vector3(-5.5f, 3.2f), new Vector3(5.5f, 3.2f),
                new Vector3(-5.5f, -3.6f), new Vector3(5.5f, -3.6f),
            }, 7f);
            Debug.Log($"[MeleeSlashPack] {prefabs.Length} variantes en {Folder}, escena {ScenePath}.");
        }

        private const string ExteriorScenePath = "Assets/Scenes/Test/SlashPreview_Exterior.unity";

        /// <summary>
        /// La variante A con cada estilo de parte exterior (y sin ella), para elegir a ojo. Las
        /// partículas van en el núcleo y en la capa cian, del color de cada capa.
        /// </summary>
        [MenuItem("Tools/RedMagic/FX/Tajo melee · Variantes de parte exterior")]
        public static void GenerateExterior()
        {
            EnsureFolder(Folder);
            var shader = Shader.Find("RedMagic/Melee Slash Layer");
            if (shader == null) { Debug.LogError("[MeleeSlashPack] Falta el shader RedMagic/Melee Slash Layer."); return; }

            var variants = new (string name, Preset preset)[]
            {
                ("Fx_Slash_A_Ext1_Rombos", Exterior(Viento(), MeleeSlashFx.ShardStyle.Rombo, 14, 0)),
                ("Fx_Slash_A_Ext3_Chispas", Exterior(Viento(), MeleeSlashFx.ShardStyle.Chispas, 22, 14)),
                ("Fx_Slash_A_Ext5_Sin", Exterior(Viento(), MeleeSlashFx.ShardStyle.Rombo, 0, 0)),
            };
            var prefabs = new GameObject[variants.Length];
            for (int i = 0; i < variants.Length; i++)
                prefabs[i] = BuildPrefab(variants[i].name, variants[i].preset, shader);

            var slots = new[]
            {
                new Vector3(-12f, 4.5f), new Vector3(0f, 4.5f), new Vector3(12f, 4.5f),

            };
            BuildScene(prefabs, ExteriorScenePath, slots, 10.5f);
            Debug.Log($"[MeleeSlashPack] {prefabs.Length} variantes de parte exterior, escena {ExteriorScenePath}.");
        }

        // Elegido (2026-10-09): Chispas para A y C. Rombos y Sin se conservan como alternativas.
        private static Preset Exterior(Preset p, MeleeSlashFx.ShardStyle style, int coreCount, int cyanCount)
        {
            foreach (var l in p.layers)
            {
                l.shardStyle = style;
                l.shards = l.label == "Núcleo" ? coreCount : l.label == "Cian" ? cyanCount : 0;
                l.shardDrift = style == MeleeSlashFx.ShardStyle.Rayas ? 0.2f : 0.5f;
            }
            return p;
        }

        private static GameObject BuildPrefab(string name, Preset preset, Shader shader)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<MeleeSlashFx>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset), fx);
            var so = new SerializedObject(fx);
            so.FindProperty("shader").objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{Folder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static void BuildScene(GameObject[] prefabs, string scenePath, Vector3[] slots, float orthoSize)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.18f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            for (int i = 0; i < prefabs.Length && i < slots.Length; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                inst.transform.position = slots[i];
                var so = new SerializedObject(inst.GetComponent<MeleeSlashFx>());
                so.FindProperty("previewLoop").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Referencia de tamaño: un "personaje" de 1×2 unidades en el origen del tajo.
                var stand = GameObject.CreatePrimitive(PrimitiveType.Quad);
                stand.name = "PlayerStandIn";
                UnityEngine.Object.DestroyImmediate(stand.GetComponent<Collider>());
                stand.transform.position = slots[i] + new Vector3(0f, -0.2f, 1f);
                stand.transform.localScale = new Vector3(0.9f, 1.8f, 1f);
                var mr = stand.GetComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                mr.sortingLayerName = "Characters";
                mr.sortingOrder = 1;
            }

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // ── Variantes ────────────────────────────────────────────────────────────────

        /// <summary>A · la más cercana a la referencia: 5 capas, cola en lenguas, estela morada, esquirlas.</summary>
        private static Preset Viento() => new Preset
        {
            arcDegrees = 230f, duration = 0.34f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.12f, 0.12f, fray: 0.45f, bands: 8f, tailTaper: 2.2f, innerJag: 0.5f, seed: 1),
                L("Azul", DeepBlue, 0.93f, 0.55f, 0.06f, fray: 0.4f, bands: 7f, innerJag: 0.45f, breakup: 0.4f, seed: 2),
                L("Cian", Cyan, 0.91f, 0.46f, 0.035f, fray: 0.32f, bands: 6f, innerJag: 0.35f, breakup: 0.3f, seed: 3),
                L("Cian claro", LightCyan, 0.90f, 0.36f, 0.015f, fray: 0.25f, bands: 5f, innerJag: 0.3f, seed: 4),
                L("Núcleo", White, 0.89f, 0.25f, 0f, fray: 0.15f, bands: 4f, innerJag: 0.2f, breakup: 0.15f, shards: 14, seed: 5),
            },
        };

        /// <summary>B · limpia y contundente: 3 capas sin deshilachar, media luna gruesa y rápida.</summary>
        private static Preset Luna() => new Preset
        {
            arcDegrees = 200f, duration = 0.26f, headPortion = 0.4f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.93f, 0.50f, 0.04f, fray: 0f, outerJag: 0.02f, innerJag: 0f, breakup: 0f, headTaper: 0.5f, seed: 1),
                L("Cian", Cyan, 0.92f, 0.40f, 0.02f, fray: 0f, outerJag: 0.02f, innerJag: 0f, breakup: 0f, headTaper: 0.5f, seed: 2),
                L("Núcleo", White, 0.91f, 0.27f, 0f, fray: 0f, outerJag: 0.01f, innerJag: 0f, breakup: 0f, headTaper: 0.5f, seed: 3),
            },
        };

        /// <summary>C · salvaje: 6 capas abiertas en abanico, mucho deshilachado y esquirlas.</summary>
        private static Preset Tormenta() => new Preset
        {
            arcDegrees = 260f, duration = 0.42f, tailStart = 0.25f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.16f, 0.16f, fray: 0.6f, bands: 10f, innerJag: 0.7f, breakup: 0.5f, shards: 10, shardDrift: 0.35f, seed: 11),
                L("Azul", DeepBlue, 0.93f, 0.60f, 0.09f, fray: 0.55f, bands: 9f, innerJag: 0.6f, breakup: 0.5f, seed: 12),
                L("Cian", Cyan, 0.90f, 0.50f, 0.06f, fray: 0.5f, bands: 8f, innerJag: 0.55f, breakup: 0.45f, seed: 13),
                L("Cian 2", Cyan, 0.72f, 0.28f, 0.11f, fray: 0.5f, bands: 6f, innerJag: 0.6f, breakup: 0.5f, seed: 14),
                L("Cian claro", LightCyan, 0.88f, 0.38f, 0.03f, fray: 0.4f, bands: 7f, innerJag: 0.45f, breakup: 0.3f, seed: 15),
                L("Núcleo", White, 0.87f, 0.24f, 0f, fray: 0.3f, bands: 5f, innerJag: 0.3f, breakup: 0.2f, shards: 24, seed: 16),
            },
        };

        /// <summary>D · fina y larga: arco casi completo, muy rápida, cola larguísima.</summary>
        private static Preset Filo() => new Preset
        {
            arcDegrees = 300f, startAngle = 150f, duration = 0.24f, headPortion = 0.5f, tailStart = 0.15f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.95f, 0.05f, 0.10f, fray: 0.3f, bands: 6f, tailTaper: 3f, innerJag: 0.3f, seed: 21),
                L("Cian", Cyan, 0.92f, 0.13f, 0.03f, fray: 0.2f, bands: 5f, tailTaper: 2.6f, innerJag: 0.2f, seed: 22),
                L("Núcleo", White, 0.91f, 0.06f, 0f, fray: 0.1f, bands: 4f, tailTaper: 2.6f, innerJag: 0.1f, shards: 8, seed: 23),
            },
        };

        private static MeleeSlashFx.Layer L(string label, Color color, float radius, float thickness, float delay,
            float fray = 0.25f, float bands = 6f, float headTaper = 0.6f, float tailTaper = 1.6f,
            float outerJag = 0.05f, float innerJag = 0.2f, float breakup = 0.3f, int shards = 0,
            float shardDrift = 0.25f, float seed = 0f) => new MeleeSlashFx.Layer
        {
            label = label, color = color, radius = radius, thickness = thickness, delay = delay,
            fraySpread = fray, frayBands = bands, headTaper = headTaper, tailTaper = tailTaper,
            outerJag = outerJag, innerJag = innerJag, breakup = breakup, shards = shards,
            shardDrift = shardDrift, seed = seed,
        };

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
