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
            public bool symmetric;
            public Vector2 shapeStretch = Vector2.one;
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

        private static GameObject BuildPrefab(string name, Preset preset, Shader shader, string folder = Folder)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<MeleeSlashFx>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset), fx);
            var so = new SerializedObject(fx);
            so.FindProperty("shader").objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{folder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static void BuildScene(GameObject[] prefabs, string scenePath, Vector3[] slots, float orthoSize,
                                       Vector3[] fxOffsets = null)
        {
            // Aditiva: no guarda, no cierra ni toca las escenas que el usuario tenga abiertas.
            var previousActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

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
                inst.transform.position = slots[i] + (fxOffsets != null ? fxOffsets[i] : Vector3.zero);
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
            if (previousActive.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }

        // ── Movimiento (salto, doble salto, dash) ─────────────────────────────────────
        // Mismo shader y paleta que los tajos, para que todo se lea como un set.

        private const string MovementFolder = "Assets/Prefabs/Fx/Player/Movement";
        private const string MovementScenePath = "Assets/Scenes/Test/MovementFxPreview.unity";

        /// <summary>Pies del muñeco de la escena respecto a su casilla (muñeco 1.8 de alto, centrado 0.2 más abajo).</summary>
        private const float FeetY = -1.1f;

        [MenuItem("Tools/RedMagic/FX/Movimiento · Generar variantes + escena de prueba")]
        public static void GenerateMovement()
        {
            EnsureFolder(MovementFolder);
            var shader = Shader.Find("RedMagic/Melee Slash Layer");
            if (shader == null) { Debug.LogError("[MeleeSlashPack] Falta el shader RedMagic/Melee Slash Layer."); return; }

            var rows = new (string prefix, Preset[] presets, Vector3 offset)[]
            {
                ("Fx_Jump", new[] { Jump1(), Jump2(), Jump3(), Jump4() }, new Vector3(0f, FeetY + 0.8f)),
                ("Fx_DoubleJump", new[] { Double1(), Double2(), Double3(), Double4() }, new Vector3(0f, FeetY + 0.1f)),
                ("Fx_Dash", new[] { Dash1(), Dash2(), Dash3(), Dash4() }, new Vector3(0.8f, -0.2f)),
            };

            var prefabs = new System.Collections.Generic.List<GameObject>();
            var slots = new System.Collections.Generic.List<Vector3>();
            var offsets = new System.Collections.Generic.List<Vector3>();
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].presets.Length; c++)
            {
                prefabs.Add(BuildPrefab($"{rows[r].prefix}_{c + 1}", rows[r].presets[c], shader, MovementFolder));
                slots.Add(new Vector3(-12f + c * 8f, 6f - r * 6.5f));
                offsets.Add(rows[r].offset);
            }

            BuildScene(prefabs.ToArray(), MovementScenePath, slots.ToArray(), 10.5f, offsets.ToArray());
            Debug.Log($"[MeleeSlashPack] {prefabs.Count} variantes de movimiento en {MovementFolder}, escena {MovementScenePath}.");
        }

        // Salto: salpicadura en los pies que se abre hacia los dos lados (simétrica, centro abajo).
        private static Preset Jump1() => new Preset   // media luna limpia
        {
            worldRadius = 1.7f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 180f,
            shapeStretch = new Vector2(1.5f, 0.55f), duration = 0.3f, headPortion = 0.4f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.95f, 0.30f, 0.04f, fray: 0.1f, innerJag: 0.1f, breakup: 0.1f, seed: 31),
                L("Cian", Cyan, 0.93f, 0.22f, 0.02f, fray: 0.1f, innerJag: 0.1f, breakup: 0.1f, seed: 32),
                L("Núcleo", White, 0.91f, 0.12f, 0f, fray: 0.05f, innerJag: 0.05f, breakup: 0.05f, seed: 33),
            },
        };

        private static Preset Jump2() => new Preset   // alas: más abierta, deshilachada, con chispas
        {
            worldRadius = 1.8f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 230f,
            shapeStretch = new Vector2(1.4f, 0.75f), duration = 0.36f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.10f, 0.08f, fray: 0.4f, bands: 7f, innerJag: 0.4f, seed: 41),
                L("Azul", DeepBlue, 0.94f, 0.38f, 0.05f, fray: 0.4f, bands: 7f, innerJag: 0.45f, breakup: 0.35f, seed: 42),
                L("Cian", Cyan, 0.92f, 0.28f, 0.025f, fray: 0.3f, innerJag: 0.35f, breakup: 0.25f, seed: 43),
                Spark(L("Núcleo", White, 0.90f, 0.15f, 0f, fray: 0.15f, innerJag: 0.2f, breakup: 0.1f, seed: 44), 12),
            },
        };

        private static Preset Jump3() => new Preset   // salpicadura baja con muchas chispas
        {
            worldRadius = 1.6f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 150f,
            shapeStretch = new Vector2(1.6f, 0.45f), duration = 0.32f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.94f, 0.32f, 0.04f, fray: 0.3f, innerJag: 0.4f, breakup: 0.3f, seed: 51),
                Spark(L("Cian", Cyan, 0.92f, 0.24f, 0.02f, fray: 0.25f, innerJag: 0.3f, breakup: 0.25f, seed: 52), 10),
                Spark(L("Núcleo", White, 0.90f, 0.12f, 0f, fray: 0.1f, innerJag: 0.15f, seed: 53), 18),
            },
        };

        private static Preset Jump4() => new Preset   // fina y rápida
        {
            worldRadius = 1.8f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 200f,
            shapeStretch = new Vector2(1.7f, 0.5f), duration = 0.22f, headPortion = 0.5f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.97f, 0.06f, 0.06f, fray: 0.2f, tailTaper: 2.6f, innerJag: 0.2f, seed: 61),
                L("Cian", Cyan, 0.95f, 0.12f, 0.02f, fray: 0.15f, tailTaper: 2.4f, innerJag: 0.15f, seed: 62),
                L("Núcleo", White, 0.94f, 0.05f, 0f, fray: 0.05f, tailTaper: 2.4f, innerJag: 0.05f, seed: 63),
            },
        };

        // Doble salto: anillo bajo los pies que se cierra por arriba (simétrico casi completo).
        private static Preset Double1() => new Preset   // anillo fino
        {
            worldRadius = 1.6f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 350f,
            shapeStretch = new Vector2(1.3f, 0.4f), duration = 0.32f, headPortion = 0.5f, tailStart = 0.35f,
            layers = new[]
            {
                L("Cian", Cyan, 0.97f, 0.12f, 0.03f, fray: 0.1f, innerJag: 0.1f, breakup: 0.1f, seed: 71),
                L("Núcleo", White, 0.96f, 0.06f, 0f, fray: 0.05f, innerJag: 0.05f, breakup: 0.05f, seed: 72),
            },
        };

        private static Preset Double2() => new Preset   // anillo grueso con estela morada
        {
            worldRadius = 1.7f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 350f,
            shapeStretch = new Vector2(1.3f, 0.45f), duration = 0.38f, tailStart = 0.3f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.10f, 0.08f, fray: 0.35f, innerJag: 0.4f, breakup: 0.3f, seed: 81),
                L("Azul", DeepBlue, 0.96f, 0.30f, 0.05f, fray: 0.3f, innerJag: 0.4f, breakup: 0.35f, seed: 82),
                L("Cian", Cyan, 0.94f, 0.22f, 0.025f, fray: 0.25f, innerJag: 0.3f, breakup: 0.25f, seed: 83),
                L("Núcleo", White, 0.92f, 0.11f, 0f, fray: 0.1f, innerJag: 0.15f, breakup: 0.1f, seed: 84),
            },
        };

        private static Preset Double3() => new Preset   // dos anillos (uno dentro de otro)
        {
            worldRadius = 1.8f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 350f,
            shapeStretch = new Vector2(1.25f, 0.42f), duration = 0.36f, tailStart = 0.3f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.98f, 0.14f, 0.06f, fray: 0.2f, innerJag: 0.2f, breakup: 0.2f, seed: 91),
                L("Cian", Cyan, 0.97f, 0.09f, 0.04f, fray: 0.15f, innerJag: 0.15f, seed: 92),
                L("Cian claro", LightCyan, 0.68f, 0.12f, 0.02f, fray: 0.2f, innerJag: 0.2f, breakup: 0.2f, seed: 93),
                L("Núcleo", White, 0.66f, 0.06f, 0f, fray: 0.1f, innerJag: 0.1f, seed: 94),
            },
        };

        private static Preset Double4() => new Preset   // anillo alto con chispas
        {
            worldRadius = 1.6f, symmetric = true, clockwise = false, startAngle = 270f, arcDegrees = 350f,
            shapeStretch = new Vector2(1.2f, 0.7f), duration = 0.36f, tailStart = 0.3f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.96f, 0.24f, 0.05f, fray: 0.3f, innerJag: 0.35f, breakup: 0.3f, seed: 101),
                Spark(L("Cian", Cyan, 0.94f, 0.18f, 0.025f, fray: 0.25f, innerJag: 0.3f, breakup: 0.25f, seed: 102), 12),
                Spark(L("Núcleo", White, 0.92f, 0.09f, 0f, fray: 0.1f, innerJag: 0.1f, seed: 103), 18),
            },
        };

        // Dash: estela detrás del jugador (el centro del arco mira hacia atrás, 180°).
        private static Preset Dash1() => new Preset   // lente simétrica limpia
        {
            worldRadius = 2f, symmetric = true, clockwise = false, startAngle = 180f, arcDegrees = 130f,
            shapeStretch = new Vector2(2.4f, 0.45f), duration = 0.3f, headPortion = 0.4f,
            layers = new[]
            {
                L("Azul", DeepBlue, 0.96f, 0.30f, 0.04f, fray: 0.15f, innerJag: 0.15f, breakup: 0.1f, seed: 111),
                L("Cian", Cyan, 0.94f, 0.22f, 0.02f, fray: 0.1f, innerJag: 0.1f, breakup: 0.1f, seed: 112),
                L("Núcleo", White, 0.92f, 0.12f, 0f, fray: 0.05f, innerJag: 0.05f, seed: 113),
            },
        };

        private static Preset Dash2() => new Preset   // corte: barre de arriba-delante a abajo-detrás
        {
            worldRadius = 2f, clockwise = false, startAngle = 110f, arcDegrees = 150f,
            shapeStretch = new Vector2(2.2f, 0.5f), duration = 0.3f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.09f, 0.08f, fray: 0.4f, tailTaper: 2.2f, innerJag: 0.4f, seed: 121),
                L("Azul", DeepBlue, 0.95f, 0.36f, 0.05f, fray: 0.4f, innerJag: 0.45f, breakup: 0.35f, seed: 122),
                L("Cian", Cyan, 0.93f, 0.27f, 0.025f, fray: 0.3f, innerJag: 0.35f, breakup: 0.25f, seed: 123),
                L("Núcleo", White, 0.91f, 0.14f, 0f, fray: 0.15f, innerJag: 0.2f, breakup: 0.1f, seed: 124),
            },
        };

        private static Preset Dash3() => new Preset   // viento: líneas largas y finas deshilachadas
        {
            worldRadius = 2.2f, symmetric = true, clockwise = false, startAngle = 180f, arcDegrees = 110f,
            shapeStretch = new Vector2(2.8f, 0.32f), duration = 0.28f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.08f, 0.07f, fray: 0.5f, bands: 9f, tailTaper: 2.6f, innerJag: 0.5f, breakup: 0.4f, seed: 131),
                L("Cian", Cyan, 0.90f, 0.16f, 0.04f, fray: 0.45f, bands: 8f, tailTaper: 2.4f, innerJag: 0.5f, breakup: 0.4f, seed: 132),
                L("Cian claro", LightCyan, 0.74f, 0.10f, 0.06f, fray: 0.45f, bands: 7f, tailTaper: 2.4f, innerJag: 0.4f, breakup: 0.4f, seed: 133),
                L("Núcleo", White, 0.88f, 0.06f, 0f, fray: 0.2f, tailTaper: 2.4f, innerJag: 0.2f, seed: 134),
            },
        };

        private static Preset Dash4() => new Preset   // lente con estela morada y chispas
        {
            worldRadius = 2f, symmetric = true, clockwise = false, startAngle = 180f, arcDegrees = 140f,
            shapeStretch = new Vector2(2.3f, 0.55f), duration = 0.34f,
            layers = new[]
            {
                L("Estela morada", Purple, 0.99f, 0.10f, 0.08f, fray: 0.4f, innerJag: 0.4f, breakup: 0.3f, seed: 141),
                L("Azul", DeepBlue, 0.95f, 0.34f, 0.05f, fray: 0.35f, innerJag: 0.4f, breakup: 0.35f, seed: 142),
                Spark(L("Cian", Cyan, 0.93f, 0.25f, 0.025f, fray: 0.3f, innerJag: 0.3f, breakup: 0.25f, seed: 143), 10),
                Spark(L("Núcleo", White, 0.91f, 0.13f, 0f, fray: 0.15f, innerJag: 0.15f, seed: 144), 16),
            },
        };

        private static MeleeSlashFx.Layer Spark(MeleeSlashFx.Layer l, int count)
        {
            l.shardStyle = MeleeSlashFx.ShardStyle.Chispas;
            l.shards = count;
            l.shardDrift = 0.4f;
            return l;
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
