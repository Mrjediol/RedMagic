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
    /// Diferencia con los menús: los marcos de las barras son <b>huecos</b> (su interior blanco se
    /// quita con <c>keyEnclosed</c>) y se pintan DELANTE del relleno que dibuja el código
    /// (<see cref="UiFrame.Overlay"/>), no detrás del contenido.
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
        private const string SourceFolder = "Assets/Ui/HudSprites";
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
            // Más estricto que los menús: estas piezas se ven muy reducidas y el halo semiblanco del JPG,
            // si se vuelve opaco, sale como píxeles blancos sueltos en el borde.
            recipe.backgroundTolerance = 0.16f;
            recipe.softOpaqueAt = 0.8f;
            recipe.margin = 2;

            recipe.pieces = new[]
            {
                // Aros huecos: el interior blanco se quita (keyEnclosed) y deja ver el relleno.
                // Con máscara: el relleno se pinta con la silueta exacta del aro y no asoma por las esquinas.
                new UiArtPiece { name = "HealthFrame", source = Src("HealthBarFrame.jpeg"), slice = UiSliceMode.Quad, softEdge = 4, emitMask = true, mipmaps = true },
                // Franja suave fina: una ancha (para el brillo de las muescas) dejaba el aro entero
                // semitransparente y de color irregular.
                new UiArtPiece { name = "EnemyFrame", source = Src("EnemyBarFrame.jpeg"), slice = UiSliceMode.Quad, softEdge = 4, emitMask = true, mipmaps = true },

                // 2×1: apagado / encendido (halo cian: franja ancha para que se desvanezca).
                new UiArtPiece
                {
                    name = "Ring", source = Src("AbilitySlot.jpeg"), columns = 2, rows = 1,
                    cellNames = new[] { "Ring", "Ring_Ready" }, softEdge = 48, mipmaps = true,
                },

                // 3×1, sin huecos que quitar; los brillos se desvanecen.
                new UiArtPiece
                {
                    name = "Icon", source = Src("AbilityIcons.jpeg"), columns = 3, rows = 1,
                    cellNames = new[] { "Icon_Attack", "Icon_Dash", "Icon_Parry" }, softEdge = 32, keyEnclosed = false, mipmaps = true,
                },

                // Chapa y tecla: interior oscuro liso (no hueco), pintado de una pieza con `interior`.
                new UiArtPiece
                {
                    name = "Plaque", source = Src("CurrencyPlaque.jpeg"), slice = UiSliceMode.Quad, softEdge = 4,
                    keyEnclosed = false, mipmaps = true, interior = new RectInt(135, 412, 762, 200),
                },
                new UiArtPiece
                {
                    name = "KeyCap", source = Src("KeyCap_Wide.png"), slice = UiSliceMode.Quad, softEdge = 4,
                    keyEnclosed = false, mipmaps = true, interior = new RectInt(30, 402, 514, 189),
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
                // Por debajo del ajuste al alto el aro sale más fino (se estira la fila lisa del hueco).
                skin.healthFrame.maxScale = 0.17f;
                skin.enemyFrame.maxScale = 0.2f;

                // Chapa de monedas (≈56 px de alto): el aro mide ~32 px de fuente → ~7 px en pantalla.
                skin.currencyPlaque.maxScale = 0.24f;
                skin.currencyPlaque.padding = new UiInsets(16, 20, 8, 8);
                skin.currencyPlaque.fillCornerRadius = 20f;

                // Tecla (≈30 px de alto): bisel inferior más grueso que el superior.
                skin.keyCap.maxScale = 0.14f;
                skin.keyCap.padding = new UiInsets(8, 8, 3, 6);
                skin.keyCap.fillCornerRadius = 24f;
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
