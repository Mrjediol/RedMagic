using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Fx.EditorTools
{
    /// <summary>
    /// Prefabs del dash y del picado dibujados por código (<see cref="DashTrailFx"/>,
    /// <see cref="ImpactBurstFx"/>) en <c>Assets/Prefabs/Fx/Player/Dash/</c>, y los asigna al
    /// jugador: dash (versión "sustituye"), impacto al atravesar enemigos, picado e impacto en el
    /// suelo. Sobrescribe los prefabs; el ajuste fino va en su Inspector.
    /// </summary>
    public static class DashFxPack
    {
        private const string Folder = "Assets/Prefabs/Fx/Player/Dash";
        private const string MaterialPath = "Assets/Art/Fx/Materials/Fx_VertexColor.mat";
        private const string PlayerPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Tools/RedMagic/FX/Dash y picado · Generar prefabs y asignar al jugador")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Fx/Player", "Dash");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("RedMagic/Fx Vertex Color");
                if (shader == null) { Debug.LogError("[DashFxPack] Falta el shader RedMagic/Fx Vertex Color."); return; }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            var replace = Build<DashTrailFx>("Fx_DashTrail_Sustituye", mat, so =>
            {
                so.FindProperty("replaceTarget").boolValue = true;
                so.FindProperty("shockSize").floatValue = 1.15f;
            });
            Build<DashTrailFx>("Fx_DashTrail_Encima", mat, so =>
            {
                so.FindProperty("replaceTarget").boolValue = false;
                so.FindProperty("shockSize").floatValue = 1.15f;
            });
            var dive = Build<DashTrailFx>("Fx_DiveTrail", mat, so =>
            {
                so.FindProperty("replaceTarget").boolValue = true;
                so.FindProperty("shockSize").floatValue = 1.5f;
                so.FindProperty("capsuleThickness").floatValue = 28f;
            });
            var groundImpact = Build<ImpactBurstFx>("Fx_GroundImpact", mat, so =>
            {
                so.FindProperty("ground").boolValue = true;
                so.FindProperty("pixel").floatValue = 0.021f;
            });
            var hitImpact = Build<ImpactBurstFx>("Fx_EnemyHitImpact", mat, so =>
            {
                so.FindProperty("ground").boolValue = false;
                so.FindProperty("duration").floatValue = 0.45f;
                so.FindProperty("spikes").intValue = 10;
                so.FindProperty("sparks").intValue = 12;
                so.FindProperty("shakeAmplitude").floatValue = 0f;
            });

            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var vfx = new SerializedObject(root.GetComponent<PlayerVfx>());
                vfx.FindProperty("dashEffect").objectReferenceValue = replace;
                vfx.FindProperty("dashEffectOffset").vector2Value = Vector2.zero;
                vfx.FindProperty("dashEffectSize").floatValue = 1f;
                vfx.ApplyModifiedPropertiesWithoutUndo();

                var dash = root.GetComponent<PlayerDashCombat>();
                if (dash != null)
                {
                    var so = new SerializedObject(dash);
                    so.FindProperty("hitEffect").objectReferenceValue = hitImpact;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var attack = new SerializedObject(root.GetComponent<PlayerAttack>());
                attack.FindProperty("diveTrail").objectReferenceValue = dive;
                attack.FindProperty("diveGroundImpact").objectReferenceValue = groundImpact;
                attack.FindProperty("diveHitEffect").objectReferenceValue = hitImpact;
                attack.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[DashFxPack] Prefabs en {Folder} y asignados a {PlayerPath}.");
        }

        private static GameObject Build<T>(string name, Material mat, System.Action<SerializedObject> setup) where T : Component
        {
            var go = new GameObject(name);
            var c = go.AddComponent<T>();
            var so = new SerializedObject(c);
            so.FindProperty("material").objectReferenceValue = mat;
            setup(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
