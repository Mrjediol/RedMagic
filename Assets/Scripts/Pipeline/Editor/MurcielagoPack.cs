using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Murciélago</b> kamikaze. Sólo datos.
    ///
    /// Duerme colgado (<see cref="EnemyTuning.sleepsUntilDetected"/>); al detectar al jugador
    /// despliega las alas (<c>Wake</c>), se lanza en picado hacia él (<c>Walk</c>) y al llegar a
    /// su rango se ilumina (<c>Attack</c>, la mecha) y revienta (<c>Death</c>) —
    /// <see cref="EnemyTuning.selfDestruct"/>. Si se rinde, vuelve a su techo a dormir.
    ///
    /// La lámina (6 filas × 5, alfa real de removebg):
    /// <list type="number">
    /// <item>colgado dormido → <c>Idle</c></item>
    /// <item>despliega las alas → <c>Wake</c></item>
    /// <item>vuelo normal → <c>Fly</c> (sin uso de momento; se declara porque el corte automático
    /// necesita las 6 franjas)</item>
    /// <item>picado: 3 poses, un estallido de velocidad y la estela → <c>Walk</c>, con
    /// <c>frameRects</c> para saltarse el estallido (4 frames: poses + estela)</item>
    /// <item>se ilumina → <c>Attack</c> (la mecha, suelta en el último frame)</item>
    /// <item>revienta en esquirlas y anillos → <c>Death</c>, con <c>evenSplit</c> porque los dos
    /// anillos finales se tocan</item>
    /// </list>
    /// </summary>
    public static class MurcielagoPack
    {
        private const string Sheet = "Assets/Sprites/Gemini_Generated_Image_cl5nqicl5nqicl5n-removebg-preview.png";
        private const string RecipeFolder = "Assets/Art/Characters/Murcielago";
        private const string SheetRecipePath = RecipeFolder + "/Murcielago.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Murcielago.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Murcielago")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(RecipeFolder);

            var sheet = Sheet_();
            var enemy = Enemy_(sheet);

            AssetDatabase.SaveAssets();
            return SpritePipeline.RunEnemy(enemy);
        }

        // ============================================================ receta de la lámina

        private static SpriteSheetRecipe Sheet_()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(SheetRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();

            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            recipe.characterName = "Murcielago";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",  frames = 5, fps = 6f,  loop = true },
                new SheetRow { state = "Wake",  frames = 5, fps = 10f, loop = false },
                new SheetRow { state = "Fly",   frames = 5, fps = 12f, loop = true },

                // El picado. El 4º dibujo es un estallido de velocidad, no el murciélago: los
                // recuadros (medidos en el Sprite Editor, y=0 abajo) toman las 3 poses y la estela.
                new SheetRow
                {
                    state = "Walk", frames = 4, fps = 12f, loop = true,
                    frameRects = new[]
                    {
                        new RectInt(2, 203, 69, 73),
                        new RectInt(86, 208, 75, 67),
                        new RectInt(178, 210, 78, 67),
                        new RectInt(351, 215, 78, 45),
                    },
                },

                // La mecha: se ilumina y explota al acabar. Rápida (0.31s) — es un aviso, no una
                // pausa; el murciélago sigue persiguiendo mientras tanto.
                new SheetRow { state = "Attack", frames = 5, fps = 16f, loop = false, releaseFrame = 4 },

                new SheetRow { state = "Death", frames = 5, fps = 14f, loop = false, evenSplit = true },
            };

            recipe.keyBackground = false;   // PNG con alfa real
            recipe.anchor = AnchorMode.Center;   // vuela: sin pies
            recipe.pixelsPerUnit = 100;
            recipe.filterMode = FilterMode.Bilinear;
            recipe.margin = 6;

            recipe.runtime = AnimRuntime.Animator;

            AssetDatabase.CreateAsset(recipe, SheetRecipePath);
            return recipe;
        }

        // ============================================================ ficha del enemigo

        private static EnemyRecipe Enemy_(SpriteSheetRecipe art)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<EnemyRecipe>(EnemyRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<EnemyRecipe>();

            recipe.enemyName = "Murcielago";
            recipe.art = art;

            recipe.spriteScale = 1.4f;                         // la lámina es pequeña (~80px por pose)
            recipe.colliderSize = new Vector2(0.7f, 0.7f);     // el cuerpo, no las alas ni el colgar
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            t.archetype = EnemyArchetype.FlyingMelee;
            t.gravityScale = 0f;

            t.sleepsUntilDetected = true;
            t.selfDestruct = true;

            t.maxHealth = 12f;                  // frágil: la respuesta es derribarlo antes de que llegue
            t.invulnerabilityDuration = 0f;
            t.knockbackHorizontal = 7f;
            t.knockbackVertical = 2f;

            t.detectionRange = 7f;
            t.loseInterestGrace = 1f;
            t.attackRange = 1.2f;               // centro a centro: casi tocando al jugador
            t.verticalTolerance = 0f;

            t.moveSpeed = 4.5f;
            t.stopAtLedges = false;
            t.hoverOffset = 0f;                 // el pivote del jugador ya está en su centro

            t.attackDamage = 20f;
            t.attackKnockbackMultiplier = 1.5f;
            t.rootedWhileAttacking = false;     // sigue encima del jugador mientras arde la mecha
            t.explosionRadius = 1.7f;
            t.explosionShake = 0.2f;

            t.contactDamage = 0f;               // su daño es la explosión

            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
