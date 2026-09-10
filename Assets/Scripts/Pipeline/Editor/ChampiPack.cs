using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Champi</b>. Sólo datos, como el resto.
    ///
    /// Nombre de archivo <c>champi-distancia-statico-suelo</c>: ataca <b>a distancia</b>, es
    /// <b>estático</b> y está <b>en el suelo</b> → <see cref="EnemyArchetype.Static"/> +
    /// <see cref="AttackKind.Ranged"/>, con gravedad normal (tiene pies, se planta en el terreno).
    /// Lanza su propio sombrero: en la fila de ataque hay un dibujo que es sólo la seta volando.
    /// </summary>
    public static class ChampiPack
    {
        private const string Sheet = "Assets/Sprites/champi-distancia-statico-suelo.jpg";
        private const string RecipeFolder = "Assets/Art/Characters/Champi";
        private const string SheetRecipePath = RecipeFolder + "/Champi.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Champi.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Champi")]
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
            recipe.characterName = "Champi";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },

                // 5 poses del champi + el sombrero suelto volando entre medias. Se declaran las 5
                // poses: el reconciliador retira el sombrero (la mancha más pequeña) y lo exporta
                // como prop, que es el proyectil. El evento va en el frame en que lo suelta.
                new SheetRow { state = "Attack", frames = 5, fps = 12f, loop = false, releaseFrame = 3 },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            recipe.keyBackground = true;          // JPG: fondo blanco pintado, se deduce del marco
            recipe.backgroundTolerance = 0.12f;

            recipe.anchor = AnchorMode.BottomCenter;   // clavado en el suelo: pivote a los pies
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

            recipe.enemyName = "Champi";
            recipe.art = art;

            recipe.spriteScale = 0.55f;   // el sombrero lo hace ancho, pero es un bicho pequeño
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // Estático a distancia: no detecta ni persigue, su único rango es el de ataque.
            t.archetype = EnemyArchetype.Static;
            t.staticAttack = AttackKind.Ranged;
            t.gravityScale = 3f;   // tiene pies: se planta en el suelo

            t.maxHealth = 30f;
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 6f;
            t.knockbackVertical = 3f;
            t.knockbackResistance = 0.15f;

            t.attackRange = 8f;
            t.verticalTolerance = 3.5f;
            t.attackDamage = 7f;
            t.attackCooldown = 1.7f;
            t.attackKnockbackMultiplier = 0.8f;

            t.projectile.speed = 8.5f;
            t.projectile.lifetime = 3f;
            t.projectile.size = new Vector2(0.35f, 0.35f);
            t.projectile.muzzleOffset = new Vector2(0.4f, 0.55f);   // sale de la mano
            t.aimAtTarget = true;

            t.contactDamage = 4f;   // si te le pegas encima, esporas
            t.contactDamageCooldown = 1f;

            recipe.projectilePropState = "Attack";   // el sombrero sale de la fila de ataque
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
