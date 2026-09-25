using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Iconos de items: quita el fondo de las imágenes de <c>Assets/Art/Icons/</c> con el mismo procesador
    /// que el arte de UI (<see cref="UiArtKitProcessor"/>, receta <c>ItemIcons.uikit.asset</c>) y los
    /// pone en los items que aún no tengan icono. Los items del
    /// set de hielo en sí los crea <c>IceSetPack</c>.
    ///
    /// Re-ejecutable: los PNG se regeneran; los items y la receta sólo se crean si faltan, y un icono
    /// ya asignado a mano no se toca. Un icono nuevo = imagen en <c>Assets/Art/Icons/</c> + una pieza más
    /// en la receta (Inspector) + ejecutar esto; luego se asigna en el item o en
    /// <c>Tools ▸ RedMagic ▸ Items ▸ Catálogo</c>.
    /// </summary>
    public static class ItemIconsPack
    {
        private const string SourceFolder = "Assets/Icon";
        private const string OutputFolder = "Assets/Art/UI/ItemIcons";
        private const string RecipePath = OutputFolder + "/ItemIcons.uikit.asset";
        private const string ItemsFolder = "Assets/Resources/Items";

        [MenuItem("Tools/RedMagic/Items/Iconos · Procesar e instalar")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(OutputFolder);

            var result = UiArtKitProcessor.Run(Recipe());
            var log = new System.Text.StringBuilder("[ItemIconsPack]\n").Append(result.Log);

            // Los items del set de hielo los crea y configura IceSetPack (Items ▸ Set de hielo ·
            // Generar); aquí sólo se les pone el icono procesado si aún no tienen uno.
            Icon(log, "Item_BotasRunicas", result.Single("Boots"));
            Icon(log, "Item_CapaEscarcha", result.Single("Cape"));
            Icon(log, "Item_YelmoRunico", result.Single("Helmet"));
            Icon(log, "Item_BastonHelado", result.Single("Staff"));
            Icon(log, "Item_AnilloGlacial", result.Single("Ring"));

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static UiArtKitRecipe Recipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<UiArtKitRecipe>(RecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<UiArtKitRecipe>();
            recipe.outputFolder = OutputFolder;
            recipe.prefix = "Icon_";
            recipe.backgroundTolerance = 0.12f;   // JPG sobre blanco
            recipe.margin = 4;

            // Halos de brillo (yelmo, capa, cristal del bastón): franja suave ancha. El anillo tiene
            // su hueco blanco encerrado: keyEnclosed (por defecto) lo quita.
            recipe.pieces = new[]
            {
                Piece("Boots"), Piece("Cape"), Piece("Helmet"), Piece("Ring"), Piece("Staff"),
            };

            AssetDatabase.CreateAsset(recipe, RecipePath);
            return recipe;

            static UiArtPiece Piece(string name) => new()
            {
                name = name,
                source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SourceFolder}/{name}.jpeg"),
                softEdge = 48,
            };
        }

        /// <summary>Pone el icono al item si existe y no tiene. Lo asignado a mano no se toca.</summary>
        private static void Icon(System.Text.StringBuilder log, string file, Sprite icon)
        {
            string path = $"{ItemsFolder}/{file}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                log.AppendLine($"Falta {path} (lo crea Tools ▸ RedMagic ▸ Items ▸ Set de hielo · Generar)");
                return;
            }

            var so = new SerializedObject(item);
            var iconProp = so.FindProperty("icon");
            if (iconProp.objectReferenceValue != null || icon == null)
            {
                log.AppendLine($"{path}: icono ya asignado");
                return;
            }

            iconProp.objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"{path}: icono asignado");
        }
    }
}
