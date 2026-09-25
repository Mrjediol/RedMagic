using System;
using System.Linq;
using System.Text;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Items.EditorTools
{
    /// <summary>
    /// Set de hielo: los 6 items (Botas, Bastón, Capa, Yelmo, Anillo, Grimorio) con su mecánica
    /// real, el efecto de explosión de hielo que comparten el Yelmo y Hielo 6, y el texto de los
    /// umbrales de sinergia implementados. <b>Tools ▸ RedMagic ▸ Items ▸ Set de hielo · Generar</b>.
    ///
    /// Qué hace, en orden (todo re-ejecutable):
    /// <list type="number">
    /// <item>Corta <c>Assets/Art/VX/IceExplotion.png</c> (una fila, 5 dibujos, alfa real) por el
    /// pipeline de láminas (<see cref="SpritePipeline.RunSheet"/>, runtime Flipbook) y monta
    /// <c>Assets/Prefabs/Fx/Items/Fx_IceExplosion.prefab</c>: <see cref="VfxOneShot"/> +
    /// <see cref="SpriteFlipbook"/>, pooled. Si el prefab ya existe sólo le refresca los frames.</item>
    /// <item>Los 5 items que ya existían (placeholders de prueba) reciben su ficha, tags, rareza y
    /// efectos reales <b>la primera vez</b> — cuando todavía no llevan el efecto del set. Después, el
    /// pack no toca lo afinado a mano en el Inspector. <b>El icono nunca se toca.</b></item>
    /// <item>El Grimorio se crea si falta, con el icono de <see cref="GrimoireIconPath"/>.</item>
    /// <item>Rellena referencias vacías al prefab de explosión (Yelmo y <c>SynergyConfig</c>) y
    /// reescribe el texto de los umbrales implementados con los números actuales.</item>
    /// </list>
    /// Un item nuevo del set: una llamada más a <see cref="Item"/> con su efecto.
    /// </summary>
    public static class IceSetPack
    {
        private const string ItemsFolder = "Assets/Resources/Items";

        private const string ExplosionSheet = "Assets/Art/VX/IceExplotion.png";
        private const string ExplosionArtFolder = "Assets/Art/VX/IceExplosion";
        private const string ExplosionRecipePath = ExplosionArtFolder + "/IceExplosion.sheet.asset";
        private const string ExplosionCharacter = "IceExplosion";
        private const string ExplosionState = "Explode";
        private const float ExplosionFps = 12f;

        private const string FxFolder = "Assets/Prefabs/Fx/Items";
        private const string ExplosionPrefabPath = FxFolder + "/Fx_IceExplosion.prefab";

        // ─── ICONO DEL GRIMORIO ───────────────────────────────────────────────────────────────────
        // El sprite del Grimorio se asigna AQUÍ, al crear Item_Grimorio (campo "icon" del asset).
        // Es Assets/Art/Icons/Grimorio.png tal cual: ya trae alfa real, importado como Sprite
        // (Single). Para cambiarlo después basta con el Inspector del item o con
        // Tools ▸ RedMagic ▸ Items ▸ Catálogo de items (iconos); el pack no lo vuelve a pisar.
        private const string GrimoireIconPath = "Assets/Art/Icons/Grimorio.png";

        private static readonly Color IceAccent = new(0.55f, 0.85f, 0.95f);

        [MenuItem("Tools/RedMagic/Items/Set de hielo · Generar")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            var log = new StringBuilder("[IceSetPack]\n");

            var explosion = ExplosionPrefab(log);

            Item(log, "Item_BotasRunicas", "Botas Rúnicas", ItemRarity.Epic, BuildTag.Haste, null,
                 "Botas de escarcha que apenas rozan el suelo. Con el frío a la vista, el tiempo corre a tu favor.",
                 new SlowedEnemyCooldownEffect { cooldownRate = 1.5f });

            Item(log, "Item_BastonHelado", "Bastón Helado", ItemRarity.Legendary, BuildTag.Haste, null,
                 "El cristal bebe de cada caída. La siguiente descarga no espera y golpea el triple.",
                 new KillEmpowersNextCastEffect { damageMultiplier = 3f, resetCooldown = true });

            Item(log, "Item_CapaEscarcha", "Capa de Escarcha", ItemRarity.Epic, BuildTag.Reset, null,
                 "Lo que mata no se detiene: el disparo se recompone en el cuerpo helado y sigue.",
                 new PierceOnKillEffect { respawnDelay = 0.5f });

            Item(log, "Item_YelmoRunico", "Yelmo Rúnico", ItemRarity.Legendary, BuildTag.Lifesteal, null,
                 "Las runas guardan cada muerte. A la quinta, el hielo estalla a tu alrededor.",
                 new KillCounterExplosionEffect
                 {
                     killsRequired = 5, radius = 4.5f, damage = 40f, slowsTargets = true,
                     explosionPrefab = explosion,
                 });

            Item(log, "Item_AnilloGlacial", "Anillo Glacial", ItemRarity.Blue, BuildTag.Lifesteal, null,
                 "La gema muerde la carne congelada: cuanto más grande, más sangra.",
                 new SlowedBonusDamageEffect { percentOfMaxHealth = 0.08f });

            Item(log, "Item_Grimorio", "Grimorio Glacial", ItemRarity.Blue, BuildTag.Reset,
                 AssetDatabase.LoadAssetAtPath<Sprite>(GrimoireIconPath),
                 "Sus páginas escarchan la armadura de quien se ralentiza.",
                 new SlowArmorShredEffect { armorReduction = 0.3f });

            FillExplosionRefs(explosion, log);
            SynergyTexts(explosion, log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ─── Explosión ────────────────────────────────────────────────────────────────────────────

        private static GameObject ExplosionPrefab(StringBuilder log)
        {
            SheetSlicer.EnsureFolder(ExplosionArtFolder);
            SheetSlicer.EnsureFolder(FxFolder);

            log.Append(SpritePipeline.RunSheet(ExplosionRecipe()));

            var frames = AssetDatabase.LoadAllAssetsAtPath($"{ExplosionArtFolder}/{ExplosionCharacter}_{ExplosionState}.png")
                .OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0)
                .ToArray();

            if (frames.Length == 0)
            {
                log.AppendLine("  ERROR: no salieron frames de la explosión; el Yelmo y Hielo 6 usarán el " +
                               "destello de código.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath);
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(ExplosionPrefabPath) : BuildExplosion(frames[0]);

            var flipbook = root.GetComponentInChildren<SpriteFlipbook>(true);
            var so = new SerializedObject(flipbook);
            var list = so.FindProperty("frames");
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            if (existing == null)
            {
                so.FindProperty("framesPerSecond").floatValue = ExplosionFps;
                so.FindProperty("pingPong").boolValue = false;
                so.FindProperty("randomStart").boolValue = false;
                so.FindProperty("oneShot").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // VfxOneShot sólo sabe medir clips de Animator: con flipbook la duración va a mano.
            float fps = so.FindProperty("framesPerSecond").floatValue;
            var oneShot = new SerializedObject(root.GetComponent<VfxOneShot>());
            oneShot.FindProperty("lifetime").floatValue = frames.Length / Mathf.Max(0.1f, fps);
            oneShot.FindProperty("extraTime").floatValue = 0.05f;
            oneShot.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);

            log.AppendLine($"  {(existing != null ? "Actualizado" : "Creado")} {ExplosionPrefabPath} ({frames.Length} frames)");
            return prefab;
        }

        private static GameObject BuildExplosion(Sprite first)
        {
            var root = new GameObject("Fx_IceExplosion");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = first;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 20;   // por encima de enemigos y proyectiles

            visual.AddComponent<SpriteFlipbook>();
            root.AddComponent<VfxOneShot>();
            return root;
        }

        private static SpriteSheetRecipe ExplosionRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(ExplosionRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(ExplosionSheet);
            recipe.characterName = ExplosionCharacter;
            recipe.outputFolder = ExplosionArtFolder;
            recipe.sliceMode = SliceMode.AutoBounds;
            recipe.rows = new[]
            {
                new SheetRow { state = ExplosionState, frames = 5, fps = ExplosionFps, loop = false },
            };
            recipe.keyBackground = false;      // PNG con alfa real
            recipe.alphaThreshold = 0.05f;     // más bajo (0.02) la niebla une dibujos vecinos y salen 4 frames
            recipe.anchor = AnchorMode.Center; // estalla alrededor de su centro
            recipe.runtime = AnimRuntime.Flipbook;   // pooled: sin Animator
            recipe.attackEvents = false;

            AssetDatabase.CreateAsset(recipe, ExplosionRecipePath);
            return recipe;
        }

        // ─── Items ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Crea el item si falta. Si todavía no lleva el efecto del set (placeholder de prueba o recién
        /// creado), le escribe ficha, tags (Hielo + <paramref name="universal"/>), rareza y efectos.
        /// El icono sólo se pone si está vacío.
        /// </summary>
        private static void Item(StringBuilder log, string file, string displayName, ItemRarity rarity,
                                 BuildTag universal, Sprite iconIfMissing, string description, ItemEffect effect)
        {
            string path = $"{ItemsFolder}/{file}.asset";
            var item = AssetDatabase.LoadAssetAtPath<FreePoolItemDefinition>(path);
            bool created = item == null;

            if (created)
            {
                item = ScriptableObject.CreateInstance<FreePoolItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            Type signature = effect.GetType();
            bool install = created || !item.Effects.Any(e => e != null && e.GetType() == signature);

            var so = new SerializedObject(item);
            if (install)
            {
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("description").stringValue = description;
                so.FindProperty("rarity").intValue = (int)rarity;
                if (created) so.FindProperty("accent").colorValue = IceAccent;

                var tags = so.FindProperty("tags");
                tags.arraySize = 2;
                tags.GetArrayElementAtIndex(0).intValue = (int)BuildTag.Ice;
                tags.GetArrayElementAtIndex(1).intValue = (int)universal;

                var list = so.FindProperty("effects");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).managedReferenceValue = effect;
            }

            var iconProp = so.FindProperty("icon");
            bool iconSet = false;
            if (iconProp.objectReferenceValue == null && iconIfMissing != null)
            {
                iconProp.objectReferenceValue = iconIfMissing;
                iconSet = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            string what = created ? "Creado" : install ? "Convertido al set" : "Ya al día";
            log.AppendLine($"  {what} {path}{(iconSet ? " · icono asignado" : "")}");
            if (iconProp.objectReferenceValue == null)
                log.AppendLine($"    AVISO: {file} no tiene icono.");
        }

        /// <summary>Rellena el prefab de explosión donde falte (Yelmo afinado a mano incluido).</summary>
        private static void FillExplosionRefs(GameObject explosion, StringBuilder log)
        {
            if (explosion == null) return;

            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                bool dirty = false;
                foreach (var effect in item.Effects)
                {
                    if (effect is KillCounterExplosionEffect helmet && helmet.explosionPrefab == null)
                    {
                        helmet.explosionPrefab = explosion;
                        dirty = true;
                    }
                }

                if (!dirty) continue;
                EditorUtility.SetDirty(item);
                log.AppendLine($"  Explosión asignada en {item.name}");
            }
        }

        // ─── Sinergias ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Texto de los umbrales implementados, generado con los números actuales de
        /// <see cref="SynergyTuning"/> para que la UI nunca mienta. Los umbrales aún sin implementar
        /// conservan su texto.
        /// </summary>
        private static void SynergyTexts(GameObject explosion, StringBuilder log)
        {
            var config = SynergyConfig.Instance;
            if (config == null)
            {
                log.AppendLine("  AVISO: falta Assets/Resources/SynergyConfig.asset; no se escriben los umbrales.");
                return;
            }

            var t = config.Tuning;
            if (t.ice6BurstPrefab == null && explosion != null) t.ice6BurstPrefab = explosion;

            config.SetTierText(BuildTag.Ice, 1,
                $"Los disparos ralentizan un {t.slowStrength * 100f:0}% durante {t.slowDuration:0.#} s.");
            config.SetTierText(BuildTag.Ice, 2,
                $"Los ralentizados reciben +{t.ice4DamageTakenBonus * 100f:0}% de daño de cualquier fuente.");
            config.SetTierText(BuildTag.Ice, 3,
                $"Matar a un ralentizado suelta un estallido de hielo ({t.ice6BurstDamage:0} de daño, radio {t.ice6BurstRadius:0.#}).");
            config.SetTierText(BuildTag.Haste, 1,
                $"Con un enemigo ralentizado en pantalla, el enfriamiento corre ×{t.haste2CooldownRate:0.##}.");
            config.SetTierText(BuildTag.Reset, 1,
                $"Cada baja quita {t.reset2CooldownReduction:0.#} s al enfriamiento actual.");
            config.SetTierText(BuildTag.Lifesteal, 1,
                $"Cada baja cura {t.lifesteal2HealPerKill:0.#} de vida.");

            EditorUtility.SetDirty(config);
            log.AppendLine("  SynergyConfig: umbrales Hielo 2/4/6, Rapidez 2, Reset 2 y Vampirismo 2 escritos.");
        }
    }
}
