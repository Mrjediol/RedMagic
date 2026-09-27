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
        public override void OnInspectorGUI()
        {
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

            bool sleeps = moves && tuning.FindPropertyRelative("sleepsUntilDetected").boolValue;
            bool explodes = tuning.FindPropertyRelative("selfDestruct").boolValue;

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

            var iterator = tuning.Copy();
            var end = iterator.GetEndProperty();
            bool first = true;

            while (iterator.NextVisible(first) && !SerializedProperty.EqualContents(iterator, end))
            {
                first = false;
                if (Hidden(iterator.name, flies, ranged && !explodes, moves, sleeps, explodes)) continue;

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Aplicar a Health / Knockback / cuerpo"))
                foreach (var t in targets) ((EnemyStats)t).Apply();
        }

        private static bool Hidden(string field, bool flies, bool ranged, bool moves, bool sleeps,
                                   bool explodes)
        {
            switch (field)
            {
                case "sleepsUntilDetected":
                    return !moves;

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

                // Sólo un enemigo que se mueve tiene detección, persecución y bordes.
                case "detectionRange":
                case "loseInterestGrace":
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
