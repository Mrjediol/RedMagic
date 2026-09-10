using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Paso 2 del pipeline: sprites cortados → <see cref="AnimationClip"/> por estado →
    /// <see cref="AnimatorController"/> cableado.
    ///
    /// Tres decisiones que no son evidentes y que sostienen todo lo demás:
    /// <list type="bullet">
    /// <item><b>El Animator va en la raíz</b>, no en el hijo del sprite, y los clips animan al
    /// hijo por ruta (<see cref="RendererPath"/>). Los <c>AnimationEvent</c> sólo llegan a
    /// componentes del mismo GameObject, y el cerebro y los ataques del enemigo viven en la raíz.</item>
    /// <item><b>El clip de ataque lleva sus eventos puestos</b>: uno en el frame en que suelta el
    /// golpe y otro al final. Es lo que hace que el proyectil salga clavado con el dibujo en vez
    /// de a ojo con un temporizador.</item>
    /// <item><b>Cada estado tiene su propio multiplicador de velocidad</b> como parámetro, no un
    /// <c>animator.speed</c> global, para poder tener el reposo lento y el ataque rápido a la vez.</item>
    /// </list>
    ///
    /// <b>Actualiza en vez de rehacer.</b> Si el controller ya existe se le reescriben los frames y
    /// se le añaden los estados que falten, dejando intactas las transiciones afinadas a mano.
    /// </summary>
    public static class AnimClipBuilder
    {
        /// <summary>Estados que el generador sabe cablear solo. El resto se crea suelto.</summary>
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Attack = "Attack";
        public const string Hurt = "Hurt";
        public const string Death = "Death";

        /// <summary>Hijo que lleva el <see cref="SpriteRenderer"/>, visto desde la raíz.</summary>
        public const string RendererPath = "Sprite";

        /// <summary>Métodos que reciben los eventos, en <c>EnemyAnimation</c> (en la raíz).</summary>
        private const string ReleaseEvent = "OnAttackRelease";
        private const string FinishEvent = "OnAttackFinished";

        /// <summary>Parámetro de velocidad de cada estado: "Idle" → "IdleSpeed".</summary>
        private static string SpeedParameter(string state) => state + "Speed";

        // ============================================================ clips

        public static Dictionary<string, AnimationClip> BuildClips(SpriteSheetRecipe recipe,
                                                                   SheetSlicer.Result sliced,
                                                                   StringBuilder log)
        {
            string folder = $"{recipe.ResolvedFolder}/Anim";
            SheetSlicer.EnsureFolder(folder);

            var clips = new Dictionary<string, AnimationClip>();

            foreach (var row in recipe.rows)
            {
                if (!sliced.ByState.TryGetValue(row.state, out var slicedSprites) || slicedSprites.Count == 0)
                    continue;

                // Cargar los sprites FRESCOS del PNG por su ruta, no confiar en las referencias que
                // trae el resultado del corte: en la primera importación de una lámina nueva esas
                // instancias mueren en el reimport, y un clip con referencias muertas sale vacío
                // (1 s, 60 fps, sin eventos) — el bug que obligaba a lanzar el pack dos veces.
                var sprites = ReloadStateSprites(recipe, row.state, slicedSprites.Count, log);
                if (sprites.Count == 0) continue;

                string path = $"{folder}/{recipe.characterName}_{row.state}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool created = clip == null;

                if (created)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, path);
                }

                clip.frameRate = row.fps;

                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = RendererPath,
                    propertyName = "m_Sprite",
                };

                // Se limpia la ruta antigua: una hoja generada antes de mover el Animator a la raíz
                // dejaría dos curvas y el sprite parpadearía entre ellas.
                AnimationUtility.SetObjectReferenceCurve(clip, LegacyBinding(), null);

                var keys = new ObjectReferenceKeyframe[sprites.Count];
                for (int i = 0; i < sprites.Count; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i / row.fps, value = sprites[i] };

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = row.loop;
                settings.stopTime = sprites.Count / row.fps;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                if (row.state == Attack) AddAttackEvents(clip, row, sprites.Count, log);

                EditorUtility.SetDirty(clip);
                clips[row.state] = clip;
                log.AppendLine($"  clip {(created ? "creado " : "actualizado")} {row.state}: " +
                               $"{sprites.Count} frames @ {row.fps}fps, loop={row.loop}");
            }

            return clips;
        }

        /// <summary>
        /// Los sub-sprites de <c><Char>_<Estado>.png</c> en orden, recargados del disco con un
        /// reintento por si la importación aún no ha dejado los sub-assets consultables.
        /// </summary>
        private static List<Sprite> ReloadStateSprites(SpriteSheetRecipe recipe, string state,
                                                      int expected, StringBuilder log)
        {
            string path = $"{recipe.ResolvedFolder}/{recipe.characterName}_{state}.png";
            var names = new string[expected];
            for (int i = 0; i < expected; i++) names[i] = SheetSlicer.SpriteName(recipe.characterName, state, i);

            List<Sprite> Load()
            {
                var byName = new Dictionary<string, Sprite>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite s) byName[s.name] = s;

                var ordered = new List<Sprite>();
                foreach (var n in names)
                    if (byName.TryGetValue(n, out var s)) ordered.Add(s);
                return ordered;
            }

            var sprites = Load();
            for (int attempt = 0; attempt < 3 && sprites.Count != expected; attempt++)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                sprites = Load();
            }

            if (sprites.Count != expected)
                log.AppendLine($"  AVISO {state}: el clip esperaba {expected} sprites y se han " +
                               $"recargado {sprites.Count} de {path}.");

            return sprites;
        }

        private static EditorCurveBinding LegacyBinding() => new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite",
        };

        /// <summary>
        /// Clava los dos avisos del ataque en el clip: el golpe y el final. Con
        /// <c>releaseFrame</c> a -1 sólo se pone el del final — el golpe caerá por el tiempo de
        /// respaldo de <c>EnemyStats</c>.
        /// </summary>
        private static void AddAttackEvents(AnimationClip clip, SheetRow row, int frames,
                                            StringBuilder log)
        {
            var events = new List<AnimationEvent>();

            if (row.releaseFrame >= 0)
            {
                int frame = Mathf.Clamp(row.releaseFrame, 0, frames - 1);
                events.Add(new AnimationEvent { time = frame / row.fps, functionName = ReleaseEvent });
                log.AppendLine($"    evento {ReleaseEvent} en el frame {frame} ({frame / row.fps:F2}s)");
            }

            // El del final va un pelo antes del último frame: justo en el borde, Unity a veces se
            // lo salta si el clip no repite.
            float end = Mathf.Max(0f, (frames - 0.01f) / row.fps);
            events.Add(new AnimationEvent { time = end, functionName = FinishEvent });

            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        }

        // ============================================================ controller

        public static AnimatorController BuildController(SpriteSheetRecipe recipe,
                                                         Dictionary<string, AnimationClip> clips,
                                                         StringBuilder log)
        {
            if (clips.Count == 0) return null;

            string path = $"{recipe.ResolvedFolder}/{recipe.characterName}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            bool created = controller == null;

            if (created) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            EnsureParameter(controller, "Moving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Dead", AnimatorControllerParameterType.Bool);

            foreach (var state in clips.Keys)
                EnsureParameter(controller, SpeedParameter(state), AnimatorControllerParameterType.Float, 1f);

            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (var child in machine.states) states[child.state.name] = child.state;

            foreach (var pair in clips)
            {
                if (!states.TryGetValue(pair.Key, out var state))
                {
                    state = machine.AddState(pair.Key);
                    states[pair.Key] = state;
                    log.AppendLine($"  estado nuevo: {pair.Key}");
                }

                state.motion = pair.Value;

                // Cada estado corre a su propio multiplicador, editable desde EnemyStats.
                state.speedParameterActive = true;
                state.speedParameter = SpeedParameter(pair.Key);
            }

            if (states.TryGetValue(Idle, out var idle)) machine.defaultState = idle;

            if (created || machine.anyStateTransitions.Length == 0) WireTransitions(machine, states, log);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            log.AppendLine($"  controller {(created ? "creado" : "actualizado")}: {path}");
            return controller;
        }

        /// <summary>
        /// El cableado por defecto, deliberadamente simple porque tiene que valer para cualquier
        /// enemigo: reposo/andar por un bool que escribe el cerebro, golpe y ataque desde AnyState
        /// (para que interrumpan lo que sea) y muerte como destino sin salida.
        ///
        /// El ataque sale por <b>tiempo de salida</b>, no por trigger de vuelta: el clip manda, y
        /// es su último frame el que avisa al cerebro de que ya puede enfriar.
        /// </summary>
        private static void WireTransitions(AnimatorStateMachine machine,
                                            Dictionary<string, AnimatorState> states,
                                            StringBuilder log)
        {
            states.TryGetValue(Idle, out var idle);
            states.TryGetValue(Walk, out var walk);

            if (idle != null && walk != null)
            {
                var toWalk = idle.AddTransition(walk);
                toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
                toWalk.hasExitTime = false;
                toWalk.duration = 0.05f;

                var toIdle = walk.AddTransition(idle);
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
                toIdle.hasExitTime = false;
                toIdle.duration = 0.05f;
            }

            foreach (var name in new[] { Attack, Hurt })
            {
                if (!states.TryGetValue(name, out var state)) continue;

                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, name);
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.hasExitTime = false;
                enter.duration = 0.02f;
                enter.canTransitionToSelf = false;

                if (idle == null) continue;

                var exit = state.AddTransition(idle);
                exit.hasExitTime = true;
                exit.exitTime = 1f;
                exit.duration = 0.05f;
            }

            if (states.TryGetValue(Death, out var death))
            {
                var die = machine.AddAnyStateTransition(death);
                die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                die.hasExitTime = false;
                die.duration = 0.05f;
                die.canTransitionToSelf = false;
            }

            log.AppendLine("  transiciones por defecto cableadas.");
        }

        private static void EnsureParameter(AnimatorController controller, string name,
                                            AnimatorControllerParameterType type,
                                            float defaultFloat = 0f)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name) return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = type,
                defaultFloat = defaultFloat,
            });
        }

        // ============================================================ flipbook

        /// <summary>
        /// La alternativa sin Animator: rellena un <see cref="SpriteStateMachine"/> con los mismos
        /// estados. Es lo que se usa en todo lo que pasa por pool, donde un Animator volvería del
        /// pool a mitad de la animación de muerte.
        /// </summary>
        public static void FillStateMachine(SpriteStateMachine target, SpriteSheetRecipe recipe,
                                            SheetSlicer.Result sliced, StringBuilder log)
        {
            var states = new List<SpriteStateMachine.State>();

            foreach (var row in recipe.rows)
            {
                if (!sliced.ByState.TryGetValue(row.state, out var sprites) || sprites.Count == 0) continue;

                states.Add(new SpriteStateMachine.State
                {
                    name = row.state,
                    frames = sprites.ToArray(),
                    fps = row.fps,
                    loop = row.loop,
                });
            }

            target.EditorSetStates(states.ToArray(), Idle);
            log.AppendLine($"  flipbook: {states.Count} estados.");
        }
    }
}
