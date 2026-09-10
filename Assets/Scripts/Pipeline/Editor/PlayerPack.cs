using System.Collections.Generic;
using System.Text;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El pack del <b>jugador</b>. Mismo patrón que los packs de enemigo — sólo datos — pero
    /// termina en el prefab que ya existe en vez de crear uno nuevo, porque el jugador lleva
    /// encima todo el control, el ataque, el inventario y el audio: aquí se cambia el arte y
    /// <b>nada más</b>.
    ///
    /// Dos decisiones que sostienen eso:
    /// <list type="bullet">
    /// <item><b>El controller se copia, no se genera.</b> El del jugador (<c>DragonWarrior</c>)
    /// tiene diez estados y transiciones afinadas a mano sobre los parámetros que escribe
    /// <see cref="PlayerAnimator"/> (Speed, VSpeed, Grounded, Crouching, Attack, Hurt, Dead). Se
    /// duplica a la carpeta del personaje y <see cref="AnimClipBuilder"/> le reescribe los clips
    /// en su sitio, así que el cableado — y por tanto el comportamiento — queda idéntico.</item>
    /// <item><b>Los estados que la lámina no dibuja se derivan.</b> La hoja no trae caída ni
    /// agachado; si se dejaran, seguirían con el arte del dragón y se vería una mezcla de dos
    /// personajes. Salen de la fila más cercana vía <see cref="SpriteSheetRecipe.derivedClips"/>.</item>
    /// </list>
    ///
    /// La lámina trae los rótulos (IDLE, WALK…) pintados en una columna a la izquierda: eso lo
    /// resuelve <c>cropLeft</c>, que los deja fuera antes de analizar nada. Sin él cuentan como
    /// contenido, se llevan un frame por delante y su color entra en la detección del fondo.
    /// </summary>
    public static class PlayerPack
    {
        private const string Sheet = "Assets/Sprites/player_sprite.jpeg";
        private const string Folder = "Assets/Art/Characters/Player";
        private const string SheetRecipePath = Folder + "/Player.sheet.asset";
        private const string ControllerPath = Folder + "/Player.controller";
        private const string SourceController = "Assets/Dragon Warrior Files/Animations/DragonWarrior.controller";
        private const string PlayerPrefab = "Assets/Prefab/Player.prefab";

        /// <summary>Recuadros del ataque cortados a mano, como Sprite sueltos.</summary>
        private const string HandCutFolder = "Assets/Sprites/New folder";

        /// <summary>
        /// Ancho de la columna de rótulos, medido con <see cref="Diagnose"/>: el texto ocupa
        /// x[15..38] y a partir de x=42 la lámina ya es sólo damero.
        /// </summary>
        private const int LabelColumnWidth = 42;

        /// <summary>
        /// Alto que debe ocupar la celda del sprite en unidades de mundo. La caja de colisión del
        /// controlador mide 1.5 de alto y el sprite lleva <c>margin</c> px de aire arriba y abajo,
        /// así que se apunta un poco por encima. El factor de escala se calcula del sprite real,
        /// no a ojo, para que un cambio de lámina no obligue a reajustar nada.
        /// </summary>
        private const float TargetSpriteHeight = 1.7f;

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Player (cambiar arte del jugador)")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            var log = new StringBuilder();

            SheetSlicer.EnsureFolder(Folder);
            var recipe = Sheet_();
            EnsureController(log);

            // Se resuelve contra el prefab real, no se da por supuesto: en el prefab del jugador
            // el Animator está en el MISMO objeto que el SpriteRenderer (el hijo 'Sprite'), no en
            // la raíz como en los enemigos generados. Con la ruta equivocada los clips animan a un
            // objeto que no existe y el personaje se queda congelado en un frame.
            recipe.rendererPath = ResolveRendererPath(log);
            AdoptHandCutAttack(recipe, log);
            EditorUtility.SetDirty(recipe);

            log.Append(SpritePipeline.RunSheet(recipe));
            WireExtraStates(log);
            DressPlayerPrefab(recipe, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        // ============================================================ receta de la lámina

        private static SpriteSheetRecipe Sheet_()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(SheetRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();

            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            recipe.characterName = "Player";
            recipe.outputFolder = Folder;

            // Bandas por contenido, frames por reparto uniforme. El reparto uniforme es
            // obligatorio aquí: DASH y CHARGE_ATK llevan estelas y estallidos que invaden el
            // frame de al lado, y la detección por manchas conexas fundiría dos poses en una.
            // No se usa modo Grid porque la rejilla de la lámina no está anclada al borde.
            recipe.sliceMode = SliceMode.AutoBounds;
            recipe.columns = 5;
            recipe.cropLeft = LabelColumnWidth;

            // Los nombres son los estados del AnimatorController del jugador, no una convención
            // nueva: es lo que hace que reescribir los clips baste y no haya que recablear nada.
            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",       frames = 5, evenSplit = true, fps = 8f,  loop = true },
                new SheetRow { state = "Walk",       frames = 5, evenSplit = true, fps = 12f, loop = true },
                new SheetRow { state = "Jump",       frames = 5, evenSplit = true, fps = 10f, loop = false },
                new SheetRow { state = "DoubleJump", frames = 5, evenSplit = true, fps = 12f, loop = false },
                new SheetRow { state = "Dash",       frames = 5, evenSplit = true, fps = 14f, loop = false },
                new SheetRow { state = "Attack",     frames = 5, evenSplit = true, fps = 12f, loop = false },
                new SheetRow { state = "Hurt",       frames = 5, evenSplit = true, fps = 14f, loop = false },
                new SheetRow { state = "Die",        frames = 5, evenSplit = true, fps = 8f,  loop = false },
            };

            recipe.derivedClips = new[]
            {
                // La lámina no dibuja caída. El cuarto frame del salto es el descenso: se congela
                // ahí, que es como se lee una caída en un plataformero.
                new DerivedClip { state = "Fall", fromState = "Jump", firstFrame = 3, frameCount = 1, fps = 10f, loop = true },

                // Tampoco hay agachado: se mantiene la primera pose del reposo.
                new DerivedClip { state = "Crouch", fromState = "Idle", firstFrame = 0, frameCount = 1, fps = 8f, loop = true },

                // Las dos variantes del ataque que el controller tiene cableadas reutilizan el
                // ataque. Sin esto se quedarían con el arte del dragón.
                new DerivedClip { state = "CrouchAttack", fromState = "Attack", fps = 12f, loop = false },
                new DerivedClip { state = "JumpAttack",   fromState = "Attack", fps = 12f, loop = false },
            };

            recipe.keyBackground = true;          // JPEG con el damero de transparencia pintado
            recipe.backgroundTolerance = 0.14f;

            recipe.anchor = AnchorMode.BottomCenter;   // pivote a los pies, como el prefab espera
            recipe.pixelsPerUnit = 100;
            recipe.filterMode = FilterMode.Bilinear;
            recipe.margin = 6;

            recipe.runtime = AnimRuntime.Animator;

            // El jugador no lleva EnemyAnimation: sin esto Unity avisaría en cada ataque de que
            // nadie escucha OnAttackRelease / OnAttackFinished.
            recipe.attackEvents = false;

            AssetDatabase.CreateAsset(recipe, SheetRecipePath);
            return recipe;
        }

        // ============================================================ controller

        /// <summary>
        /// Copia el controller del dragón a la carpeta del jugador la primera vez. A partir de
        /// ahí es el del jugador y <see cref="AnimClipBuilder.BuildController"/> lo actualiza en
        /// su sitio, conservando transiciones y parámetros.
        /// </summary>
        private static void EnsureController(StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                log.AppendLine($"[PlayerPack] controller ya existe: {ControllerPath}");
                return;
            }

            if (!AssetDatabase.CopyAsset(SourceController, ControllerPath))
            {
                log.AppendLine($"[PlayerPack] ERROR: no se pudo copiar '{SourceController}'.");
                return;
            }

            AssetDatabase.ImportAsset(ControllerPath);
            log.AppendLine($"[PlayerPack] controller copiado de DragonWarrior → {ControllerPath}");
        }

        /// <summary>
        /// Ruta del <see cref="SpriteRenderer"/> vista desde el objeto que lleva el
        /// <see cref="Animator"/>, leída del prefab. Vacía si son el mismo objeto.
        /// </summary>
        private static string ResolveRendererPath(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null) return AnimClipBuilder.RendererPath;

            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                var renderer = root.GetComponentInChildren<SpriteRenderer>(true);

                if (animator == null || renderer == null)
                {
                    log.AppendLine("[PlayerPack] AVISO: el prefab no tiene Animator o SpriteRenderer.");
                    return AnimClipBuilder.RendererPath;
                }

                string path = AnimationUtility.CalculateTransformPath(renderer.transform, animator.transform);
                log.AppendLine($"[PlayerPack] Animator en '{animator.name}', sprite en " +
                               $"'{renderer.name}' → ruta de animación '{path}'.");
                return path;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ============================================================ ataque cortado a mano

        /// <summary>
        /// La fila del ataque es la única que ninguna heurística acierta: el aura de energía crece
        /// tanto que invade los frames vecinos, así que el reparto uniforme partía dos poses por la
        /// mitad. Los cinco recuadros están puestos a mano como <see cref="Sprite"/> en
        /// <c>Assets/Sprites/New folder/</c>; aquí se copian a la receta.
        ///
        /// Se copian, no se leen en cada corte: una vez escritos, la receta es autosuficiente y los
        /// assets sueltos se pueden borrar. Si se retocan a mano, volver a lanzar el pack los
        /// vuelve a adoptar.
        /// </summary>
        private static void AdoptHandCutAttack(SpriteSheetRecipe recipe, StringBuilder log)
        {
            var rects = new List<RectInt>();

            for (int i = 1; i <= 5; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{HandCutFolder}/attack{i}.asset");
                if (sprite == null) break;

                var r = sprite.rect;
                rects.Add(new RectInt(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y),
                                      Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height)));
            }

            var row = System.Array.Find(recipe.rows, candidate => candidate.state == "Attack");
            if (row == null) return;

            if (rects.Count == 0)
            {
                log.AppendLine(row.frameRects != null && row.frameRects.Length > 0
                    ? "[PlayerPack] ataque: se usan los recuadros ya guardados en la receta."
                    : "[PlayerPack] AVISO: no hay recuadros a mano del ataque ni en la receta.");
                return;
            }

            row.frameRects = rects.ToArray();
            row.frames = rects.Count;
            log.AppendLine($"[PlayerPack] ataque: adoptados {rects.Count} recuadros cortados a mano " +
                           $"de {HandCutFolder}.");
        }

        // ============================================================ estados propios del jugador

        /// <summary>
        /// Cablea los dos estados que la lámina trae y el controller del dragón no tenía: el dash
        /// y el salto en el aire. <see cref="AnimClipBuilder"/> ya los creó como estados sueltos
        /// (crea el estado, no las transiciones, porque su cableado por defecto es el de un
        /// enemigo); aquí se les da entrada y salida con los parámetros que escribe
        /// <see cref="PlayerAnimator"/>.
        ///
        /// Idempotente: si la transición ya existe no se duplica, así que se puede afinar a mano
        /// en la ventana del Animator y relanzar el pack.
        /// </summary>
        private static void WireExtraStates(StringBuilder log)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) return;

            EnsureParameter(controller, "Dashing", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "DoubleJump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;
            AnimatorState dash = null, doubleJump = null, fallback = null, fall = null;

            foreach (var child in machine.states)
            {
                switch (child.state.name)
                {
                    case "Dash": dash = child.state; break;
                    case "DoubleJump": doubleJump = child.state; break;
                    case "Fall": fall = child.state; break;
                    case "Idle": fallback = child.state; break;
                }
            }

            var exit = fall ?? fallback;
            if (exit == null) return;

            // El dash manda mientras dura: entra desde AnyState y sale en cuanto se apaga el bool.
            if (dash != null && !HasAnyStateTransition(machine, dash))
            {
                var enter = machine.AddAnyStateTransition(dash);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Dashing");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.hasExitTime = false;
                enter.duration = 0.02f;
                enter.canTransitionToSelf = false;

                var leave = dash.AddTransition(exit);
                leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dashing");
                leave.hasExitTime = false;
                leave.duration = 0.05f;

                log.AppendLine("[PlayerPack] Dash cableado (AnyState ← Dashing, salida al soltarlo).");
            }

            // El salto en el aire es un disparo puntual: se reproduce entero y devuelve a la caída.
            if (doubleJump != null && !HasAnyStateTransition(machine, doubleJump))
            {
                var enter = machine.AddAnyStateTransition(doubleJump);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "DoubleJump");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.hasExitTime = false;
                enter.duration = 0.02f;
                enter.canTransitionToSelf = false;

                var leave = doubleJump.AddTransition(exit);
                leave.hasExitTime = true;
                leave.exitTime = 1f;
                leave.duration = 0.05f;

                log.AppendLine("[PlayerPack] DoubleJump cableado (AnyState ← trigger, salida por tiempo).");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static bool HasAnyStateTransition(AnimatorStateMachine machine, AnimatorState state)
        {
            foreach (var transition in machine.anyStateTransitions)
                if (transition.destinationState == state) return true;

            return false;
        }

        private static void EnsureParameter(AnimatorController controller, string name,
                                            AnimatorControllerParameterType type)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name) return;

            controller.AddParameter(name, type);
        }

        // ============================================================ prefab

        /// <summary>
        /// Apunta el prefab del jugador al controller nuevo, le pone el sprite de reposo y ajusta
        /// la escala del hijo <c>Sprite</c> para que el personaje mida lo mismo que antes. No toca
        /// ningún otro componente: el movimiento, el ataque, la vida y el audio quedan como estaban.
        /// </summary>
        private static void DressPlayerPrefab(SpriteSheetRecipe recipe, StringBuilder log)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var idle = FirstSprite(recipe, "Idle");

            if (controller == null || idle == null)
            {
                log.AppendLine("[PlayerPack] ERROR: falta el controller o el sprite de reposo.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null)
            {
                log.AppendLine($"[PlayerPack] ERROR: no se pudo abrir '{PlayerPrefab}'.");
                return;
            }

            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null) animator.runtimeAnimatorController = controller;

                var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.sprite = idle;
                    renderer.color = Color.white;

                    float height = idle.bounds.size.y;
                    float scale = height > 0.0001f ? TargetSpriteHeight / height : 1f;
                    renderer.transform.localScale = new Vector3(scale, scale, 1f);

                    log.AppendLine($"[PlayerPack] sprite {height:F2}u → escala {scale:F3} " +
                                   $"(objetivo {TargetSpriteHeight}u).");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                log.AppendLine($"[PlayerPack] prefab actualizado: {PlayerPrefab}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite FirstSprite(SpriteSheetRecipe recipe, string state)
        {
            string path = $"{recipe.ResolvedFolder}/{recipe.characterName}_{state}.png";
            string wanted = SheetSlicer.SpriteName(recipe.characterName, state, 0);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite s && s.name == wanted) return s;

            return null;
        }

        // ============================================================ diagnóstico

        /// <summary>
        /// Mide la lámina antes de cortarla: tamaño, y qué ancho ocupa la columna de rótulos de la
        /// izquierda. Es de donde sale <see cref="LabelColumnWidth"/> — el recorte se mide, no se
        /// estima. Vuelve a lanzarlo si llega una versión nueva de la hoja.
        /// </summary>
        [MenuItem("Tools/RedMagic/Pipeline/Packs/Player · Diagnosticar lámina")]
        public static void DiagnoseMenu() => Debug.Log(Diagnose());

        public static string Diagnose()
        {
            var log = new StringBuilder();
            var tex = SheetSlicer.LoadRaw(Sheet);

            if (tex == null) return $"[PlayerPack] No se pudo leer '{Sheet}'.";

            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            log.AppendLine($"[PlayerPack] lámina {w}x{h}, {h / 8f:F1} px por fila.");

            // La columna de rótulos es la única franja oscura que recorre la lámina de arriba
            // abajo: el damero de fondo es claro y los personajes no tocan todas las filas.
            int probe = 0;
            for (int x = 0; x < Mathf.Min(w, 200); x++)
            {
                int dark = 0;
                for (int y = 0; y < h; y++)
                {
                    var c = px[y * w + x];
                    if ((c.r + c.g + c.b) / 3 < 90) dark++;
                }

                bool spans = dark > h * 0.2f;
                if (spans) probe = x + 1;
                if (x < 60 || spans) log.AppendLine($"  x={x,3}  oscuro {dark * 100f / h,5:F1}%{(spans ? "  ← rótulo" : "")}");
            }

            log.AppendLine($"[PlayerPack] la columna de rótulos llega hasta x={probe - 1} " +
                           $"→ cropLeft ≈ {probe + 2}. Ahora mismo la receta usa {LabelColumnWidth}.");

            return log.ToString();
        }
    }
}
