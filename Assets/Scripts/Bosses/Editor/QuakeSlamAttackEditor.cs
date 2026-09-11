using UnityEditor;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Oculta el 'telegraph' heredado: en este arquetipo el tiempo lo manda <c>impactDelay</c>
    /// (<see cref="QuakeSlamAttack.Telegraph"/> lo devuelve), y un segundo campo de tiempo que no
    /// hace nada sólo confundiría.
    /// </summary>
    [CustomEditor(typeof(QuakeSlamAttack))]
    public class QuakeSlamAttackEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "telegraph");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(((BossAttack)target).ShortStats(), MessageType.None);
        }
    }
}
