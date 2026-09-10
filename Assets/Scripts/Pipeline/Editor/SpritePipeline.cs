using System.Text;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// La puerta de entrada al pipeline: una llamada por receta.
    ///
    /// Existe para que importar una lámina nueva sea una orden, no un procedimiento. Una sesión
    /// futura sólo necesita el asset de receta y <see cref="RunSheet"/> / <see cref="RunEnemy"/>;
    /// no tiene que saber nada del corte, de los clips ni de qué componentes lleva un enemigo.
    /// </summary>
    public static class SpritePipeline
    {
        /// <summary>Corta la lámina y genera clips + controller (o rellena el flipbook).</summary>
        public static string RunSheet(SpriteSheetRecipe recipe)
        {
            var log = new StringBuilder();

            var sliced = SheetSlicer.Slice(recipe);
            log.Append(sliced.Log);
            if (!sliced.Ok) return log.ToString();

            if (recipe.runtime == AnimRuntime.Animator)
            {
                // Se construyen los clips, se asienta el AssetDatabase y se vuelven a construir. En
                // la PRIMERA importación de una lámina nueva los sub-sprites recién cortados no
                // siempre están listos en el mismo tick, y el primer pase deja algún clip vacío
                // (1 s, 60 fps, sin eventos). El segundo, ya con todo importado, lo rellena. Es
                // idempotente, así que en las regeneraciones siguientes el segundo pase no cambia
                // nada. Esto es lo que quita el "hay que lanzar el pack dos veces".
                AnimClipBuilder.BuildClips(recipe, sliced, log);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var clips = AnimClipBuilder.BuildClips(recipe, sliced, log);
                AnimClipBuilder.BuildController(recipe, clips, log);
            }
            else
            {
                log.AppendLine("  runtime = Flipbook: sin clips ni controller; los estados se " +
                               "escriben en el SpriteStateMachine al generar/vestir el prefab.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        /// <summary>
        /// Lámina + prefab de enemigo, de una vez. Es el camino normal.
        ///
        /// Con <paramref name="resetTuning"/> el bloque de valores del prefab vuelve al de la
        /// ficha; sin él se respeta lo que se haya afinado a mano en el <c>EnemyStats</c>, que es
        /// lo que se quiere el 99% de las veces (regenerar el arte no debe borrar los números).
        /// </summary>
        public static string RunEnemy(EnemyRecipe recipe, bool resetTuning = false)
        {
            var log = new StringBuilder();

            if (recipe == null) return "[SpritePipeline] Sin receta de enemigo.";

            if (recipe.art != null) log.Append(RunSheet(recipe.art));
            else log.AppendLine("[SpritePipeline] La ficha no tiene hoja de arte; sólo se crea el prefab.");

            EnemyFactory.Generate(recipe, log, resetTuning);
            return log.ToString();
        }

        // ============================================================ menús

        [MenuItem("Tools/RedMagic/Pipeline/2 · Cortar hoja + animar (receta seleccionada)")]
        public static void SliceSelected()
        {
            var recipe = Selection.activeObject as SpriteSheetRecipe;
            if (recipe == null)
            {
                EditorUtility.DisplayDialog("Pipeline",
                    "Selecciona un asset de tipo Sprite Sheet Recipe en el Project.", "Vale");
                return;
            }

            Debug.Log(RunSheet(recipe));
        }

        [MenuItem("Tools/RedMagic/Pipeline/3 · Generar enemigo (ficha seleccionada)")]
        public static void EnemySelected()
        {
            var recipe = Selection.activeObject as EnemyRecipe;
            if (recipe == null)
            {
                EditorUtility.DisplayDialog("Pipeline",
                    "Selecciona un asset de tipo Enemy Recipe en el Project.", "Vale");
                return;
            }

            Debug.Log(RunEnemy(recipe));
        }

        [MenuItem("Tools/RedMagic/Pipeline/3b · Generar enemigo RESETEANDO valores (ficha seleccionada)")]
        public static void EnemySelectedReset()
        {
            var recipe = Selection.activeObject as EnemyRecipe;
            if (recipe == null) return;

            if (!EditorUtility.DisplayDialog("Resetear valores",
                    $"Se sobrescribirá el EnemyStats de '{recipe.enemyName}' con los valores de la " +
                    $"ficha. Lo que hayas afinado a mano en el prefab se pierde.", "Resetear", "Cancelar"))
                return;

            Debug.Log(RunEnemy(recipe, resetTuning: true));
        }

        [MenuItem("Tools/RedMagic/Pipeline/3b · Generar enemigo RESETEANDO valores (ficha seleccionada)", true)]
        private static bool EnemySelectedResetValidate() => Selection.activeObject is EnemyRecipe;

        [MenuItem("Tools/RedMagic/Pipeline/2 · Cortar hoja + animar (receta seleccionada)", true)]
        private static bool SliceSelectedValidate() => Selection.activeObject is SpriteSheetRecipe;

        [MenuItem("Tools/RedMagic/Pipeline/3 · Generar enemigo (ficha seleccionada)", true)]
        private static bool EnemySelectedValidate() => Selection.activeObject is EnemyRecipe;
    }
}
