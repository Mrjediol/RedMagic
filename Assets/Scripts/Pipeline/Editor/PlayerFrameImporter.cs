using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Reconstruye los clips del jugador a partir de las carpetas ya cortadas
    /// <c>Assets/Art/Characters/Player/Animations/&lt;Estado&gt;/frame_###.png</c> (el mismo formato
    /// manifest.json + una carpeta de frames por animación que ya consume <c>EnemyImporter</c> para
    /// enemigos, aquí adaptado para escribir DENTRO de los <c>.anim</c> ya existentes del jugador en
    /// vez de generar un AnimatorController nuevo — el del jugador tiene diez años de transiciones
    /// afinadas a mano (<c>Player.controller</c>) que hay que conservar intactas).
    ///
    /// Reutilizable: la próxima vez que se sustituya el arte del jugador por una lámina ya cortada en
    /// carpetas de este mismo formato, este es el punto de entrada — no hay que volver a tocar los
    /// clips a mano ni redescubrir el binding path.
    /// </summary>
    public static class PlayerFrameImporter
    {
        private const string Folder = "Assets/Art/Characters/Player";
        private const string FramesRoot = Folder + "/Animations";
        private const string AnimFolder = Folder + "/Anim";
        private const string ControllerPath = Folder + "/Player.controller";

        private const float PixelsPerUnit = 100f;
        private const FilterMode Filter = FilterMode.Bilinear;

        /// <summary>Un renglón de trabajo: carpeta de frames → clip existente a reescribir.</summary>
        private class StateJob
        {
            public string Folder;
            public string ClipPath;
            public float Fps;
            public bool Loop;
        }

        private static readonly StateJob[] Jobs =
        {
            new StateJob { Folder = "Idle",      ClipPath = AnimFolder + "/Player_Idle.anim",       Fps = 8,  Loop = true },
            new StateJob { Folder = "Walk",      ClipPath = AnimFolder + "/Player_Walk.anim",       Fps = 12, Loop = true },
            new StateJob { Folder = "Jump",      ClipPath = AnimFolder + "/Player_Jump.anim",       Fps = 10, Loop = false },
            new StateJob { Folder = "DobleJump", ClipPath = AnimFolder + "/Player_DoubleJump.anim", Fps = 12, Loop = false },
            new StateJob { Folder = "Dash",      ClipPath = AnimFolder + "/Player_Dash.anim",       Fps = 14, Loop = false },
            new StateJob { Folder = "Attack",    ClipPath = AnimFolder + "/Player_Attack.anim",     Fps = 12, Loop = false },
            new StateJob { Folder = "Hurt",      ClipPath = AnimFolder + "/Player_Hurt.anim",       Fps = 14, Loop = false },
            new StateJob { Folder = "Death",     ClipPath = AnimFolder + "/Player_Die.anim",        Fps = 8,  Loop = false },
        };

        private const string InteractClipPath = AnimFolder + "/Player_Interact.anim";
        private const float InteractFps = 12f;

        [MenuItem("Tools/RedMagic/Pipeline/Packs/Player · Reconstruir clips desde carpetas de frames")]
        public static void Build() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();

            foreach (var job in Jobs)
            {
                var sprites = LoadAndConfigureFolder($"{FramesRoot}/{job.Folder}", log);
                if (sprites.Count == 0)
                {
                    log.AppendLine($"[PlayerFrameImporter] AVISO: sin frames en '{FramesRoot}/{job.Folder}', se deja '{job.ClipPath}' como está.");
                    continue;
                }

                WriteClip(job.ClipPath, sprites, job.Fps, job.Loop, log);
            }

            // JumpAttack reutiliza los mismos frames que Attack (igual que hacía antes con la
            // lámina del dragón) — sin esto se quedaría con el arte anterior.
            var attackJob = System.Array.Find(Jobs, j => j.Folder == "Attack");
            var attackSprites = LoadFolderSprites($"{FramesRoot}/{attackJob.Folder}");
            if (attackSprites.Count > 0)
                WriteClip(AnimFolder + "/Player_JumpAttack.anim", attackSprites, 12, false, log);

            // Fall: la lámina no dibuja caída — se congela en el último frame del salto, igual que
            // hacía la receta del sheet original.
            var jumpJob = System.Array.Find(Jobs, j => j.Folder == "Jump");
            var jumpSprites = LoadFolderSprites($"{FramesRoot}/{jumpJob.Folder}");
            if (jumpSprites.Count > 0)
                WriteClip(AnimFolder + "/Player_Fall.anim", new List<Sprite> { jumpSprites[jumpSprites.Count - 1] }, 10, true, log);

            // Interact es nuevo: no había estado ni clip antes. Se crea el asset si falta.
            var interactSprites = LoadAndConfigureFolder($"{FramesRoot}/Interact", log);
            AnimationClip interactClip = null;
            if (interactSprites.Count > 0)
                interactClip = WriteClip(InteractClipPath, interactSprites, InteractFps, false, log, createIfMissing: true);
            else
                log.AppendLine("[PlayerFrameImporter] AVISO: sin frames de Interact, no se crea el clip.");

            WireController(interactClip, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        // ============================================================ sprites

        private static List<Sprite> LoadAndConfigureFolder(string relativeFolder, StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(relativeFolder))
            {
                log.AppendLine($"[PlayerFrameImporter] no existe la carpeta '{relativeFolder}'.");
                return new List<Sprite>();
            }

            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { relativeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToList();

            foreach (var path in paths) ConfigureSpriteImport(path);
            AssetDatabase.Refresh();

            return paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToList();
        }

        private static List<Sprite> LoadFolderSprites(string relativeFolder)
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { relativeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToList();
        }

        /// <summary>
        /// Single (no Multiple: el auto-slice del importador partía frames con estelas/auras en dos
        /// sub-sprites), pivote a los pies (BottomCenter) — la convención del resto del pipeline —
        /// y PPU/filtro fijos para que las 9 animaciones queden uniformes entre sí.
        /// </summary>
        private static void ConfigureSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = Filter;
            importer.spritePixelsPerUnit = PixelsPerUnit;

            var pivot = new Vector2(0.5f, 0f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.spritePivot = pivot;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        // ============================================================ clips

        /// <summary>
        /// Reescribe (o crea, si <paramref name="createIfMissing"/>) el AnimationClip en su sitio —
        /// conserva el GUID, así que el AnimatorController no necesita volver a cablearse. El binding
        /// va con <c>path=""</c>: en el prefab del jugador el Animator y el SpriteRenderer viven en el
        /// MISMO objeto (el hijo 'Sprite'), no en padre/hijo como en los enemigos generados.
        /// </summary>
        private static AnimationClip WriteClip(string path, List<Sprite> sprites, float fps, bool loop,
                                                StringBuilder log, bool createIfMissing = false)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;

            if (clip == null)
            {
                if (!createIfMissing)
                {
                    log.AppendLine($"[PlayerFrameImporter] ERROR: no existe el clip '{path}' y createIfMissing=false.");
                    return null;
                }

                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = fps;

            var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };

            // Limpia cualquier binding previo con otra ruta (por si el clip viniera de otra jerarquía).
            foreach (var existing in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, existing, null);

            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = loop;
            clipSettings.stopTime = sprites.Count / fps;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            EditorUtility.SetDirty(clip);
            log.AppendLine($"[PlayerFrameImporter] clip {(created ? "creado " : "actualizado")} '{path}': {sprites.Count} frames @ {fps}fps, loop={loop}.");
            return clip;
        }

        // ============================================================ controller

        /// <summary>
        /// Quita Crouch/CrouchAttack (mecánica retirada) y añade Interact (gesto puntual, mismo
        /// patrón que Hurt/Attack: AnyState → estado por trigger sin exit time, estado → salida por
        /// tiempo con exitTime=1). Idempotente: si Interact ya está cableado no duplica nada.
        /// </summary>
        private static void WireController(AnimationClip interactClip, StringBuilder log)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                log.AppendLine($"[PlayerFrameImporter] ERROR: no se encontró el controller '{ControllerPath}'.");
                return;
            }

            var machine = controller.layers[0].stateMachine;

            RemoveCrouchMechanic(controller, machine, log);

            if (interactClip != null)
                WireInteractState(controller, machine, interactClip, log);

            EditorUtility.SetDirty(controller);
        }

        private static void RemoveCrouchMechanic(AnimatorController controller, AnimatorStateMachine machine, StringBuilder log)
        {
            AnimatorState crouch = null, crouchAttack = null;
            foreach (var child in machine.states)
            {
                if (child.state.name == "Crouch") crouch = child.state;
                else if (child.state.name == "CrouchAttack") crouchAttack = child.state;
            }

            if (crouch != null) { machine.RemoveState(crouch); log.AppendLine("[PlayerFrameImporter] estado 'Crouch' eliminado."); }
            if (crouchAttack != null) { machine.RemoveState(crouchAttack); log.AppendLine("[PlayerFrameImporter] estado 'CrouchAttack' eliminado."); }

            // La transición AnyState → Attack seguía comprobando ¬Crouching; sin el estado, la
            // condición queda muerta pero se retira igualmente para no dejar basura.
            foreach (var transition in machine.anyStateTransitions)
            {
                if (transition.destinationState == null || transition.destinationState.name != "Attack") continue;

                var kept = transition.conditions.Where(c => c.parameter != "Crouching").ToArray();
                if (kept.Length != transition.conditions.Length)
                {
                    transition.conditions = kept;
                    log.AppendLine("[PlayerFrameImporter] condición 'Crouching' quitada de AnyState→Attack.");
                }
            }

            RemoveParameter(controller, "Crouching", log);
            RemoveParameter(controller, "CrouchSpeed", log);
            RemoveParameter(controller, "CrouchAttackSpeed", log);
        }

        private static void RemoveParameter(AnimatorController controller, string name, StringBuilder log)
        {
            if (!controller.parameters.Any(p => p.name == name)) return;
            controller.RemoveParameter(controller.parameters.First(p => p.name == name));
            log.AppendLine($"[PlayerFrameImporter] parámetro '{name}' eliminado.");
        }

        private static void WireInteractState(AnimatorController controller, AnimatorStateMachine machine,
                                              AnimationClip clip, StringBuilder log)
        {
            EnsureParameter(controller, "Interact", AnimatorControllerParameterType.Trigger);

            AnimatorState interact = null, fall = null, idle = null;
            foreach (var child in machine.states)
            {
                switch (child.state.name)
                {
                    case "Interact": interact = child.state; break;
                    case "Fall": fall = child.state; break;
                    case "Idle": idle = child.state; break;
                }
            }

            if (interact == null)
            {
                interact = machine.AddState("Interact");
                log.AppendLine("[PlayerFrameImporter] estado 'Interact' creado.");
            }
            interact.motion = clip;

            var exit = fall ?? idle;
            if (exit == null) return;

            bool hasEnter = machine.anyStateTransitions.Any(t => t.destinationState == interact);
            if (!hasEnter)
            {
                var enter = machine.AddAnyStateTransition(interact);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Interact");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.hasExitTime = false;
                enter.duration = 0.02f;
                enter.canTransitionToSelf = false;
                log.AppendLine("[PlayerFrameImporter] Interact cableado (AnyState ← trigger Interact).");
            }

            bool hasExit = interact.transitions.Any(t => t.destinationState == exit);
            if (!hasExit)
            {
                var leave = interact.AddTransition(exit);
                leave.hasExitTime = true;
                leave.exitTime = 1f;
                leave.duration = 0.05f;
                log.AppendLine($"[PlayerFrameImporter] Interact → {exit.name} cableado (salida por tiempo).");
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(name, type);
        }
    }
}
