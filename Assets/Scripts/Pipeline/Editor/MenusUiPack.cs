using RedMagic.UI;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del kit de arte de los <b>menús de pantalla completa</b> (principal, pausa,
    /// opciones). Mismo molde que <see cref="ItemsUiPack"/>: escribe la receta
    /// (<c>Menus.uikit.asset</c>), la procesa con <see cref="UiArtKitProcessor"/> y cablea los
    /// sprites en <c>Resources/MenuSkin.asset</c>.
    ///
    /// Re-ejecutable: los PNG y las referencias se regeneran; la receta y los números del skin sólo
    /// se escriben si no existen.
    /// </summary>
    public static class MenusUiPack
    {
        private const string SourceFolder = "Assets/Ui/UiSprites";
        private const string OutputFolder = "Assets/Art/UI/Menus";
        private const string RecipePath = OutputFolder + "/Menus.uikit.asset";
        private const string SkinPath = "Assets/Resources/" + MenuSkin.ResourcePath + ".asset";

        /// <summary>
        /// Los tres estados del botón son el mismo dibujo, así que comparten <b>recorte</b>
        /// (<c>trimGroup</c>) y <b>rectángulo interior</b>: si cada uno se midiera por su cuenta, el
        /// halo del hover encogería su interior y el botón daría un salto al pasar el ratón.
        /// </summary>
        private const int ButtonTrimGroup = 1;

        [MenuItem("Tools/RedMagic/UI/Menús · Procesar kit de arte")]
        public static void Build() => Debug.Log(Run());

        [MenuItem("Tools/RedMagic/UI/Menús · Procesar kit RESETEANDO números del skin")]
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
            return "[MenusUiPack]\n" + result.Log + wired;
        }

        // ============================================================ receta

        private static UiArtKitRecipe Recipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<UiArtKitRecipe>(RecipePath);
            if (recipe != null) return WithInteriors(recipe);

            recipe = ScriptableObject.CreateInstance<UiArtKitRecipe>();
            recipe.outputFolder = OutputFolder;
            recipe.prefix = "Menus_";
            recipe.backgroundTolerance = 0.12f;   // JPG sobre blanco
            recipe.margin = 2;

            recipe.pieces = new[]
            {
                new UiArtPiece { name = "Window", source = Src("Sample.jpeg"), slice = UiSliceMode.Quad, softEdge = 6 },
                new UiArtPiece { name = "Panel", source = Src("Container.jpeg"), slice = UiSliceMode.Quad, softEdge = 6 },
                new UiArtPiece { name = "PanelTall", source = Src("VerticalContainer.jpeg"), slice = UiSliceMode.Quad, softEdge = 6 },
                new UiArtPiece { name = "TitleBar", source = Src("TitleContainer.jpeg"), slice = UiSliceMode.Quad, softEdge = 6 },

                // El hover lleva un halo turquesa que se sale del marco: franja suave ancha para
                // que se desvanezca en vez de cortarse a tijera.
                new UiArtPiece { name = "Button", source = Src("Unpresedbutton.jpeg"), slice = UiSliceMode.Quad, softEdge = 6, trimGroup = ButtonTrimGroup },
                new UiArtPiece { name = "Button_Hover", source = Src("HoverButton.jpeg"), slice = UiSliceMode.Quad, softEdge = 64, trimGroup = ButtonTrimGroup },
                new UiArtPiece { name = "Button_Pressed", source = Src("PressedButton.jpeg"), slice = UiSliceMode.Quad, softEdge = 6, trimGroup = ButtonTrimGroup },

                // 2×1: normal / hover (el hover lleva halo rojo).
                new UiArtPiece
                {
                    name = "Close", source = Src("CloseButtonWithHover.jpeg"), columns = 2, rows = 1,
                    cellNames = new[] { "Close", "Close_Hover" }, softEdge = 64,
                },

                // 2×2: columna izquierda apagada, derecha encendida; fila 1 normal, fila 2 hover.
                new UiArtPiece
                {
                    name = "Check", source = Src("checkBox.jpeg"), columns = 2, rows = 2,
                    cellNames = new[] { "Check_Off", "Check_On", "Check_Off_Hover", "Check_On_Hover" },
                    softEdge = 48,
                },
            };

            AssetDatabase.CreateAsset(recipe, RecipePath);
            return WithInteriors(recipe);
        }

        /// <summary>
        /// Panel interior de cada marco Quad, en px de la imagen fuente (x, y desde arriba). Medido
        /// rellenando el interior liso desde el centro con tolerancia de color y quedándose con su
        /// caja: los márgenes salen simétricos en las cuatro piezas, que es la comprobación de que
        /// el relleno se paró en el bisel y no se coló en la piedra del marco.
        /// </summary>
        private static readonly (string piece, RectInt interior)[] Interiors =
        {
            ("Window", new RectInt(248, 244, 519, 522)),
            ("Panel", new RectInt(216, 209, 766, 482)),
            ("PanelTall", new RectInt(217, 233, 458, 724)),
            ("TitleBar", new RectInt(267, 238, 842, 295)),

            // Los tres estados comparten el interior del normal a propósito (ver ButtonTrimGroup).
            ("Button", new RectInt(269, 236, 834, 297)),
            ("Button_Hover", new RectInt(269, 236, 834, 297)),
            ("Button_Pressed", new RectInt(269, 236, 834, 297)),
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
            var skin = AssetDatabase.LoadAssetAtPath<MenuSkin>(SkinPath);
            bool created = skin == null;
            if (created)
            {
                skin = ScriptableObject.CreateInstance<MenuSkin>();

                // REGLA: el relleno de cada marco tiene que ser MAYOR que el grosor de su piedra,
                // o el texto se mete dentro del bisel. La piedra mide, en px de pantalla,
                // `fill.rect.x * escala` (y lo propio arriba/abajo) — no se estima a ojo: se saca
                // con la cuenta de UiFrame.Fit para el tamaño real que tendrá el elemento. Los
                // números de abajo dejan ~12-15 px de aire por encima de esa piedra.
                skin.window.maxScale = 0.26f;
                skin.window.padding = new UiInsets(58, 58, 56, 56);

                // Panel de opciones (≈992×478): piedra ≈ 45 a escala 0.24. Se le da bastante más
                // que eso porque aquí el texto empieza en el borde mismo (las etiquetas Master /
                // Música / Efectos) y pegado al bisel se lee fatal.
                skin.panel.maxScale = 0.24f;
                skin.panel.padding = new UiInsets(72, 72, 52, 50);

                // Panel de pausa (≈560×560): piedra ≈ 33 a escala 0.30.
                skin.panelTall.maxScale = 0.30f;
                skin.panelTall.padding = new UiInsets(54, 54, 58, 58);

                // Placa de título: la piedra son ~27 px a los lados y ~22 arriba y abajo.
                skin.titleBar.maxScale = 0.14f;
                skin.titleBar.padding = new UiInsets(44, 44, 26, 26);

                // Celda de mejora (210×180): mismo dibujo que la placa, pero la piedra tiene que
                // ser fina o no caben las cuatro líneas (título, descripción, nivel, coste). A
                // escala 0.14 mide 27×22 y deja 156×134 de hueco; con este relleno, 142×120.
                skin.card.maxScale = 0.14f;
                skin.card.padding = new UiInsets(34, 34, 30, 30);

                // El botón es pequeño en pantalla: escala baja o la piedra se come el texto.
                foreach (var b in new[] { skin.button, skin.buttonHover, skin.buttonPressed })
                {
                    b.maxScale = 0.09f;
                    b.padding = new UiInsets(30, 30, 22, 22);
                }
            }

            skin.window.quads = r.Quads("Window");
            // La celda comparte dibujo con la placa de título; lo que cambia son sus números.
            skin.card.quads = r.Quads("TitleBar");
            skin.panel.quads = r.Quads("Panel");
            skin.panelTall.quads = r.Quads("PanelTall");
            skin.titleBar.quads = r.Quads("TitleBar");
            skin.button.quads = r.Quads("Button");
            skin.buttonHover.quads = r.Quads("Button_Hover");
            skin.buttonPressed.quads = r.Quads("Button_Pressed");

            skin.window.fill = r.Fill("Window");
            skin.card.fill = r.Fill("TitleBar");
            skin.panel.fill = r.Fill("Panel");
            skin.panelTall.fill = r.Fill("PanelTall");
            skin.titleBar.fill = r.Fill("TitleBar");
            skin.button.fill = r.Fill("Button");
            skin.buttonHover.fill = r.Fill("Button_Hover");
            skin.buttonPressed.fill = r.Fill("Button_Pressed");

            skin.closeButton.normal = r.Single("Close");
            skin.closeButton.hover = r.Single("Close_Hover");
            skin.checkOff.normal = r.Single("Check_Off");
            skin.checkOff.hover = r.Single("Check_Off_Hover");
            skin.checkOn.normal = r.Single("Check_On");
            skin.checkOn.hover = r.Single("Check_On_Hover");

            if (created) AssetDatabase.CreateAsset(skin, SkinPath);
            else EditorUtility.SetDirty(skin);

            string missing = "";
            if (!skin.window.IsSet || !skin.panel.IsSet || !skin.panelTall.IsSet ||
                !skin.titleBar.IsSet || !skin.card.IsSet || !skin.button.IsSet ||
                !skin.buttonHover.IsSet || !skin.buttonPressed.IsSet || !skin.closeButton.IsSet ||
                !skin.checkOff.IsSet || !skin.checkOn.IsSet)
                missing = "\n⚠ Faltan sprites en el skin (¿primera importación?): vuelve a ejecutar el pack.";

            return $"{(created ? "Creado" : "Actualizado")} {SkinPath}.{missing}";
        }
    }
}
