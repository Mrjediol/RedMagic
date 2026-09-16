using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Hub.EditorTools
{
    /// <summary>
    /// Construye los <see cref="AnimatorController"/> de los props del hub. No encajan en
    /// <c>AnimClipBuilder.BuildController</c> (pensado para el vocabulario de un enemigo:
    /// Idle/Walk/Attack/Hurt/Death con un cerebro detrás) ni en el patrón de gestos de
    /// <c>BossAnimator</c> (estados sueltos que vuelven solos a Idle): un cofre/armario/libro
    /// necesita un <b>bool</b> con dos direcciones — abrir Y cerrar son transiciones con
    /// condición, no un gesto de una vía — así que hace falta esta pieza aparte.
    ///
    /// Reutilizable para cualquier prop futuro con la misma forma: pásale los clips (recortados
    /// con el pipeline normal, <c>AnimClipBuilder.BuildClips</c> + <c>DerivedClip</c> para
    /// Cerrado/Abierto/Cerrando) y te da el controller. Es idempotente como el resto del pipeline:
    /// si el controller ya existe, sólo se reescriben los <c>motion</c> de los estados — las
    /// transiciones cableadas a mano no se tocan una segunda vez.
    /// </summary>
    public static class OpenCloseControllerBuilder
    {
        private const string ClosedState = "Closed";
        private const string OpeningState = "Opening";
        private const string OpenState = "Open";
        private const string ClosingState = "Closing";

        private const string IdleState = "Idle";
        private const string ActionState = "Action";

        /// <summary>
        /// Cofre / armario / libro: <c>Closed</c> —(bool a true)→ <c>Opening</c> —(fin)→
        /// <c>Open</c> —(bool a false)→ <c>Closing</c> —(fin)→ <c>Closed</c>.
        /// </summary>
        public static AnimatorController BuildBoolDriven(string controllerPath, string paramName,
                                                          AnimationClip closed, AnimationClip opening,
                                                          AnimationClip open, AnimationClip closing)
        {
            bool created = !AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var controller = LoadOrCreate(controllerPath);

            EnsureBoolParameter(controller, paramName);

            var machine = controller.layers[0].stateMachine;
            var closedState = EnsureState(machine, ClosedState, closed);
            var openingState = EnsureState(machine, OpeningState, opening);
            var openState = EnsureState(machine, OpenState, open);
            var closingState = EnsureState(machine, ClosingState, closing);

            machine.defaultState = closedState;

            if (created)
            {
                var toOpening = closedState.AddTransition(openingState);
                toOpening.AddCondition(AnimatorConditionMode.If, 0f, paramName);
                toOpening.hasExitTime = false;
                toOpening.duration = 0f;

                var toOpen = openingState.AddTransition(openState);
                toOpen.hasExitTime = true;
                toOpen.exitTime = 1f;
                toOpen.duration = 0f;

                var toClosing = openState.AddTransition(closingState);
                toClosing.AddCondition(AnimatorConditionMode.IfNot, 0f, paramName);
                toClosing.hasExitTime = false;
                toClosing.duration = 0f;

                var toClosed = closingState.AddTransition(closedState);
                toClosed.hasExitTime = true;
                toClosed.exitTime = 1f;
                toClosed.duration = 0f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Yunque: <c>Idle</c> —(trigger)→ <c>Action</c> —(fin, sin condición)→ vuelve a
        /// <c>Idle</c> sola. <paramref name="idle"/> puede ser null (estado vacío, primer frame de
        /// la propia acción se ve igual de bien como reposo).
        /// </summary>
        public static AnimatorController BuildTriggerOneShot(string controllerPath, string triggerName,
                                                              AnimationClip idle, AnimationClip action)
        {
            bool created = !AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var controller = LoadOrCreate(controllerPath);

            EnsureTriggerParameter(controller, triggerName);

            var machine = controller.layers[0].stateMachine;
            var idleState = EnsureState(machine, IdleState, idle);
            var actionState = EnsureState(machine, ActionState, action);

            machine.defaultState = idleState;

            if (created)
            {
                var enter = machine.AddAnyStateTransition(actionState);
                enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
                enter.hasExitTime = false;
                enter.duration = 0f;
                enter.canTransitionToSelf = false;

                var exit = actionState.AddTransition(idleState);
                exit.hasExitTime = true;
                exit.exitTime = 1f;
                exit.duration = 0f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Espejo: un único estado en bucle, sin parámetros — el brillo no para nunca.</summary>
        public static AnimatorController BuildIdleLoop(string controllerPath, AnimationClip idle)
        {
            var controller = LoadOrCreate(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var idleState = EnsureState(machine, IdleState, idle);
            machine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorController LoadOrCreate(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            return controller != null ? controller : AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        private static AnimatorState EnsureState(AnimatorStateMachine machine, string name, AnimationClip motion)
        {
            foreach (var child in machine.states)
                if (child.state.name == name)
                {
                    child.state.motion = motion;
                    return child.state;
                }

            var state = machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void EnsureBoolParameter(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name) return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Bool,
            });
        }

        private static void EnsureTriggerParameter(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name) return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Trigger,
            });
        }
    }
}
