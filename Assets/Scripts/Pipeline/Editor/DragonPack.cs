using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Dragón</b> (cría de dragón de bosque). Sólo datos.
    ///
    /// Nombre de archivo <c>Dragon-Distancia-Movimiento-Volador</c>: ataca <b>a distancia</b>,
    /// <b>se mueve</b> y <b>vuela</b> → <see cref="EnemyArchetype.FlyingRanged"/>. A diferencia de
    /// la Abeja (que sólo flota en su sitio), éste sí persigue por el aire, así que le toca un
    /// arquetipo <c>Flying*</c> de verdad y no un <c>Static</c> con gravedad 0.
    ///
    /// De las dos versiones de la lámina se usa la segunda: el arte es más limpio y, sobre todo,
    /// en la fila de ataque el orbe está dibujado suelto y separado del dragón, que es justo lo
    /// que el corte necesita para exportarlo como proyectil.
    /// </summary>
    public static class DragonPack
    {
        private const string Sheet = "Assets/Sprites/Dragon-Distancia-Movimiento-Volador (2).jpeg";
        private const string RecipeFolder = "Assets/Art/Characters/Dragon";
        private const string SheetRecipePath = RecipeFolder + "/Dragon.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Dragon.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Dragon")]
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
            recipe.characterName = "Dragon";
            recipe.outputFolder = RecipeFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },

                // La fila rotulada FLY es su locomoción: se declara como 'Walk' porque ése es el
                // estado que el generador cablea con el parámetro Speed. Un estado llamado 'Fly'
                // se crearía igual, pero sin transiciones y sin que nada lo dispare.
                new SheetRow { state = "Walk",   frames = 5, fps = 12f, loop = true },

                // 6 dibujos, 4 poses: los dos últimos son la bola de fuego ya escupida.
                // 'propBlobs = 2' los aparta por la derecha y los exporta como prop; el primero
                // (el más "en vuelo") es el que se convierte en proyectil.
                new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 3, propBlobs = 2 },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.14f;

            // Vuela: el pivote va al centro. Con BottomCenter, la cola y las patas colgando harían
            // de "suelo" y el collider quedaría descolgado (misma razón que en la Abeja).
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

            recipe.enemyName = "Dragon";
            recipe.art = art;

            recipe.spriteScale = 0.9f;
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // Vuela persiguiendo y dispara. La fábrica le quita la gravedad por ser Flying*.
            t.archetype = EnemyArchetype.FlyingRanged;
            t.gravityScale = 0f;

            t.maxHealth = 55f;
            t.invulnerabilityDuration = 0f;

            t.knockbackHorizontal = 6f;
            t.knockbackVertical = 3f;
            t.knockbackResistance = 0.15f;

            t.detectionRange = 15f;
            t.loseInterestGrace = 1f;
            t.attackRange = 9f;
            t.personalSpace = 5f;             // se aparta si le saltas encima
            t.verticalTolerance = 0f;         // vuela: no le importa el desnivel

            t.moveSpeed = 3.6f;
            t.retreatSpeed = 3.8f;
            t.stopAtLedges = false;           // no pisa suelo
            t.hoverOffset = 2.5f;             // se queda por encima: obliga a mirar arriba

            t.attackDamage = 12f;
            t.attackCooldown = 2f;
            t.attackKnockbackMultiplier = 1.2f;

            t.projectile.speed = 10f;
            t.projectile.lifetime = 3.5f;
            t.projectile.size = new Vector2(0.45f, 0.45f);
            t.projectile.muzzleOffset = new Vector2(0.8f, 0.1f);   // el hocico (pivote al centro)
            t.projectile.arcGravity = 0f;
            t.aimAtTarget = true;

            t.contactDamage = 6f;
            t.contactDamageCooldown = 1f;

            recipe.projectilePropState = "Attack";
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Elite;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
