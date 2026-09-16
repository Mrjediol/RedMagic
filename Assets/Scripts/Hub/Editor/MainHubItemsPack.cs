using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Hub.EditorTools
{
    /// <summary>
    /// El pack de los 5 props del hub (cofre, libro, yunque, armario, espejo), los cinco cortados
    /// de la misma lámina, <c>Assets/Sprites/Maibhubitems.png</c>.
    ///
    /// No encaja en el molde de <c>&lt;Nombre&gt;Pack.cs</c> de un enemigo o un jefe — una receta,
    /// un personaje — porque aquí <b>una lámina son cinco objetos</b>, cada uno con su propio
    /// <see cref="AnimatorController"/> construido con <see cref="OpenCloseControllerBuilder"/> en
    /// vez de <c>AnimClipBuilder.BuildController</c> (pensado para el vocabulario de un enemigo).
    /// Aun así sigue la misma idea: los datos de la receta viven aquí, re-lanzable de una vez
    /// (idempotente en el corte y los clips; los 4 prefabs sólo se crean si no existen — si ya
    /// existen se avisa en vez de pisarlos, porque para entonces pueden llevar posición o
    /// referencias puestas a mano en una escena).
    ///
    /// Uso: <b>Tools ▸ RedMagic ▸ Boss ▸ (no, aquí no) — Tools ▸ RedMagic ▸ Hub ▸ Generar props del
    /// hub</b>. Headless: <c>unity command run_script --file Assets/Scripts/Hub/Editor/
    /// MainHubItemsPack.cs --entry RedMagic.Hub.EditorTools.MainHubItemsPack.Run</c>.
    /// </summary>
    public static class MainHubItemsPack
    {
        private const string SheetPath = "Assets/Sprites/Maibhubitems.png";
        private const string CharacterName = "MainHubItems";
        private const string RootFolder = "Assets/Art/Characters/MainHubItems";
        private const string RecipePath = RootFolder + "/MainHubItems.sheet.asset";
        private const string AnimFolder = RootFolder + "/Anim/";
        private const string PrefabFolder = "Assets/Prefab/Eviroment/";

        [MenuItem("Tools/RedMagic/Hub/Generar props del hub", priority = 400)]
        public static void Run()
        {
            var recipe = BuildRecipe();

            var sliced = SheetSlicer.Slice(recipe);
            Debug.Log(sliced.Log);
            if (!sliced.Ok) return;

            // Dos pasadas por el mismo motivo que SpritePipeline.RunSheet: en la primera
            // importación de una lámina nueva los sub-sprites recién cortados no están listos en
            // el mismo tick y la primera pasada deja algún clip vacío.
            var log = new System.Text.StringBuilder();
            AnimClipBuilder.BuildClips(recipe, sliced, log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AnimClipBuilder.BuildClips(recipe, sliced, log);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());

            BuildControllers();
            DressGoldChest();
            CreatePropPrefabIfMissing<BookLootContainer>("BookLectern", "BookOpening", "Book");
            CreatePropPrefabIfMissing<WardrobeLootContainer>("Wardrobe", "WardrobeOpening", "Wardrobe");
            CreatePropPrefabIfMissing<AnvilInteractable>("Anvil", "AnvilSpark", "Anvil");
            CreatePropPrefabIfMissing<MirrorInteractable>("Mirror", "MirrorIdle", "Mirror");

            AssetDatabase.SaveAssets();
            Debug.Log("[MainHubItemsPack] Listo.");
        }

        // ------------------------------------------------------------------ receta

        private static SpriteSheetRecipe BuildRecipe()
        {
            EnsureFolder(RootFolder);

            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(RecipePath);
            bool created = recipe == null;
            if (created) recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();

            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            recipe.characterName = CharacterName;
            recipe.attackEvents = false; // ninguno de estos objetos lleva EnemyAnimation

            // Fondo verde oscuro plano, igual que TreeBoss.png/BossAttack.png: softEdge para que
            // el halo del brillo no salga cortado a tijera, fillHoles porque las sombras internas
            // del arte cogen el tono del fondo y el recorte las perfora.
            //
            // El fondo de ESTA lámina en concreto comparte tono con parte del propio arte (la
            // veta de la madera, el brillo teal, el cristal del espejo): con la tolerancia por
            // defecto (0.12) el recorte se comía trozos reales — madera del cofre y del armario
            // agujereada, media luna del espejo desaparecida. Bajarla a 0.06 lo arregla casi
            // del todo; lo que queda (un pelín de fondo en el borde del espejo, cuyo cristal es el
            // color más parecido al fondo de los cinco objetos) es el lado bueno del compromiso:
            // un poco de fondo colándose en el borde molesta menos que perder contenido real en
            // medio del objeto. Subir fillHoles/softEdge por encima de esto no mejoró nada más —
            // ver la memoria "keyed-background-tolerance-is-a-tradeoff". No hay un valor que deje
            // el corte perfecto en una lámina así; éste es el que el usuario prefirió tras
            // comparar varias pasadas.
            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.06f;
            recipe.softEdge = 14;
            recipe.fillHoles = 400;

            // Medido con Pipeline ▸ 2b (Diagnosticar bandas y manchas): armario y espejo sólo
            // traen 6 dibujos reales, no 7 — la lámina no es una rejilla uniforme por fila.
            recipe.rows = new[]
            {
                new SheetRow { state = "ChestOpening", frames = 7, fps = 8f, loop = false },
                new SheetRow { state = "BookOpening", frames = 7, fps = 8f, loop = false },
                new SheetRow { state = "AnvilSpark", frames = 4, fps = 10f, loop = false },
                new SheetRow { state = "WardrobeOpening", frames = 6, fps = 8f, loop = false },
                new SheetRow { state = "MirrorIdle", frames = 6, fps = 6f, loop = true },
            };

            // Cada fila ya dibuja el ciclo abrir+cerrar completo (cerrado → grieta → brillo pico →
            // brillo atenuado → grieta → cerrado), así que "cerrar" no hace falta invertirlo: son
            // los últimos frames de la propia fila, en el mismo orden en que están dibujados.
            recipe.derivedClips = new[]
            {
                Derived("ChestClosed", "ChestOpening", 0, 1, 8f, loop: true),
                Derived("ChestOpeningAnim", "ChestOpening", 0, 4, 8f, loop: false),
                Derived("ChestOpen", "ChestOpening", 3, 2, 4f, loop: true),
                Derived("ChestClosing", "ChestOpening", 4, 3, 8f, loop: false),

                Derived("BookClosed", "BookOpening", 0, 1, 8f, loop: true),
                Derived("BookOpeningAnim", "BookOpening", 0, 4, 8f, loop: false),
                Derived("BookOpen", "BookOpening", 3, 2, 4f, loop: true),
                Derived("BookClosing", "BookOpening", 4, 3, 8f, loop: false),

                Derived("WardrobeClosed", "WardrobeOpening", 0, 1, 8f, loop: true),
                Derived("WardrobeOpeningAnim", "WardrobeOpening", 0, 3, 8f, loop: false),
                Derived("WardrobeOpen", "WardrobeOpening", 2, 2, 4f, loop: true),
                Derived("WardrobeClosing", "WardrobeOpening", 3, 3, 8f, loop: false),

                // El yunque no necesita cortar nada de más: la fila entera YA es el gesto
                // (reposo → chispa → chispa → reposo) que se dispara de un tirón. Sólo hace falta
                // un fotograma de reposo suelto para el estado Idle de fuera del gesto.
                Derived("AnvilIdle", "AnvilSpark", 0, 1, 8f, loop: true),
            };

            if (created)
            {
                AssetDatabase.CreateAsset(recipe, RecipePath);
            }
            else
            {
                EditorUtility.SetDirty(recipe);
            }

            AssetDatabase.SaveAssets();
            return recipe;
        }

        private static DerivedClip Derived(string state, string from, int first, int count, float fps, bool loop) =>
            new DerivedClip { state = state, fromState = from, firstFrame = first, frameCount = count, fps = fps, loop = loop };

        // ------------------------------------------------------------------ controllers

        private static void BuildControllers()
        {
            OpenCloseControllerBuilder.BuildBoolDriven(RootFolder + "/Chest.controller", "IsOpened",
                Clip("ChestClosed"), Clip("ChestOpeningAnim"), Clip("ChestOpen"), Clip("ChestClosing"));

            OpenCloseControllerBuilder.BuildBoolDriven(RootFolder + "/Book.controller", "IsOpened",
                Clip("BookClosed"), Clip("BookOpeningAnim"), Clip("BookOpen"), Clip("BookClosing"));

            OpenCloseControllerBuilder.BuildBoolDriven(RootFolder + "/Wardrobe.controller", "IsOpened",
                Clip("WardrobeClosed"), Clip("WardrobeOpeningAnim"), Clip("WardrobeOpen"), Clip("WardrobeClosing"));

            OpenCloseControllerBuilder.BuildTriggerOneShot(RootFolder + "/Anvil.controller", "Spark",
                Clip("AnvilIdle"), Clip("AnvilSpark"));

            OpenCloseControllerBuilder.BuildIdleLoop(RootFolder + "/Mirror.controller", Clip("MirrorIdle"));

            AssetDatabase.SaveAssets();
        }

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}{CharacterName}_{name}.anim");

        // ------------------------------------------------------------------ prefabs

        /// <summary>
        /// Viste <c>GoldChest.prefab</c> con el arte y el controller nuevos, sin tocar nada más —
        /// mismo trato que <c>PrefabDresser</c>: sólo <c>SpriteRenderer</c> y <c>Animator</c>.
        /// El hijo del sprite se renombra a "Sprite" porque los clips animan esa ruta por convenio
        /// (<see cref="AnimClipBuilder.RendererPath"/>); en este prefab se llamaba "Chest".
        /// </summary>
        private static void DressGoldChest()
        {
            const string path = "Assets/Prefab/Eviroment/GoldChest.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[MainHubItemsPack] No se encuentra '{path}'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);

            var spriteChild = root.transform.Find(AnimClipBuilder.RendererPath) ?? root.transform.Find("Chest");
            if (spriteChild == null)
            {
                Debug.LogWarning("[MainHubItemsPack] GoldChest no tiene un hijo con el sprite.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            spriteChild.name = AnimClipBuilder.RendererPath;

            var renderer = spriteChild.GetComponent<SpriteRenderer>();
            renderer.sprite = FirstFrame("ChestOpening");
            renderer.color = Color.white;

            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(RootFolder + "/Chest.controller");

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[MainHubItemsPack] GoldChest vestido con el arte nuevo.");
        }

        /// <summary>
        /// Crea el prefab si no existe todavía. Si ya existe se deja tal cual — puede llevar
        /// posición, tamaño de collider o referencias puestas a mano tras colocarlo en una escena,
        /// y regenerar el arte no debe pisar eso (misma regla que "la ficha manda sobre el
        /// prefab" del resto del pipeline).
        /// </summary>
        private static void CreatePropPrefabIfMissing<T>(string name, string sheetState,
                                                          string controllerName) where T : Component
        {
            string path = $"{PrefabFolder}{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[MainHubItemsPack] '{path}' ya existe, no se toca.");
                return;
            }

            var root = new GameObject(name);
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            var spriteGo = new GameObject(AnimClipBuilder.RendererPath);
            spriteGo.transform.SetParent(root.transform, false);
            var renderer = spriteGo.AddComponent<SpriteRenderer>();
            var sprite = FirstFrame(sheetState);
            renderer.sprite = sprite;
            if (sprite != null) collider.size = sprite.bounds.size;

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"{RootFolder}/{controllerName}.controller");

            root.AddComponent<T>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[MainHubItemsPack] Creado '{path}'.");
        }

        private static Sprite FirstFrame(string sheetState)
        {
            string pngPath = $"{RootFolder}/{CharacterName}_{sheetState}.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pngPath))
                if (asset is Sprite sprite && sprite.name.EndsWith("_00"))
                    return sprite;

            Debug.LogWarning($"[MainHubItemsPack] No se encontró el frame 0 de '{sheetState}' en '{pngPath}'.");
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
