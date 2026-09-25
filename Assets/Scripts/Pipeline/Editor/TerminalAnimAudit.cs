using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Auditoría de animaciones terminales (<see cref="AnimStates"/>: muerte, impacto, explosión…).
    /// Encuentra —y con <c>repair</c> arregla— todo lo que puede hacer que una de esas animaciones
    /// vuelva a empezar al acabar:
    /// <list type="bullet">
    /// <item>Clips (<c>.anim</c>) terminales con <c>loopTime</c> encendido.</item>
    /// <item>Controllers: transición desde AnyState a un estado terminal que puede re-entrar en sí
    /// mismo (<c>canTransitionToSelf</c>), o transiciones de SALIDA de un estado terminal.</item>
    /// <item>Prefabs: estados terminales de un <see cref="SpriteStateMachine"/> con <c>loop</c>, y
    /// flipbooks de un <see cref="VfxOneShot"/> sin <c>oneShot</c>.</item>
    /// </list>
    /// El runtime ya se protege solo (EnemyAnimation, SpriteStateMachine, VfxOneShot), pero esto
    /// deja los assets diciendo la verdad. Relánzalo tras importar contenido del exportador web.
    /// </summary>
    public static class TerminalAnimAudit
    {
        private static readonly string[] Vendored = { "Assets/Brackeys", "Assets/Cainos", "Assets/Dragon Warrior Files" };

        [MenuItem("Tools/RedMagic/Pipeline/8 · Auditar animaciones terminales (muerte, impacto)")]
        public static void AuditMenu() => Debug.Log(Run(repair: false));

        [MenuItem("Tools/RedMagic/Pipeline/9 · Auditar y reparar animaciones terminales")]
        public static void RepairMenu() => Debug.Log(Run(repair: true));

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run(bool repair)
        {
            var log = new StringBuilder($"[TerminalAnimAudit] {(repair ? "REPARAR" : "auditar")}\n");
            int issues = 0;

            issues += Clips(repair, log);
            issues += Controllers(repair, log);
            issues += Prefabs(repair, log);

            if (repair) AssetDatabase.SaveAssets();
            log.AppendLine(issues == 0 ? "Todo correcto." : $"{issues} problema(s){(repair ? " reparados" : "")}.");
            return log.ToString();
        }

        public static string RunRepair() => Run(repair: true);

        private static IEnumerable<string> Paths(string filter) =>
            AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !Vendored.Any(v => p.StartsWith(v)))
                .Distinct();

        private static int Clips(bool repair, StringBuilder log)
        {
            int n = 0;
            foreach (var path in Paths("t:AnimationClip"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !AnimStates.IsTerminal(clip.name)) continue;

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (!settings.loopTime) continue;

                n++;
                log.AppendLine($"  clip en bucle: {path}");
                if (!repair) continue;

                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
            return n;
        }

        private static int Controllers(bool repair, StringBuilder log)
        {
            int n = 0;
            foreach (var path in Paths("t:AnimatorController"))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;

                bool dirty = false;
                foreach (var layer in controller.layers)
                    n += Machine(layer.stateMachine, path, repair, log, ref dirty);

                if (dirty) EditorUtility.SetDirty(controller);
            }
            return n;
        }

        private static int Machine(AnimatorStateMachine machine, string path, bool repair, StringBuilder log, ref bool dirty)
        {
            int n = 0;

            foreach (var t in machine.anyStateTransitions)
            {
                if (t.destinationState == null || !AnimStates.IsTerminal(t.destinationState.name) || !t.canTransitionToSelf)
                    continue;
                n++;
                log.AppendLine($"  {path}: AnyState → {t.destinationState.name} puede re-entrar en sí mismo");
                if (repair) { t.canTransitionToSelf = false; dirty = true; }
            }

            foreach (var child in machine.states)
            {
                var state = child.state;
                if (!AnimStates.IsTerminal(state.name) || state.transitions.Length == 0) continue;

                n++;
                log.AppendLine($"  {path}: {state.name} tiene {state.transitions.Length} transición(es) de salida");
                if (!repair) continue;

                foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
                dirty = true;
            }

            foreach (var sub in machine.stateMachines)
                n += Machine(sub.stateMachine, path, repair, log, ref dirty);

            return n;
        }

        private static int Prefabs(bool repair, StringBuilder log)
        {
            int n = 0;
            foreach (var path in Paths("t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool isVfx = prefab.GetComponent<VfxOneShot>() != null;
                var machines = prefab.GetComponentsInChildren<SpriteStateMachine>(true);
                var flipbooks = isVfx ? prefab.GetComponentsInChildren<SpriteFlipbook>(true) : new SpriteFlipbook[0];
                if (machines.Length == 0 && flipbooks.Length == 0) continue;

                bool changed = false;

                foreach (var machine in machines)
                {
                    var so = new SerializedObject(machine);
                    var states = so.FindProperty("states");
                    for (int i = 0; i < states.arraySize; i++)
                    {
                        var s = states.GetArrayElementAtIndex(i);
                        string name = s.FindPropertyRelative("name").stringValue;
                        var loop = s.FindPropertyRelative("loop");
                        if (!AnimStates.IsTerminal(name) || !loop.boolValue) continue;

                        n++;
                        log.AppendLine($"  {path}: estado '{name}' en bucle");
                        if (repair) { loop.boolValue = false; changed = true; }
                    }
                    if (repair) so.ApplyModifiedPropertiesWithoutUndo();
                }

                foreach (var flipbook in flipbooks)
                {
                    var so = new SerializedObject(flipbook);
                    var oneShot = so.FindProperty("oneShot");
                    if (oneShot.boolValue) continue;

                    n++;
                    log.AppendLine($"  {path}: VFX de un solo uso con flipbook en bucle");
                    if (!repair) continue;

                    oneShot.boolValue = true;
                    so.FindProperty("pingPong").boolValue = false;
                    so.FindProperty("randomStart").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed) EditorUtility.SetDirty(prefab);
            }
            return n;
        }
    }
}
