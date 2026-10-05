using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Dibuja <see cref="EnemyStats"/> <b>plano</b>: los valores están dentro de un objeto anidado
    /// (<see cref="EnemyTuning"/>) para poder compartir la misma lista con la ficha del pipeline,
    /// pero eso no debe costarle al que afina un enemigo un desplegable extra por cada visita.
    ///
    /// Además oculta lo que no aplica al arquetipo elegido — la caja de melé en un enemigo que
    /// dispara, el esquive en uno que no vuela —, que es la mitad de por qué un inspector largo se
    /// vuelve difícil de leer.
    /// </summary>
    [CustomEditor(typeof(EnemyStats))]
    public class EnemyStatsEditor : UnityEditor.Editor
    {
        /// <summary>Grupos abiertos de entrada: lo que se toca a diario. El resto, plegado (pero ahí).</summary>
        private static readonly string[] OpenGroups =
        {
            "Tipo", "Rangos", "Ataque", "Ataque · baraja (ataques del catálogo)",
            "Ataque · melé", "Ataque · a distancia",
        };

        private List<string> _fx;
        private List<string> _attacks;
        private bool _showFx = true;

        private void OnEnable() => _fx = null;

        /// <summary>
        /// Atajo a todo lo que el enemigo suelta: proyectiles y efectos (Prefabs/Fx) y los ataques del
        /// catálogo. Sale de las dependencias del prefab, así que cubre barajas, gestos y rig sin
        /// cablear nada por tipo.
        /// </summary>
        private void DrawShortcuts()
        {
            if (_fx == null)
            {
                _fx = new List<string>();
                _attacks = new List<string>();
                var go = ((EnemyStats)target).gameObject;
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(path)) path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path))  // Prefab Mode: el objeto es de la etapa, no del asset
                {
                    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go);
                    if (stage != null) path = stage.assetPath;
                }
                if (string.IsNullOrEmpty(path)) { _fx = null; return; }
                    foreach (var dep in AssetDatabase.GetDependencies(path, true))
                    {
                        if (dep.EndsWith(".prefab") && dep.StartsWith("Assets/Prefabs/Fx/")) _fx.Add(dep);
                        else if (dep.EndsWith(".asset") && dep.StartsWith("Assets/ScriptableObjects/EnemyAttacks/"))
                            _attacks.Add(dep);
                    }
            }

            _showFx = EditorGUILayout.Foldout(_showFx,
                $"Proyectiles y efectos ({_fx.Count}) · Ataques ({_attacks.Count})", true);
            if (!_showFx) return;

            EditorGUI.indentLevel++;
            foreach (var list in new[] { _attacks, _fx })
                foreach (var path in list)
                {
                    var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                    var icon = AssetDatabase.GetCachedIcon(path);
                    var label = new GUIContent(" " + System.IO.Path.GetFileNameWithoutExtension(path), icon, path);
                    if (GUI.Button(rect, label, EditorStyles.objectField))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(path);
                        EditorGUIUtility.PingObject(asset);
                        if (Event.current.clickCount == 2) Selection.activeObject = asset;
                    }
                }
            EditorGUI.indentLevel--;
            if (GUILayout.Button("Refrescar lista", EditorStyles.miniButton)) _fx = null;
            EditorGUILayout.Space(4f);
        }

        public override void OnInspectorGUI()
        {
            // Enemigo por partes: sus gestos y ataques se editan en la web, que guarda aquí (el puente).
            if (targets.Length == 1)
                RedMagic.Rigging.EditorTools.WebBridgeMenu.DrawInspector(((EnemyStats)target).gameObject);

            if (targets.Length == 1) DrawShortcuts();

            serializedObject.Update();

            var tuning = serializedObject.FindProperty("tuning");
            var archetype = tuning.FindPropertyRelative("archetype");
            var value = (EnemyArchetype)archetype.enumValueIndex;

            bool flies = value is EnemyArchetype.FlyingMelee or EnemyArchetype.FlyingRanged;
            bool moves = value != EnemyArchetype.Static;

            var staticAttack = tuning.FindPropertyRelative("staticAttack");
            bool ranged = moves
                ? value is EnemyArchetype.Ranged or EnemyArchetype.FlyingRanged
                : (AttackKind)staticAttack.enumValueIndex == AttackKind.Ranged;

            bool sleeps = tuning.FindPropertyRelative("sleepsUntilDetected").boolValue;
            bool explodes = tuning.FindPropertyRelative("selfDestruct").boolValue;
            bool deck = tuning.FindPropertyRelative("attacks").arraySize > 0;

            EditorGUILayout.HelpBox(Describe(value), MessageType.Info);
            if (sleeps)
                EditorGUILayout.HelpBox("Dormido: su Idle es dormir. Al detectar (o al recibir un " +
                                        "golpe) reproduce 'Wake' quieto y persigue; al rendirse " +
                                        "vuelve a su sitio y se duerme.", MessageType.None);
            if (explodes)
                EditorGUILayout.HelpBox("Kamikaze: al llegar a 'attackRange' reproduce el ataque " +
                                        "(la mecha) y explota — 'attackDamage' en " +
                                        "'explosionRadius' — y muere. Su clip de muerte es la " +
                                        "explosión.", MessageType.None);

            // Un grupo plegable por cada [Header] de EnemyTuning (nada se esconde: se baja el ruido).
            var iterator = tuning.Copy();
            var end = iterator.GetEndProperty();
            var visible = new List<SerializedProperty>();
            bool first = true;

            while (iterator.NextVisible(first) && !SerializedProperty.EqualContents(iterator, end))
            {
                first = false;
                if (Hidden(iterator.name, flies, ranged && !explodes, moves, sleeps, explodes, deck)) continue;

                visible.Add(iterator.Copy());
            }

            FoldoutInspector.Draw(typeof(EnemyTuning), visible, OpenGroups);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Aplicar a Health / Knockback / cuerpo"))
                foreach (var t in targets) ((EnemyStats)t).Apply();
        }

        private static bool Hidden(string field, bool flies, bool ranged, bool moves, bool sleeps,
                                   bool explodes, bool deck)
        {
            switch (field)
            {
                // Un kamikaze sólo explota: no lleva baraja. Y el peso del ataque propio sólo
                // significa algo si hay cartas con las que sortearlo.
                case "attacks":
                    return explodes;

                // Un Static no se desplaza: no puede apartarse.
                case "defences":
                    return !moves;

                case "builtInAttack":
                case "builtInWeight":
                    return explodes || !deck;

                case "wakeAnimSpeed":
                    return !sleeps;

                case "explosionRadius":
                case "explosionShake":
                    return !explodes;

                // Muere al primer ataque: ni enfriamiento ni caja de melé.
                case "attackCooldown":
                    return explodes;

                // Los que se mueven ya dicen en su nombre si disparan o golpean.
                case "staticAttack":
                    return moves;

                // Detección: los que persiguen y los dormilones (una torreta plegada también).
                case "detectionRange":
                case "loseInterestGrace":
                    return !moves && !sleeps;

                // Sólo un enemigo que se mueve tiene persecución y bordes.
                case "moveSpeed":
                case "moveSoundInterval":
                case "stopAtLedges":
                case "ledgeProbeDepth":
                    return !moves;

                case "retreatSpeed":
                case "personalSpace":
                    return !moves || !ranged;

                case "hoverOffset":
                    return !flies;

                case "gravityScale":
                    return flies;

                case "meleeHitboxSize":
                case "meleeHitboxOffset":
                    return ranged || explodes;

                case "projectile":
                case "aimAtTarget":
                case "projectileSprite":
                case "projectileTint":
                    return !ranged;

                case "moveAnimSpeed":
                    return !moves;

                default:
                    return false;
            }
        }

        private static string Describe(EnemyArchetype archetype) => archetype switch
        {
            EnemyArchetype.Static =>
                "Estático: no se mueve ni detecta. Espera en reposo y ataca en cuanto el objetivo " +
                "entra en su rango de ataque; entre golpe y golpe vuelve al reposo. Elige abajo " +
                "si dispara o golpea.",
            EnemyArchetype.Melee =>
                "Melé de suelo: reposo → detecta → camina → golpea de cerca.",
            EnemyArchetype.Ranged =>
                "A distancia de suelo: camina hasta tenerlo a tiro, dispara, y retrocede si le " +
                "invaden el espacio propio.",
            EnemyArchetype.FlyingMelee =>
                "Melé volador: va por el aire esquivando obstáculos y golpea de cerca.",
            EnemyArchetype.FlyingRanged =>
                "A distancia volador: vuela hasta su distancia de tiro, dispara y se aparta.",
            _ => string.Empty,
        };
    }
}
