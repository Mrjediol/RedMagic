using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Gameplay.Movement;
using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Construye el <b>cuerpo</b> de un jefe (bloque <c>body</c> del BossConfig) a partir de una
    /// carpeta con una subcarpeta de frames por animación: clips, AnimatorController y el prefab
    /// jugable (BossController + BossAnimator + Health… y, si se pide, un <see cref="Mover"/>).
    ///
    /// <b>Primera vez</b>: crea el prefab entero con los valores del JSON.
    /// <b>Siguientes</b>: sólo vuelve a enganchar lo que viene del arte y del import (controller,
    /// sprite, definición, avisos vacíos, Mover si falta). Los valores que ajustes a mano en el
    /// prefab (vida, escala, arena, el tipo de movimiento…) no se pisan.
    /// </summary>
    public static class BossBodyBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Enemies";

        public static string PrefabPathFor(string slug) => $"{PrefabFolder}/Boss_{slug}.prefab";

        public static GameObject Build(string slug, JObject body, BossDefinition definition,
                                       Dictionary<string, string> artMap, ConfigImportReport report,
                                       StringBuilder log)
        {
            string artFolder = (body.Value<string>("folder") ?? string.Empty).TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(artFolder))
                throw new ConfigImportException($"body.folder: no existe la carpeta '{artFolder}'.");

            string outFolder = $"Assets/Art/Bosses/{slug}";
            SheetSlicer.EnsureFolder($"{outFolder}/Anim");

            // --- 1) clips
            var clips = new Dictionary<string, AnimationClip>();
            var framesByState = new Dictionary<string, Sprite[]>();

            var animations = body["animations"] as JObject;
            if (animations == null || animations.Count == 0)
                throw new ConfigImportException("body.animations: vacío o falta (al menos 'Idle').");

            // Una sola caja para TODAS las animaciones del cuerpo: mismo rect y mismo pivote en todos
            // los clips (si no, un puño estirado movería el centro y el jefe saltaría al cambiar).
            var shared = FrameFolderImporter.MeasureContent(animations.Properties()
                .Select(p => $"{artFolder}/{(p.Value as JObject)?.Value<string>("folder") ?? p.Name}"));

            foreach (var prop in animations.Properties())
            {
                string state = prop.Name;
                if (state.Contains("_"))
                    throw new ConfigImportException($"body.animations.{state}: el nombre de estado no puede llevar '_' " +
                                                    "(BossAnimator lee el estado del nombre del clip tras el último '_').");

                var a = prop.Value as JObject ?? new JObject();
                string sub = a.Value<string>("folder") ?? state;
                var sprites = FrameFolderImporter.Import($"{artFolder}/{sub}", AnchorMode.BottomCenter, 100f, 4, log, shared);
                if (sprites.Count == 0)
                {
                    report.Warnings.Add($"body.animations.{state}: '{artFolder}/{sub}' no tiene frames; se omite.");
                    continue;
                }

                framesByState[state] = sprites.ToArray();
                clips[state] = BuildClip($"{outFolder}/Anim/{slug}_{state}.anim", sprites.ToArray(),
                                         a.Value<float?>("fps") ?? 10f,
                                         a.Value<bool?>("loop") ?? (state == "Idle" || state == "Walk"),
                                         a.Value<int?>("releaseFrame") ?? -1);
                log.AppendLine($"  clip {state}: {sprites.Count} frames");
            }

            // --- 1b) derivados: un trozo de otra animación en bucle (la pose de viaje de una embestida).
            if (body["derived"] is JObject derived)
            {
                foreach (var prop in derived.Properties())
                {
                    var d = prop.Value as JObject ?? new JObject();
                    string from = d.Value<string>("from");
                    if (string.IsNullOrEmpty(from) || !framesByState.TryGetValue(from, out var source))
                    {
                        report.Warnings.Add($"body.derived.{prop.Name}: 'from' ('{from}') no es una animación importada.");
                        continue;
                    }

                    int first = Mathf.Clamp(d.Value<int?>("first") ?? 0, 0, source.Length - 1);
                    int count = Mathf.Clamp(d.Value<int?>("count") ?? source.Length - first, 1, source.Length - first);
                    var slice = source.Skip(first).Take(count).ToArray();
                    clips[prop.Name] = BuildClip($"{outFolder}/Anim/{slug}_{prop.Name}.anim", slice,
                                                 d.Value<float?>("fps") ?? 10f, d.Value<bool?>("loop") ?? true, -1);
                    log.AppendLine($"  clip {prop.Name} (de {from}[{first}..{first + count - 1}])");
                }
            }

            if (!framesByState.ContainsKey("Idle"))
                throw new ConfigImportException("body.animations: falta 'Idle' (o su carpeta no tiene frames).");

            // --- 2) controller (el mismo cableado que los enemigos: Idle↔Walk por 'Moving', Death por 'Dead')
            var recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            AnimatorController controller;
            try
            {
                recipe.characterName = slug;
                recipe.outputFolder = outFolder;
                controller = AnimClipBuilder.BuildController(recipe, clips, log);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
            }

            if (controller == null) throw new ConfigImportException("body: no se pudo crear el AnimatorController.");
            report.Add(false, AssetDatabase.GetAssetPath(controller));

            // --- 3) prefab
            string prefabPath = PrefabPathFor(slug);
            var idle = framesByState["Idle"][0];
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;

            GameObject root = null;
            try
            {
                root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : CreateRoot(slug, body, idle, report, log);
                Relink(root, body, controller, idle, definition, artMap, report);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (root != null)
                {
                    if (exists) PrefabUtility.UnloadPrefabContents(root);
                    else Object.DestroyImmediate(root);
                }
            }

            report.Add(!exists, prefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // ============================================================ clips

        private static AnimationClip BuildClip(string path, Sprite[] sprites, float fps, bool loop, int releaseFrame)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            fps = Mathf.Max(0.1f, fps);
            clip.frameRate = fps;

            foreach (var existing in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                if (existing.propertyName == "m_Sprite")
                    AnimationUtility.SetObjectReferenceCurve(clip, existing, null);

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = AnimClipBuilder.RendererPath,
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = RedMagic.Pipeline.AnimStates.Loops(clip.name, loop);   // muerte nunca repite
            settings.stopTime = sprites.Length / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Gesto: OnAttackRelease en el frame de suelta y OnAttackFinished al final (BossAnimator).
            if (releaseFrame >= 0)
            {
                int frame = Mathf.Clamp(releaseFrame, 0, sprites.Length - 1);
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = frame / fps, functionName = "OnAttackRelease" },
                    new AnimationEvent { time = Mathf.Max(0f, (sprites.Length - 0.01f) / fps), functionName = "OnAttackFinished" },
                });
            }
            else
            {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ============================================================ prefab

        /// <summary>El prefab entero, sólo la primera vez. Mismo montaje que el Ent Cristalino.</summary>
        private static GameObject CreateRoot(string slug, JObject body, Sprite idle, ConfigImportReport report,
                                             StringBuilder log)
        {
            var root = new GameObject($"Boss_{slug}");
            BossAuthoring.TrySetTag(root, "Enemy");

            float height = body.Value<float?>("height") ?? 6f;
            float hover = body.Value<float?>("hoverHeight") ?? 0f;
            float spriteHeight = Mathf.Max(0.01f, idle.bounds.size.y);
            float scale = height / spriteHeight;

            // ---- visual: el hijo "Sprite", la ruta que animan los clips.
            var visual = new GameObject(AnimClipBuilder.RendererPath);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, hover, 0f);
            visual.transform.localScale = Vector3.one * scale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idle;
            renderer.sortingLayerName = body.Value<string>("sortingLayer") ?? "Characters";
            renderer.sortingOrder = 5;

            var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            rb.freezeRotation = true;

            // Caja del cuerpo: más estrecha que el dibujo (puños y bordes no cuentan como cuerpo).
            float widthFactor = body.Value<float?>("colliderWidth") ?? 0.5f;
            float heightFactor = body.Value<float?>("colliderHeight") ?? 0.8f;
            var size = idle.bounds.size * scale;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(Mathf.Max(0.5f, size.x * widthFactor), Mathf.Max(0.5f, size.y * heightFactor));
            collider.offset = new Vector2(0f, hover + collider.size.y * 0.5f);

            float maxHealth = body.Value<float?>("health") ?? 2500f;
            new BossAuthoring.Fields(root.AddComponent<Health>())
                .Set("maxHealth", maxHealth)
                .Set("currentHealth", maxHealth)
                .Set("invulnerabilityDuration", 0f)
                .Apply();

            new BossAuthoring.Fields(root.AddComponent<Knockback>()).Set("immune", true).Set("resistance", 1f).Apply();
            root.AddComponent<HitFlash>();

            new BossAuthoring.Fields(root.AddComponent<Corpse>())
                .Set("linger", 1.4f).Set("fadeDuration", 1.2f).Set("tintOnDeath", false)
                .Apply();

            new BossAuthoring.Fields(root.AddComponent<CurrencyDropper>()).Set("tier", (int)EnemyTier.Boss).Apply();

            root.AddComponent<BossAnimator>();

            new BossAuthoring.Fields(root.AddComponent<BossController>())
                .Set("arenaHalfWidth", body.Value<float?>("arenaHalfWidth") ?? 15f)
                .Set("arenaHeight", body.Value<float?>("arenaHeight") ?? 12f)
                .Set("activationRadius", body.Value<float?>("activationRadius") ?? 18f)
                .Set("faceTarget", true)
                .Set("introSeconds", 2.2f)
                .Set("introShake", 0.6f)
                .Set("contactDamage", body.Value<float?>("contactDamage") ?? 12f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.4f)
                .Set("swayDegrees", 0f)   // animado: el balanceo ya está en el dibujo
                .Apply();

            if (body["movement"] is JObject movement) AddMover(root, movement, report);

            log.AppendLine($"  prefab nuevo Boss_{slug}: alto {height:0.#} u, vida {maxHealth:0}");
            return root;
        }

        /// <summary>Lo que se re-engancha en cada import (nuevo o existente).</summary>
        private static void Relink(GameObject root, JObject body, AnimatorController controller, Sprite idle,
                                   BossDefinition definition, Dictionary<string, string> artMap,
                                   ConfigImportReport report)
        {
            var animator = root.GetComponent<Animator>();
            if (animator != null) animator.runtimeAnimatorController = controller;

            var visual = root.transform.Find(AnimClipBuilder.RendererPath);
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : root.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) renderer.sprite = idle;

            var boss = root.GetComponent<BossController>();
            if (boss == null) return;

            var so = new SerializedObject(boss);
            so.FindProperty("definition").objectReferenceValue = definition;

            // Avisos: sólo si el hueco está vacío (uno puesto a mano manda).
            AssignIfEmpty(so, "warnCirclePrefab", body.Value<string>("warnCircle"), artMap, report);
            AssignIfEmpty(so, "warnArrowPrefab", body.Value<string>("warnArrow"), artMap, report);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (body["movement"] is JObject movement && root.GetComponent<Mover>() == null)
                AddMover(root, movement, report);
        }

        private static void AssignIfEmpty(SerializedObject so, string field, string artId,
                                          Dictionary<string, string> artMap, ConfigImportReport report)
        {
            if (string.IsNullOrEmpty(artId)) return;
            var prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue != null) return;

            if (artMap == null || !artMap.TryGetValue(artId, out var path))
            {
                report.Warnings.Add($"body.{field}: arte '{artId}' no está en artAssets; queda el placeholder.");
                return;
            }

            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// Añade un <see cref="Mover"/> con el tipo de movimiento pedido (<c>type</c> = nombre de la
        /// clase C#, conjunto abierto como los ataques) y sus valores (<c>params</c>).
        /// </summary>
        private static void AddMover(GameObject root, JObject movement, ConfigImportReport report)
        {
            string typeName = movement.Value<string>("type");
            if (string.IsNullOrWhiteSpace(typeName)) return;

            var type = TypeCache.GetTypesDerivedFrom<MovementBehaviour>()
                .FirstOrDefault(t => t.Name == typeName && !t.IsAbstract);
            if (type == null)
            {
                report?.Warnings.Add($"body.movement.type: '{typeName}' no es ningún MovementBehaviour; el jefe no se moverá.");
                return;
            }

            var mover = root.AddComponent<Mover>();
            var so = new SerializedObject(mover);
            var behaviour = so.FindProperty("behaviour");
            behaviour.managedReferenceValue = Activator.CreateInstance(type);
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            behaviour = so.FindProperty("behaviour");

            if (movement["params"] is JObject p)
            {
                var unresolved = report != null ? report.UnresolvedParams : new List<string>();
                foreach (var param in p.Properties())
                {
                    var child = behaviour.FindPropertyRelative(param.Name);
                    if (child == null)
                    {
                        unresolved.Add($"body.movement.params.{param.Name}: no existe en {typeName}.");
                        continue;
                    }
                    SerializedFieldWriter.TryWriteValue(child, param.Value, $"body.movement.params.{param.Name}", unresolved);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
