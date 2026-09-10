using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del enemigo <b>TreeWalk</b>, y a la vez la plantilla de cómo se importa cualquier
    /// lámina nueva.
    ///
    /// Un pack no hace trabajo: escribe las dos recetas y llama al pipeline. Todo lo que hay aquí
    /// es la descripción del enemigo — cuántas filas tiene la lámina, a qué estado corresponde
    /// cada una, cuánta vida tiene el bicho. Ni un componente, ni una transición, ni un collider.
    ///
    /// Está en el repo por lo mismo que los packs de jefe: la importación queda reproducible. Si
    /// mañana llega una versión retocada de la lámina, se relanza esto y sale todo otra vez.
    ///
    /// Para el siguiente personaje: copiar este archivo, cambiar los datos, cambiar el nombre del
    /// menú. No hay nada más que tocar.
    /// </summary>
    public static class TreeWalkPack
    {
        private const string Sheet = "Assets/Sprites/TreeWalk.jpg";
        private const string RecipeFolder = "Assets/Art/Characters/TreeWalk";
        private const string SheetRecipePath = RecipeFolder + "/TreeWalk.sheet.asset";
        private const string EnemyRecipePath = RecipeFolder + "/TreeWalk.enemy.asset";

        [MenuItem("Tools/RedMagic/Pipeline/Packs/TreeWalk")]
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
            recipe.characterName = "TreeWalk";
            recipe.outputFolder = RecipeFolder;

            // La lámina lleva los rótulos IDLE/ATTACK/HURT/DEATH pintados y los frames no están
            // en una rejilla exacta, así que se detecta el contenido en vez de dividir por 4x5.
            recipe.sliceMode = SliceMode.AutoBounds;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 5, fps = 8f,  loop = true },
                // El cuarto frame es en el que la piedra ya ha salido de la mano: ahí va el
                // AnimationEvent, así que el proyectil nace justo en ese dibujo.
                new SheetRow { state = "Attack", frames = 5, fps = 12f, loop = false, releaseFrame = 3 },
                new SheetRow { state = "Hurt",   frames = 5, fps = 14f, loop = false },
                new SheetRow { state = "Death",  frames = 5, fps = 8f,  loop = false },
            };

            // Es un JPG: no hay alfa, el damero de transparencia está pintado. Se deduce del marco.
            recipe.keyBackground = true;
            recipe.backgroundTolerance = 0.12f;

            recipe.anchor = AnchorMode.BottomCenter;   // personaje: pivote a los pies
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

            recipe.enemyName = "TreeWalk";
            recipe.art = art;

            // El arte mide ~2.3 unidades de alto a 100 px/u; a 0.7 queda algo por encima del
            // jugador, que es lo que pide un bicho lento de cuerpo a cuerpo.
            recipe.spriteScale = 0.7f;
            recipe.sortingOrder = 5;

            // La lámina no trae animación de andar, así que se queda como ESTÁTICO A DISTANCIA:
            // espera en reposo, y en cuanto el jugador entra en su rango de ataque tira la piedra.
            // Sin caminar no hay nada que quede mal.
            var t = recipe.tuning;
            t.archetype = EnemyArchetype.Static;
            t.staticAttack = AttackKind.Ranged;   // torreta: tira la piedra

            t.maxHealth = 40f;
            t.invulnerabilityDuration = 0f;   // los i-frames se comerían las armas multigolpe

            t.knockbackHorizontal = 6f;       // es un árbol: pesa
            t.knockbackVertical = 3f;
            t.knockbackResistance = 0.2f;

            // Un estático no detecta ni persigue: su único rango es el de ataque.
            t.attackRange = 7f;
            t.attackDamage = 8f;
            t.attackCooldown = 1.8f;
            t.attackKnockbackMultiplier = 1f;
            t.verticalTolerance = 3f;

            t.projectile.speed = 8f;
            t.projectile.lifetime = 3f;
            t.projectile.size = new Vector2(0.35f, 0.35f);
            t.projectile.muzzleOffset = new Vector2(0.55f, 0.9f);   // a la altura de la mano
            t.aimAtTarget = true;   // un tiro plano falla en cuanto hay desnivel

            // Es un árbol clavado en el suelo: si te pegas a él te araña, pero poco.
            t.contactDamage = 4f;
            t.contactDamageCooldown = 1f;

            t.attackAnimSpeed = 1f;
            t.idleAnimSpeed = 1f;

            recipe.projectilePropState = "Attack";
            recipe.projectileScale = 1f;
            recipe.tier = Economy.EnemyTier.Basic;

            AssetDatabase.CreateAsset(recipe, EnemyRecipePath);
            return recipe;
        }
    }
}
