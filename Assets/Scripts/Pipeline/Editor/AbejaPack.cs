using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack de la <b>Abeja</b>. Mismo molde que <see cref="TreeWalkPack"/> y
    /// <see cref="OgroPack"/>: sólo datos.
    ///
    /// Lo que dice su nombre de archivo (<c>abeja-distancia-statica-aire</c>): ataca <b>a
    /// distancia</b>, es <b>estática</b> y está <b>en el aire</b>.
    ///
    /// Eso se traduce en <see cref="EnemyArchetype.Static"/> +
    /// <see cref="AttackKind.Ranged"/> — una torreta — con la particularidad de que flota: los
    /// arquetipos <c>Flying*</c> son los que <i>vuelan persiguiendo</i>, y ésta no persigue, así
    /// que lo único que necesita del vuelo es <b>no caerse</b>. Se consigue con
    /// <see cref="EnemyTuning.gravityScale"/> a 0, que es lo que <c>EnemyStats.Apply</c> vuelca al
    /// <c>Rigidbody2D</c>. Queda clavada a la altura a la que la dejes en la escena.
    /// </summary>
    public static class AbejaPack
    {
        private const string Sheet = "Assets/Sprites/abeja-distancia-statica-aire.jpg";
        private const string RecipeFolder = "Assets/Art/Characters/Abeja";
        private const string SheetRecipePath = RecipeFolder + "/Abeja.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Abeja.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Abeja")]
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
            recipe.characterName = "Abeja";
            recipe.outputFolder = RecipeFolder;

            recipe.sliceMode = SliceMode.AutoBounds;

            // Cuatro filas: esta lámina no trae andar (no le hace falta, es estática).
            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 10f, loop = true },

                // En la fila de ataque, uno de los dibujos es SÓLO el aguijón disparado, sin la
                // abeja. Declarando 4 frames, ese blob suelto se reconcilia con su vecino y se
                // exporta aparte como sprite: es el proyectil. El evento va donde lo suelta.
                new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 2 },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.12f;

            // Flota: el pivote va al centro, no a los pies. Con BottomCenter, el aguijón de abajo
            // haría de "suelo" y la abeja quedaría descolgada respecto a su propio collider.
            recipe.anchor = AnchorMode.Center;
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

            recipe.enemyName = "Abeja";
            recipe.art = art;

            recipe.spriteScale = 0.45f;   // bicho pequeño
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // Torreta que dispara. No persigue, así que no lleva Flying*: sólo necesita no caerse.
            t.archetype = EnemyArchetype.Static;
            t.staticAttack = AttackKind.Ranged;

            // Esto es lo que la mantiene en el aire: EnemyStats.Apply lo vuelca al Rigidbody2D.
            t.gravityScale = 0f;

            t.maxHealth = 25f;                // frágil: molesta desde arriba, no aguanta
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 7f;       // pesa poco: sale despedida
            t.knockbackVertical = 3f;
            t.knockbackResistance = 0f;

            // Un estático no detecta ni persigue: su único rango es el de ataque.
            t.attackRange = 9f;               // largo, porque está en alto y no puede reposicionarse
            t.verticalTolerance = 8f;         // dispara hacia abajo: tiene que "ver" bastante bajo

            t.attackDamage = 6f;
            t.attackCooldown = 1.6f;
            t.attackKnockbackMultiplier = 0.6f;

            t.projectile.speed = 11f;         // el aguijón va rápido
            t.projectile.lifetime = 3f;
            t.projectile.size = new Vector2(0.3f, 0.3f);
            t.projectile.muzzleOffset = new Vector2(0.3f, -0.2f);   // sale por el aguijón, hacia abajo
            t.aimAtTarget = true;

            t.contactDamage = 4f;   // si la tocas, pica
            t.contactDamageCooldown = 1f;

            recipe.projectilePropState = "Attack";   // el aguijón sale de la fila de ataque
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
