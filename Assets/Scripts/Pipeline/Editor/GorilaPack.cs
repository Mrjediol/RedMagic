using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Gorila</b> (gorila de musgo). Sólo datos.
    ///
    /// Nombre de archivo <c>Gorilla-meele-movimiento-suelo</c>: ataca <b>cuerpo a cuerpo</b>,
    /// <b>se mueve</b> y <b>no vuela</b> → <see cref="EnemyArchetype.Melee"/>. La lámina trae fila
    /// de andar, así que persigue de verdad. Su ataque es un golpe: los últimos frames dibujan
    /// polvo y cascotes del impacto, no un proyectil suelto, así que <b>no</b> exporta prop y
    /// todas las filas llevan sus 5 dibujos.
    /// </summary>
    public static class GorilaPack
    {
        private const string Sheet = "Assets/Sprites/Gorilla-meele-movimiento-suelo.jpeg";
        private const string RecipeFolder = "Assets/Art/Characters/Gorila";
        private const string SheetRecipePath = RecipeFolder + "/Gorila.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Gorila.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Gorila")]
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
            recipe.characterName = "Gorila";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },
                new SheetRow { state = "Walk",   frames = 5, fps = 11f, loop = true },

                // Golpe cuerpo a cuerpo. Los dos últimos frames son el pisotón: un estallido
                // blanco y una nube de polvo, anchos y pisándose en horizontal, que la detección
                // por manchas fundía en un solo frame doble de ancho. 'evenSplit' parte la fila en
                // 5 columnas iguales en su lugar. El evento va en el 4º dibujo (la maza abajo).
                new SheetRow { state = "Attack", frames = 5, fps = 12f, loop = false, releaseFrame = 3, evenSplit = true },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.14f;   // la lámina trae el damero de transparencia pintado

            recipe.anchor = AnchorMode.BottomCenter;   // camina por el suelo: pivote a los pies
            recipe.pixelsPerUnit = 100;
            recipe.filterMode = FilterMode.Bilinear;
            recipe.margin = 8;

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

            recipe.enemyName = "Gorila";
            recipe.art = art;

            recipe.spriteScale = 1.05f;   // grande: bastante más que el jugador
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // Cuerpo a cuerpo, se mueve, no vuela. Un melé persigue y pega — no retrocede nunca,
            // así que personalSpace se queda a 0 (y aunque no lo estuviera, Retreats lo ignora).
            t.archetype = EnemyArchetype.Melee;

            t.maxHealth = 120f;               // el tanque del grupo
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 3f;       // pesa mucho: casi no se le mueve
            t.knockbackVertical = 2f;
            t.knockbackResistance = 0.5f;

            t.detectionRange = 12f;
            t.loseInterestGrace = 1f;
            t.attackRange = 2.2f;             // brazos largos, pero es melé
            t.verticalTolerance = 3f;

            t.moveSpeed = 2.8f;               // no es lento: cierra distancia con decisión
            t.stopAtLedges = true;
            t.gravityScale = 3f;

            t.attackDamage = 20f;             // un mazazo
            t.attackCooldown = 2.2f;          // pero tarda en recuperarse
            t.attackKnockbackMultiplier = 2f;
            t.meleeHitboxSize = new Vector2(2f, 1.8f);
            t.meleeHitboxOffset = new Vector2(1.2f, 0.9f);

            t.contactDamage = 8f;   // chocar con él ya duele
            t.contactDamageCooldown = 1f;
            t.contactKnockbackMultiplier = 1.5f;

            recipe.tier = Economy.EnemyTier.Elite;   // no es un bicho de relleno

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
