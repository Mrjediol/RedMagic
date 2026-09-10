using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>Ogro</b>. Copia de <see cref="TreeWalkPack"/> con otros datos: es literalmente
    /// todo lo que hace falta escribir para meter una lámina nueva en el juego.
    ///
    /// Lo que la lámina dice de él, por su nombre de archivo
    /// (<c>Ogro-distancia-movimiento-suelo</c>): ataca <b>a distancia</b>, <b>se mueve</b> y
    /// <b>no vuela</b> → arquetipo <see cref="EnemyArchetype.Ranged"/>.
    ///
    /// A diferencia del TreeWalk, esta lámina <b>sí trae fila de andar</b>, así que puede
    /// perseguir de verdad en vez de quedarse como torreta.
    /// </summary>
    public static class OgroPack
    {
        private const string Sheet = "Assets/Sprites/Ogro-distancia-movimiento-suelo.jpeg";
        private const string RecipeFolder = "Assets/Art/Characters/Ogro";
        private const string SheetRecipePath = RecipeFolder + "/Ogro.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/Ogro.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Ogro")]
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
            recipe.characterName = "Ogro";
            recipe.outputFolder = RecipeFolder;

            // Rótulos IDLE/WALK/ATTACK/HURT/DEATH pintados en la propia lámina y frames sin rejilla
            // exacta: se detecta el contenido.
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },
                new SheetRow { state = "Walk",   frames = 5, fps = 10f, loop = true },

                // Sólo hay 4 poses del ogro: el quinto dibujo de la fila es la piedra ya volando,
                // sin ogro. Declarando 4, el slicer la retira de la cuenta de frames y la exporta
                // como sprite suelto — que es exactamente el proyectil. El evento va en el frame
                // en que la suelta.
                new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 3 },

                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            // JPEG: no hay alfa, el fondo está pintado. Se deduce del marco.
            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.12f;

            recipe.anchor = AnchorMode.BottomCenter;   // camina por el suelo: pivote a los pies
            recipe.pixelsPerUnit = 100;
            recipe.filterMode = FilterMode.Bilinear;   // arte pintado, no pixel art
            recipe.margin = 8;

            recipe.runtime = AnimRuntime.Animator;     // enemigo: no pasa por pool

            AssetDatabase.CreateAsset(recipe, SheetRecipePath);
            return recipe;
        }

        // ============================================================ ficha del enemigo

        private static EnemyRecipe Enemy_(SpriteSheetRecipe art)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<EnemyRecipe>(EnemyRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<EnemyRecipe>();

            recipe.enemyName = "Ogro";
            recipe.art = art;

            recipe.spriteScale = 0.85f;   // un ogro: bastante más grande que el jugador
            recipe.sortingOrder = 5;

            var t = recipe.tuning;

            // A distancia, se mueve, no vuela.
            t.archetype = EnemyArchetype.Ranged;

            t.maxHealth = 70f;                // aguanta: es el pesado del grupo
            t.invulnerabilityDuration = 0f;   // los i-frames se comerían las armas multigolpe

            t.knockbackHorizontal = 4f;       // pesa, así que se le mueve poco
            t.knockbackVertical = 2.5f;
            t.knockbackResistance = 0.35f;

            // Persigue desde lejos, tira la piedra a media distancia y se aparta si te pegas a él.
            t.detectionRange = 13f;
            t.loseInterestGrace = 1f;
            t.attackRange = 8f;
            t.personalSpace = 3.5f;
            t.verticalTolerance = 3f;

            t.moveSpeed = 2.2f;               // lento y pesado
            t.retreatSpeed = 1.8f;            // aún más al retroceder: no es ágil
            t.stopAtLedges = true;
            t.gravityScale = 3f;

            t.attackDamage = 12f;
            t.attackCooldown = 2f;
            t.attackKnockbackMultiplier = 1.3f;   // una piedra de ese tamaño empuja

            t.projectile.speed = 9f;
            t.projectile.lifetime = 3.5f;
            t.projectile.size = new Vector2(0.45f, 0.45f);
            t.projectile.muzzleOffset = new Vector2(0.7f, 1f);   // a la altura de la mano
            t.projectile.arcGravity = 0f;
            t.aimAtTarget = true;   // un tiro plano falla en cuanto hay desnivel

            t.contactDamage = 6f;   // si te le pegas encima, te aparta a golpes
            t.contactDamageCooldown = 1f;

            recipe.projectilePropState = "Attack";   // la piedra sale de la fila de ataque
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
