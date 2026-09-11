using RedMagic.UI;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del kit de arte de la <b>pantalla de items</b>. Sólo datos, como los packs de enemigos:
    /// escribe la receta (<c>ItemsUi.uikit.asset</c>), la procesa con <see cref="UiArtKitProcessor"/> y
    /// cablea los sprites en <c>Resources/ItemMenuSkin.asset</c>, que es lo que lee
    /// <see cref="ItemMenuController"/>.
    ///
    /// Re-ejecutable: los PNG y las referencias de sprites se regeneran; la receta y los números del
    /// skin (escalas, rellenos, tamaños) sólo se escriben si no existen, así que lo ajustado a mano se
    /// queda.
    /// </summary>
    public static class ItemsUiPack
    {
        private const string SourceFolder = "Assets/Ui/ItemsUi";
        private const string OutputFolder = "Assets/Art/UI/ItemsUi";
        private const string RecipePath = OutputFolder + "/ItemsUi.uikit.asset";
        private const string SkinPath = "Assets/Resources/" + ItemMenuSkin.ResourcePath + ".asset";

        [MenuItem("Tools/RedMagic/UI/Items · Procesar kit de arte")]
        public static void Build() => Debug.Log(Run());

        [MenuItem("Tools/RedMagic/UI/Items · Procesar kit RESETEANDO números del skin")]
        public static void BuildResetting() => Debug.Log(RunResettingSkin());

        /// <summary>Como <see cref="Run"/>, pero borra el skin antes: vuelve a los números por defecto.</summary>
        public static string RunResettingSkin()
        {
            AssetDatabase.DeleteAsset(SkinPath);
            return Run();
        }

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(OutputFolder);

            var recipe = Recipe();
            var result = UiArtKitProcessor.Run(recipe);
            string wired = WireSkin(result);

            AssetDatabase.SaveAssets();
            return "[ItemsUiPack]\n" + result.Log + wired;
        }

        // ============================================================ receta

        private static UiArtKitRecipe Recipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<UiArtKitRecipe>(RecipePath);
            if (recipe != null) return WithInteriors(recipe);

            recipe = ScriptableObject.CreateInstance<UiArtKitRecipe>();
            recipe.outputFolder = OutputFolder;
            recipe.prefix = "ItemsUi_";
            recipe.backgroundTolerance = 0.12f;   // JPG sobre blanco
            recipe.margin = 2;

            recipe.pieces = new[]
            {
                // 2×2: columna izquierda normal, derecha hover; fila 1 vacío, fila 2 equipado
                // (brillo interior suave).
                new UiArtPiece
                {
                    name = "Slot", source = Src("Empty Items Slots, with hover.jpeg"), columns = 2, rows = 2,
                    cellNames = new[] { "Slot_Empty", "Slot_Empty_Hover", "Slot_Filled", "Slot_Filled_Hover" },
                    softEdge = 4,
                },

                // Normal / hover; el hover lleva halo rojo: franja suave ancha.
                new UiArtPiece
                {
                    name = "Close", source = Src("CloseButtonWithHover.jpeg"), columns = 2, rows = 1,
                    cellNames = new[] { "Close", "Close_Hover" }, softEdge = 64,
                },

                // Mismo marco en tres archivos: grupo de recorte 1 para que se superpongan exactos.
                new UiArtPiece { name = "IconSlot", source = Src("ItemSlotWithScapeForitemIcon.jpeg"), trimGroup = 1, softEdge = 4 },
                new UiArtPiece { name = "IconSlot_Hover", source = Src("ItemSlotHoverWithSpaceForItemIcon.jpeg"), trimGroup = 1, softEdge = 64 },
                new UiArtPiece { name = "WeaponSlot", source = Src("ItenSlot.jpeg"), trimGroup = 1, softEdge = 4 },

                new UiArtPiece { name = "Window", source = Src("ExteriorBorde.jpeg"), slice = UiSliceMode.Quad, softEdge = 6 },
                new UiArtPiece { name = "SidePanel", source = Src("LefftAndRightSectionForSinergiasAndDescription.jpeg"), slice = UiSliceMode.Quad },
                new UiArtPiece { name = "MiddlePanel", source = Src("MiddleSectionForItems.jpeg"), slice = UiSliceMode.Quad },
                new UiArtPiece { name = "TitleBar", source = Src("ForTitlesSinergyItemsAndDescription.jpeg"), slice = UiSliceMode.Quad },

                // El marco apaisado sin cristales (Gemini_…): filas de sinergia.
                new UiArtPiece { name = "Row", source = Src("Gemini_Generated_Image_ct623cct623cct62.jpeg"), slice = UiSliceMode.Quad },
            };

            AssetDatabase.CreateAsset(recipe, RecipePath);
            return WithInteriors(recipe);
        }

        /// <summary>
        /// Panel interior de cada marco Quad, medido una vez sobre la imagen fuente con una rejilla
        /// (px, y hacia abajo; justo dentro del bisel interior).
        /// </summary>
        private static readonly (string piece, RectInt interior)[] Interiors =
        {
            ("Window", new RectInt(280, 278, 644, 330)),
            ("SidePanel", new RectInt(214, 230, 472, 730)),
            ("MiddlePanel", new RectInt(212, 208, 776, 478)),
            ("TitleBar", new RectInt(266, 236, 842, 292)),
            ("Row", new RectInt(280, 278, 644, 338)),
        };

        /// <summary>Rellena el interior de las piezas sin medir; lo ajustado a mano en la receta se queda.</summary>
        private static UiArtKitRecipe WithInteriors(UiArtKitRecipe recipe)
        {
            bool dirty = false;
            foreach (var piece in recipe.pieces)
            foreach (var (name, rect) in Interiors)
            {
                if (piece.name != name || piece.interior.width > 0) continue;
                piece.interior = rect;
                dirty = true;
            }

            if (dirty) EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static Texture2D Src(string file) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{SourceFolder}/{file}");

        // ============================================================ skin

        private static string WireSkin(UiArtKitProcessor.Result r)
        {
            var skin = AssetDatabase.LoadAssetAtPath<ItemMenuSkin>(SkinPath);
            bool created = skin == null;
            if (created)
            {
                skin = ScriptableObject.CreateInstance<ItemMenuSkin>();

                // Escalas elegidas para que los cuatro marcos acaben con un grosor parecido en
                // pantalla: cada lámina viene a una densidad distinta.
                skin.window.maxScale = 0.36f;
                skin.window.padding = new UiInsets(56, 56, 58, 46);
                skin.sidePanel.maxScale = 0.34f;
                skin.sidePanel.padding = new UiInsets(30, 30, 26, 26);
                skin.middlePanel.maxScale = 0.18f;
                // ≥ margen del interior × escala (168 px × 0.18 ≈ 30): si no, la última fila pisa el bisel.
                skin.middlePanel.padding = new UiInsets(36, 36, 30, 36);
                skin.titleBar.maxScale = 0.12f;
                skin.titleBar.padding = new UiInsets(20, 20, 4, 4);
                skin.synergyRow.maxScale = 0.1f;
                skin.synergyRow.padding = new UiInsets(20, 20, 4, 4);
            }

            skin.window.quads = r.Quads("Window");
            skin.sidePanel.quads = r.Quads("SidePanel");
            skin.middlePanel.quads = r.Quads("MiddlePanel");
            skin.titleBar.quads = r.Quads("TitleBar");
            skin.synergyRow.quads = r.Quads("Row");

            skin.window.fill = r.Fill("Window");
            skin.sidePanel.fill = r.Fill("SidePanel");
            skin.middlePanel.fill = r.Fill("MiddlePanel");
            skin.titleBar.fill = r.Fill("TitleBar");
            skin.synergyRow.fill = r.Fill("Row");

            skin.emptySlot.normal = r.Single("Slot_Empty");
            skin.emptySlot.hover = r.Single("Slot_Empty_Hover");
            skin.filledSlot.normal = r.Single("Slot_Filled");
            skin.filledSlot.hover = r.Single("Slot_Filled_Hover");
            skin.iconSlot.normal = r.Single("IconSlot");
            skin.iconSlot.hover = r.Single("IconSlot_Hover");
            skin.weaponSlot.normal = r.Single("WeaponSlot");
            skin.weaponSlot.hover = r.Single("IconSlot_Hover");
            skin.closeButton.normal = r.Single("Close");
            skin.closeButton.hover = r.Single("Close_Hover");

            if (created) AssetDatabase.CreateAsset(skin, SkinPath);
            else EditorUtility.SetDirty(skin);

            string missing = "";
            if (!skin.window.IsSet || !skin.sidePanel.IsSet || !skin.middlePanel.IsSet ||
                !skin.titleBar.IsSet || !skin.synergyRow.IsSet || !skin.emptySlot.IsSet ||
                !skin.filledSlot.IsSet || !skin.iconSlot.IsSet || !skin.weaponSlot.IsSet ||
                !skin.closeButton.IsSet)
                missing = "\n⚠ Faltan sprites en el skin (¿primera importación?): vuelve a ejecutar el pack.";

            return $"{(created ? "Creado" : "Actualizado")} {SkinPath}.{missing}";
        }
    }
}
