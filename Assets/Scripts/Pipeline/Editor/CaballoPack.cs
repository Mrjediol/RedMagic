using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Caballo</b> (caballo de madera y musgo). Sólo datos.
    ///
    /// Nombre de archivo <c>caballo-mele-movimiento-suelo</c>: ataca <b>cuerpo a cuerpo</b>,
    /// <b>se mueve</b> y <b>no vuela</b> → <see cref="EnemyArchetype.Melee"/>.
    ///
    /// <b>Pendiente</b>: su ataque no va a ser el genérico — la fila de ataque dibuja una embestida
    /// con la cabeza, no un golpe en el sitio. De momento lleva el melé por defecto de la fábrica
    /// (caja de golpe hacia donde mira) para que sea jugable ya; la carga es trabajo aparte.
    /// </summary>
    public static class CaballoPack
    {
        private const string Sheet = "Assets/Sprites/caballo-mele-movimiento-suelo.jpeg";
        private const string RecipeFolder = "Assets/Art/Characters/Caballo";
        private const string SheetRecipePath = RecipeFolder + "/Caballo.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Caballo.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Caballo")]
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
            recipe.characterName = "Caballo";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            // Todas las filas traen 5 dibujos. El caballo galopa con la cola muy larga: entre dos
            // poses quedan huecos reales de 3-5 px que el margen por defecto (~13 px) se comía,
            // fundiendo la fila entera en un frame. Por eso 'groupSlack' bajo en las filas con
            // hueco, y 'evenSplit' sólo en la de ataque, donde el FX sí invade al vecino.
            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },
                new SheetRow { state = "Walk",   frames = 5, fps = 12f, loop = true,  groupSlack = 0.15f },

                // Embestida. Los dos últimos dibujos traen el destello y el estallido de hojas del
                // impacto, anchos y pisándose en horizontal: la detección por manchas los fundiría
                // o los contaría como frame suelto. 'evenSplit' parte la fila en 5 columnas iguales,
                // igual que en el Gorila. El evento va en el 4º dibujo, cuando la cabeza llega.
                new SheetRow { state = "Attack", frames = 5, fps = 13f, loop = false, releaseFrame = 3, evenSplit = true },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false, groupSlack = 0.5f },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false, groupSlack = 0.3f },
            };

            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.14f;   // la lámina trae el damero de transparencia pintado

            recipe.anchor = AnchorMode.BottomCenter;   // camina por el suelo: pivote a los cascos
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

            recipe.enemyName = "Caballo";
            recipe.art = art;

            recipe.spriteScale = 1f;
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // Melé que se mueve. Un melé no retrocede nunca: personalSpace se queda a 0.
            t.archetype = EnemyArchetype.Melee;

            t.maxHealth = 90f;
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 4f;       // pesa: se le mueve poco
            t.knockbackVertical = 2f;
            t.knockbackResistance = 0.4f;

            // Es lo que un caballo tiene de particular: te ve de lejos y llega enseguida.
            t.detectionRange = 15f;
            t.loseInterestGrace = 1f;
            t.attackRange = 2f;
            t.verticalTolerance = 3f;

            t.moveSpeed = 4.6f;               // el enemigo más rápido de a pie
            t.stopAtLedges = true;
            t.gravityScale = 3f;

            t.attackDamage = 16f;
            t.attackCooldown = 1.7f;
            t.attackKnockbackMultiplier = 2.2f;   // una embestida manda lejos
            t.meleeHitboxSize = new Vector2(1.8f, 1.3f);
            t.meleeHitboxOffset = new Vector2(1.3f, 0.8f);   // la cabeza, por delante

            t.contactDamage = 8f;             // atropella
            t.contactDamageCooldown = 1f;
            t.contactKnockbackMultiplier = 1.6f;

            recipe.tier = Economy.EnemyTier.Elite;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
