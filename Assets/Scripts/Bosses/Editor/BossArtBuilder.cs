using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Construye el <b>arte de los ataques</b> de un jefe (bloque <c>artAssets</c> del BossConfig):
    /// cada entrada es una carpeta de frames y sale un prefab listo para su hueco.
    ///
    /// <list type="bullet">
    /// <item><c>projectile</c> — Projectile + cuerpo + collider + estados (Move / Impact). Para
    /// <c>ProjectileSpec.prefab</c>.</item>
    /// <item><c>fx</c> — efecto de un solo uso (VfxOneShot + flipbook). Para los huecos de impacto.</item>
    /// <item><c>warning</c> — aviso (FxTelegraph que se ajusta al ancho). Para los avisos del jefe.</item>
    /// <item><c>prop</c> — sólo dibujo (lo que el jefe sujeta).</item>
    /// </list>
    ///
    /// Los prefabs van en <c>Assets/Prefabs/Fx/Bosses/&lt;Jefe&gt;/</c> (una carpeta por jefe, la
    /// convención del proyecto) y se <b>regeneran</b> en cada import conservando su GUID, así que
    /// las referencias no se rompen. Para retocarlos a mano, duplica el prefab y apunta a la copia.
    /// </summary>
    public static class BossArtBuilder
    {
        public const string KindProjectile = "projectile";
        public const string KindFx = "fx";
        public const string KindWarning = "warning";
        public const string KindProp = "prop";

        /// <summary>
        /// Construye todas las entradas. Devuelve id → ruta del prefab, que es lo que sustituye a
        /// cada <c>{"art": "id"}</c> del resto del JSON.
        /// </summary>
        public static Dictionary<string, string> Build(string bossSlug, JObject artAssets, StringBuilder log)
        {
            var result = new Dictionary<string, string>();
            if (artAssets == null) return result;

            string folder = $"Assets/Prefabs/Fx/Bosses/{bossSlug}";
            SheetSlicer.EnsureFolder(folder);

            foreach (var prop in artAssets.Properties())
            {
                if (!(prop.Value is JObject entry))
                {
                    log.AppendLine($"  AVISO artAssets.{prop.Name}: no es un objeto; se omite.");
                    continue;
                }

                string path = $"{folder}/Fx_{bossSlug}_{prop.Name}.prefab";
                try
                {
                    if (BuildOne(prop.Name, entry, path, log)) result[prop.Name] = path;
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ERROR artAssets.{prop.Name}: {e.Message}");
                }
            }

            return result;
        }

        private static bool BuildOne(string id, JObject entry, string prefabPath, StringBuilder log)
        {
            string kind = entry.Value<string>("kind") ?? KindFx;
            string baseFolder = (entry.Value<string>("folder") ?? string.Empty).TrimEnd('/');
            float worldWidth = entry.Value<float?>("worldWidth") ?? 1.5f;
            int sortingOrder = entry.Value<int?>("sortingOrder") ?? (kind == KindWarning ? 4 : 6);
            string sortingLayer = entry.Value<string>("sortingLayer");

            var states = ReadStates(id, baseFolder, entry["states"] as JObject, log);
            if (states.Count == 0)
            {
                log.AppendLine($"  AVISO artAssets.{id}: ningún estado tiene frames en '{baseFolder}'; se omite.");
                return false;
            }

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var primary = PickPrimary(states, kind);
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = primary.frames[0];
                renderer.sortingOrder = sortingOrder;
                if (!string.IsNullOrWhiteSpace(sortingLayer)) renderer.sortingLayerName = sortingLayer;

                bool single = kind != KindProjectile || states.Count == 1;
                if (single) AddFlipbook(root, primary, oneShot: kind == KindFx && !primary.loop);
                else AddStateMachine(root, states, primary.name);

                // Tamaño: el ancho del dibujo en el mundo. Los avisos no: los ajusta FxTelegraph.
                // Se mide con bounds (lo mismo que usa el collider): tamaño y collider salen de la
                // misma medida y no pueden descuadrarse.
                float spriteWidth = primary.frames[0].bounds.size.x;
                if (kind != KindWarning && spriteWidth > 0.0001f)
                    root.transform.localScale = Vector3.one * (worldWidth / spriteWidth);

                // Proyectiles y objetos: su tamaño lo manda el ataque ("Size" / "heldSize").
                if (kind == KindProjectile || kind == KindProp) root.AddComponent<RedMagic.Fx.FxArtSize>();

                switch (kind)
                {
                    case KindProjectile: DressProjectile(root, entry, primary); break;
                    case KindWarning: DressWarning(root, entry); break;
                    case KindProp: break;
                    default: DressFx(root, primary); break;
                }

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.AppendLine($"  arte '{id}' ({kind}) {(existed ? "regenerado" : "creado")}: {prefabPath} " +
                               $"[{string.Join("/", states.Select(s => $"{s.name}:{s.frames.Length}"))}]");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Estados: <c>{"Move": {"folder": "Move", "fps": 10, "loop": true}, …}</c>. Sin bloque, cada
        /// subcarpeta de <paramref name="baseFolder"/> es un estado con su nombre.
        /// </summary>
        private static List<SpriteStateMachine.State> ReadStates(string id, string baseFolder, JObject statesJson,
                                                                 StringBuilder log)
        {
            var list = new List<SpriteStateMachine.State>();
            var entries = new List<(string name, string folder, float fps, bool loop)>();

            if (statesJson != null)
            {
                foreach (var s in statesJson.Properties())
                {
                    var o = s.Value as JObject ?? new JObject();
                    entries.Add((s.Name, o.Value<string>("folder") ?? s.Name, o.Value<float?>("fps") ?? 10f,
                                 o.Value<bool?>("loop") ?? true));
                }
            }
            else if (AssetDatabase.IsValidFolder(baseFolder))
            {
                foreach (var sub in AssetDatabase.GetSubFolders(baseFolder))
                {
                    string name = System.IO.Path.GetFileName(sub);
                    entries.Add((name, name, 10f, true));
                }
            }

            foreach (var e in entries)
            {
                var sprites = FrameFolderImporter.Import($"{baseFolder}/{e.folder}", AnchorMode.Center, 100f, 4, log);
                if (sprites.Count == 0) continue;

                list.Add(new SpriteStateMachine.State
                {
                    name = e.name,
                    frames = sprites.ToArray(),
                    fps = Mathf.Max(0.1f, e.fps),
                    loop = e.loop,
                });
            }

            return list;
        }

        private static SpriteStateMachine.State PickPrimary(List<SpriteStateMachine.State> states, string kind)
        {
            string preferred = kind == KindProjectile ? "Move" : null;
            return (preferred != null ? states.FirstOrDefault(s => s.name == preferred) : null) ?? states[0];
        }

        // ============================================================ por tipo

        private static void DressProjectile(GameObject root, JObject entry, SpriteStateMachine.State primary)
        {
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;   // la parábola la pone Projectile (arcGravity), no la física
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Radio sobre el dibujo recortado (en unidades locales: la escala del root lo lleva al
            // mundo). Algo menor que el dibujo: un roce con el borde no debería contar.
            var bounds = primary.frames[0].bounds.size;
            float factor = entry.Value<float?>("colliderScale") ?? 0.8f;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.01f, Mathf.Min(bounds.x, bounds.y) * 0.5f * factor);

            var projectile = root.AddComponent<Projectile>();
            var so = new SerializedObject(projectile);
            so.FindProperty("faceTravelDirection").boolValue = entry.Value<bool?>("faceTravelDirection") ?? false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DressWarning(GameObject root, JObject entry)
        {
            var telegraph = root.AddComponent<FxTelegraph>();
            var so = new SerializedObject(telegraph);
            so.FindProperty("fit").enumValueIndex = (int)FxTelegraph.FitMode.FitWidth;
            so.FindProperty("applyColor").boolValue = entry.Value<bool?>("tint") ?? false;
            so.FindProperty("fadeOut").boolValue = entry.Value<bool?>("fadeOut") ?? true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DressFx(GameObject root, SpriteStateMachine.State primary)
        {
            var oneShot = root.AddComponent<VfxOneShot>();
            var so = new SerializedObject(oneShot);
            // VfxOneShot sólo sabe medir Animators: con flipbook, la duración va a mano.
            so.FindProperty("lifetime").floatValue = Mathf.Max(0.05f, primary.Duration);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============================================================ animación (sin Animator: se poolea)

        private static void AddFlipbook(GameObject root, SpriteStateMachine.State state, bool oneShot)
        {
            var flipbook = root.AddComponent<SpriteFlipbook>();
            var so = new SerializedObject(flipbook);
            WriteSprites(so.FindProperty("frames"), state.frames);
            so.FindProperty("framesPerSecond").floatValue = state.fps;
            so.FindProperty("pingPong").boolValue = false;
            so.FindProperty("randomStart").boolValue = false;
            so.FindProperty("oneShot").boolValue = oneShot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddStateMachine(GameObject root, List<SpriteStateMachine.State> states, string defaultState)
        {
            var machine = root.AddComponent<SpriteStateMachine>();
            var so = new SerializedObject(machine);
            var list = so.FindProperty("states");
            list.arraySize = states.Count;
            for (int i = 0; i < states.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = states[i].name;
                element.FindPropertyRelative("fps").floatValue = states[i].fps;
                // El impacto no repite: Projectile lo reproduce una vez antes de volver al pool.
                element.FindPropertyRelative("loop").boolValue = states[i].name != "Impact" && states[i].loop;
                WriteSprites(element.FindPropertyRelative("frames"), states[i].frames);
            }
            so.FindProperty("defaultState").stringValue = defaultState;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteSprites(SerializedProperty prop, Sprite[] sprites)
        {
            prop.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        // ============================================================ referencias en el JSON

        /// <summary>
        /// Cambia, en todo el árbol, cada <c>{"art": "id"}</c> por la ruta de su prefab, para que el
        /// resto del importador (que ya sabe leer rutas de assets) no tenga que saber nada de esto.
        /// Un id que no existe se queda sin resolver y se avisa.
        /// </summary>
        public static void ResolveArtRefs(JToken token, Dictionary<string, string> artMap, List<string> warnings,
                                          string context = "")
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties().ToList())
                {
                    if (prop.Value is JObject child && child.Count == 1 && child["art"] != null &&
                        child["art"].Type == JTokenType.String)
                    {
                        string id = child.Value<string>("art");
                        if (artMap != null && artMap.TryGetValue(id, out var path)) prop.Value = new JValue(path);
                        else
                        {
                            warnings.Add($"{context}.{prop.Name}: arte '{id}' no está en artAssets (o no se pudo construir); el campo queda vacío (placeholder del ataque).");
                            prop.Value = JValue.CreateNull();
                        }
                        continue;
                    }

                    ResolveArtRefs(prop.Value, artMap, warnings, $"{context}.{prop.Name}");
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] is JObject element && element.Count == 1 && element["art"] != null &&
                        element["art"].Type == JTokenType.String)
                    {
                        string id = element.Value<string>("art");
                        if (artMap != null && artMap.TryGetValue(id, out var path)) array[i] = new JValue(path);
                        else
                        {
                            warnings.Add($"{context}[{i}]: arte '{id}' no está en artAssets; se deja vacío.");
                            array[i] = JValue.CreateNull();
                        }
                        continue;
                    }

                    ResolveArtRefs(array[i], artMap, warnings, $"{context}[{i}]");
                }
            }
        }
    }
}
