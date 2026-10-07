using RedMagic.UI;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del kit de arte del <b>HUD de juego</b>. Mismo molde que <see cref="MenusUiPack"/>:
    /// escribe la receta (<c>Hud.uikit.asset</c>), la procesa con <see cref="UiArtKitProcessor"/> y
    /// cablea los sprites en <c>Resources/HudSkin.asset</c>.
    ///
    /// Diferencia con los menús: los marcos de las barras son <b>huecos</b> y se pintan DELANTE del
    /// relleno que dibuja el código (<see cref="UiFrame.Overlay"/>), que lleva su silueta (máscara).
    ///
    /// Las fuentes son las de <c>HD/</c> (ya con alfa) y cada pieza sale reducida aquí
    /// (<see cref="UiArtPiece.outputHeight"/>, promedio de área) a ~2× su alto en pantalla: la GPU
    /// reduciendo 2048 → 70 px a lo bruto es lo que pixelaba los bordes.
    ///
    /// <c>KeyCap_Wide.png</c> es un recorte de <c>KeyCap.jpeg</c> (la tecla ancha sola, x ≥ 347): las
    /// dos teclas venían pegadas y la rejilla 2×1 las habría partido. La ancha sirve para todas, porque
    /// el modo Quad la estrecha hasta sus partes fijas.
    ///
    /// Re-ejecutable: los PNG y las referencias se regeneran; la receta y los números del skin sólo
    /// se escriben si no existen.
    /// </summary>
    public static class HudUiPack
    {
        /// <summary>
        /// Las imágenes de Gemini pasadas por ComfyUI (<c>claude-workflow.mjs finish</c>: ×4 con
        /// RealESRGAN anime, a 4 MP, fondo quitado con RMBG-2.0): PNG con alfa a ~2048 px. Los JPG
        /// originales siguen en la carpeta de arriba.
        /// </summary>
        private const string SourceFolder = "Assets/Ui/HudSprites/HD";
        private const string OutputFolder = "Assets/Art/UI/Hud";
        private const string RecipePath = OutputFolder + "/Hud.uikit.asset";
        private const string SkinPath = "Assets/Resources/" + HudSkin.ResourcePath + ".asset";

        [MenuItem("Tools/RedMagic/UI/HUD · Procesar kit de arte")]
        public static void Build() => Debug.Log(Run());

        [MenuItem("Tools/RedMagic/UI/HUD · Procesar kit RESETEANDO números del skin")]
        public static void BuildResetting() => Debug.Log(RunResettingSkin());

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
            return "[HudUiPack]\n" + result.Log + wired;
        }

        // ============================================================ receta

        private static UiArtKitRecipe Recipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<UiArtKitRecipe>(RecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<UiArtKitRecipe>();
            recipe.outputFolder = OutputFolder;
            recipe.prefix = "Hud_";
            // Las fuentes ya traen alfa (RMBG), así que la tolerancia de color sólo cuenta si alguna
            // vuelve a ser un JPG sobre blanco: entonces, más estricta que la de los menús.
            recipe.backgroundTolerance = 0.16f;
            recipe.softOpaqueAt = 0.8f;
            recipe.margin = 2;

            // outputHeight ≈ 2× el alto en pantalla; maskErode en px de la fuente HD (~2× la del JPG).
            recipe.pieces = new[]
            {
                // Con máscara: el relleno se pinta con la silueta exacta del aro y no asoma por las esquinas.
                new UiArtPiece { name = "HealthFrame", source = Src("HealthBarFrame.png"), slice = UiSliceMode.Quad, softEdge = 4,
                                 emitMask = true, maskErode = 8, mipmaps = true, outputHeight = 128 },
                new UiArtPiece { name = "EnemyFrame", source = Src("EnemyBarFrame.png"), slice = UiSliceMode.Quad, softEdge = 4,
                                 emitMask = true, maskErode = 8, mipmaps = true, outputHeight = 96 },

                // 2×1: apagado / encendido.
                new UiArtPiece
                {
                    name = "Ring", source = Src("AbilitySlot.png"), columns = 2, rows = 1,
                    cellNames = new[] { "Ring", "Ring_Ready" }, softEdge = 48, mipmaps = true, outputHeight = 256,
                },

                new UiArtPiece
                {
                    name = "Icon", source = Src("AbilityIcons.png"), columns = 3, rows = 1,
                    cellNames = new[] { "Icon_Attack", "Icon_Dash", "Icon_Parry" }, softEdge = 32, keyEnclosed = false,
                    mipmaps = true, outputHeight = 160,
                },

                // Chapa y tecla: interior oscuro liso, pintado de una pieza con 'interior' (medido en el
                // JPG y pasado a la HD: ×2 la chapa, ×2.46 la tecla).
                new UiArtPiece
                {
                    name = "Plaque", source = Src("CurrencyPlaque.png"), slice = UiSliceMode.Quad, softEdge = 4,
                    keyEnclosed = false, mipmaps = true, outputHeight = 128, interior = new RectInt(270, 824, 1524, 400),
                },
                new UiArtPiece
                {
                    name = "KeyCap", source = Src("KeyCap_Wide.png"), slice = UiSliceMode.Quad, softEdge = 4,
                    keyEnclosed = false, mipmaps = true, outputHeight = 96, interior = new RectInt(74, 989, 1264, 465),
                },
            };

            AssetDatabase.CreateAsset(recipe, RecipePath);
            return recipe;
        }

        private static Texture2D Src(string file) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{SourceFolder}/{file}");

        // ============================================================ skin

        private static string WireSkin(UiArtKitProcessor.Result r)
        {
            var skin = AssetDatabase.LoadAssetAtPath<HudSkin>(SkinPath);
            bool created = skin == null;
            if (created)
            {
                skin = ScriptableObject.CreateInstance<HudSkin>();

                // Las barras: UiFrame.Fit baja la escala sola hasta que el aro cabe en el alto de la
                // barra, así que maxScale sólo pone el techo.
                // Las escalas son px de UI por px del SPRITE (ya reducido a outputHeight). Por debajo del
                // ajuste al alto, el aro sale más fino (se estira la fila lisa del hueco): la barra de vida
                // (sprite de 128) se dibuja a ~34 px de alto natural dentro de sus 46.
                skin.healthFrame.maxScale = 0.27f;
                skin.enemyFrame.maxScale = 0.42f;

                // Chapa de monedas (≈56 px de alto): el aro queda en ~7 px.
                skin.currencyPlaque.maxScale = 0.47f;
                skin.currencyPlaque.padding = new UiInsets(16, 20, 8, 8);
                skin.currencyPlaque.fillCornerRadius = 10f;

                // Tecla (≈30 px de alto): bisel inferior más grueso que el superior.
                skin.keyCap.maxScale = 0.375f;
                skin.keyCap.padding = new UiInsets(8, 8, 3, 6);
                skin.keyCap.fillCornerRadius = 9f;
            }

            skin.healthFrame.quads = r.Quads("HealthFrame");
            skin.enemyFrame.quads = r.Quads("EnemyFrame");
            skin.healthFrame.maskQuads = r.Quads("HealthFrame_Mask");
            skin.enemyFrame.maskQuads = r.Quads("EnemyFrame_Mask");
            skin.currencyPlaque.quads = r.Quads("Plaque");
            skin.currencyPlaque.fill = r.Fill("Plaque");
            skin.keyCap.quads = r.Quads("KeyCap");
            skin.keyCap.fill = r.Fill("KeyCap");

            skin.abilityRing.normal = r.Single("Ring");
            skin.abilityRing.hover = r.Single("Ring_Ready");
            skin.iconAttack = r.Single("Icon_Attack");
            skin.iconDash = r.Single("Icon_Dash");
            skin.iconParry = r.Single("Icon_Parry");

            if (created) AssetDatabase.CreateAsset(skin, SkinPath);
            else EditorUtility.SetDirty(skin);

            string missing = "";
            if (!skin.healthFrame.IsSet || !skin.enemyFrame.IsSet || !skin.healthFrame.HasMask || !skin.enemyFrame.HasMask || !skin.currencyPlaque.IsSet ||
                !skin.keyCap.IsSet || !skin.abilityRing.IsSet || skin.abilityRing.hover == null ||
                skin.iconAttack == null || skin.iconDash == null || skin.iconParry == null)
                missing = "\n⚠ Faltan sprites en el skin (¿primera importación?): vuelve a ejecutar el pack.";

            return $"{(created ? "Creado" : "Actualizado")} {SkinPath}.{missing}";
        }
    }
}
