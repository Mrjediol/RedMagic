using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Lobo</b> (lobo de musgo). Solo datos.
    ///
    /// Nombre de archivo <c>Lobo-Distancia-movimiento-suelo</c>: ataca <b>a distancia</b>,
    /// <b>se mueve</b> y <b>no vuela</b> -> <see cref="EnemyArchetype.Ranged"/>, el mismo molde que
    /// el Ogro pero al reves de caracter: ligero, rapidisimo y con muy poca vida. Escupe un orbe.
    /// </summary>
    public static class LoboPack
    {
        private const string Sheet = "Assets/Sprites/Lobo-Distancia-movimiento-suelo.jpeg";
        private const string RecipeFolder = "Assets/Art/Characters/Lobo";
        private const string SheetRecipePath = RecipeFolder + "/Lobo.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Lobo.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Lobo")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada tambien para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(RecipeFolder);

            var sheet = Sheet_();
            var enemy = Enemy_(sheet);

            AssetDatabase.SaveAssets();
            return SpritePipeline.RunEnemy(enemy);
        }

        // ============================================================ receta de la lamina

        private static SpriteSheetRecipe Sheet_()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(SheetRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();

            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            recipe.characterName = "Lobo";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true,  groupSlack = 0.4f },
                new SheetRow { state = "Walk",   frames = 5, fps = 12f, loop = true,  groupSlack = 0.2f },

                // La fila de ataque trae 6 dibujos, pero sólo 4 son el lobo: los otros dos son el
                // orbe ya escupido. 'propBlobs = 2' los aparta por la derecha ANTES de agrupar —
                // hace falta porque el primer orbe se solapa en X con el hocico, y dejarlo a la
                // reconciliación por cuenta lo fundía en la última pose (celda 272 en vez de 192,
                // lobo descentrado y orbe pintado encima del proyectil de verdad). El evento va en
                // el 4º dibujo, que es donde lo suelta.
                new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 3, propBlobs = 2 },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.14f;

            recipe.anchor = AnchorMode.BottomCenter;   // corre por el suelo: pivote a las patas
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

            recipe.enemyName = "Lobo";
            recipe.art = art;

            recipe.spriteScale = 0.9f;   // bajo y alargado: no llega a la altura del jugador
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // A distancia, se mueve, no vuela. Retrocede en cuanto le entras: es un hostigador.
            t.archetype = EnemyArchetype.Ranged;

            t.maxHealth = 40f;                // fragil: lo que le mantiene vivo es no dejarse tocar
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 7f;       // ligero: sale despedido
            t.knockbackVertical = 3f;
            t.knockbackResistance = 0.1f;

            t.detectionRange = 15f;
            t.loseInterestGrace = 1f;
            t.attackRange = 8.5f;
            t.personalSpace = 5f;             // no deja que le llegues: huye antes
            t.verticalTolerance = 3f;

            t.moveSpeed = 4.4f;               // rapido de verdad
            t.retreatSpeed = 4.8f;            // y aun mas al huir
            t.stopAtLedges = true;
            t.gravityScale = 3f;

            t.attackDamage = 10f;
            t.attackCooldown = 1.5f;
            t.attackKnockbackMultiplier = 1f;

            t.projectile.speed = 12f;         // el orbe va rapido y plano
            t.projectile.lifetime = 3f;
            t.projectile.size = new Vector2(0.4f, 0.4f);
            t.projectile.muzzleOffset = new Vector2(0.8f, 0.75f);   // a la altura del hocico
            t.projectile.arcGravity = 0f;
            t.aimAtTarget = true;

            t.contactDamage = 5f;   // muerde si te le pegas
            t.contactDamageCooldown = 1f;

            recipe.projectilePropState = "Attack";
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
