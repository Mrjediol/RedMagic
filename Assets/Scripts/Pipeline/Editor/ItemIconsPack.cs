using RedMagic.Economy;
using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Iconos de items: quita el fondo de las imágenes de <c>Assets/Art/Icons/</c> con el mismo procesador
    /// que el arte de UI (<see cref="UiArtKitProcessor"/>, receta <c>ItemIcons.uikit.asset</c>) y los
    /// pone en los items que aún no tengan icono. Además crea los items de prueba de hielo (botas,
    /// capa, yelmo, anillo, bastón) con efectos exagerados para comprobar el sistema de efectos.
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

        private static readonly Color IceAccent = new(0.55f, 0.85f, 0.95f);

        [MenuItem("Tools/RedMagic/Items/Iconos · Procesar e instalar")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(OutputFolder);

            var result = UiArtKitProcessor.Run(Recipe());
            var log = new System.Text.StringBuilder("[ItemIconsPack]\n").Append(result.Log);

            Item(log, "Item_BotasRunicas", "Botas Rúnicas", result.Single("Boots"),
                 "Botas de escarcha que apenas rozan el suelo. (Prueba: triplican la velocidad.)",
                 BuildTag.Ice, BuildTag.Haste,
                 new PlayerStatMultiplierEffect { stat = PlayerStat.MoveSpeed, multiplier = 3f });

            Item(log, "Item_CapaEscarcha", "Capa de Escarcha", result.Single("Cape"),
                 "Una ráfaga helada te sigue al esquivar. (Prueba: dash el triple de largo.)",
                 BuildTag.Ice, BuildTag.Reset,
                 new PlayerStatMultiplierEffect { stat = PlayerStat.DashDistance, multiplier = 3f });

            Item(log, "Item_YelmoRunico", "Yelmo Rúnico", result.Single("Helmet"),
                 "Las runas del yelmo tiran de ti hacia arriba. (Prueba: saltas el triple.)",
                 BuildTag.Ice, BuildTag.Tank,
                 new PlayerStatMultiplierEffect { stat = PlayerStat.JumpHeight, multiplier = 3f });

            Item(log, "Item_BastonHelado", "Bastón Helado", result.Single("Staff"),
                 "El cristal bebe de quien lo empuña. (Prueba: pierdes 1 de vida por segundo.)",
                 BuildTag.Ice, BuildTag.Lifesteal,
                 new DrainHealthEffect { amount = 1f, interval = 1f, canKill = false });

            Item(log, "Item_AnilloGlacial", "Anillo Glacial", result.Single("Ring"),
                 "La gema se escarcha de diamantes. (Prueba: +1 diamante por segundo.)",
                 BuildTag.Ice, BuildTag.Reset,
                 new GrantCurrencyEffect { currency = Currency.Diamond, amount = 1, interval = 1f });

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

        /// <summary>Crea el item si falta y le pone el icono si no tiene. Nada más: lo ajustado a mano se queda.</summary>
        private static void Item(System.Text.StringBuilder log, string file, string displayName, Sprite icon,
                                 string description, BuildTag elemental, BuildTag universal, params ItemEffect[] effects)
        {
            string path = $"{ItemsFolder}/{file}.asset";
            var item = AssetDatabase.LoadAssetAtPath<FreePoolItemDefinition>(path);
            bool created = item == null;

            if (created)
            {
                item = ScriptableObject.CreateInstance<FreePoolItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            if (created)
            {
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("description").stringValue = description;
                so.FindProperty("accent").colorValue = IceAccent;

                var tags = so.FindProperty("tags");
                tags.arraySize = 2;
                tags.GetArrayElementAtIndex(0).intValue = (int)elemental;
                tags.GetArrayElementAtIndex(1).intValue = (int)universal;

                var list = so.FindProperty("effects");
                list.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++)
                    list.GetArrayElementAtIndex(i).managedReferenceValue = effects[i];
            }

            var iconProp = so.FindProperty("icon");
            bool iconSet = false;
            if (iconProp.objectReferenceValue == null && icon != null)
            {
                iconProp.objectReferenceValue = icon;
                iconSet = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"{(created ? "Creado" : "Ya existía")} {path}{(iconSet ? " · icono asignado" : "")}");
        }
    }
}
