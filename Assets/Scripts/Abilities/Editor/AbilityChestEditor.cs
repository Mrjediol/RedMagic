using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Abilities.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="AbilityChest"/>. Sustituye el campo de arrastrar-y-soltar de la
    /// habilidad forzada por un <b>desplegable</b> con todas las habilidades del juego, cuya
    /// primera opción es "Aleatoria".
    ///
    /// Existe por comodidad de pruebas: elegir qué suelta el cofre es lo que se toca a cada rato
    /// mientras se comparan habilidades, y buscar el asset a mano en el proyecto cada vez es un
    /// engorro. La lista sale de <see cref="AbilityLibrary"/>, así que las habilidades nuevas
    /// aparecen solas sin tocar este archivo.
    /// </summary>
    [CustomEditor(typeof(AbilityChest))]
    public class AbilityChestEditor : Editor
    {
        private const string RandomLabel = "Aleatoria (como en el juego)";

        private List<AbilityDefinition> _abilities;
        private string[] _options;

        private void OnEnable() => RefreshOptions();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.FindProperty("forcedAbility");

            using (new EditorGUILayout.HorizontalScope())
            {
                int current = IndexOf(property.objectReferenceValue as AbilityDefinition);
                int next = EditorGUILayout.Popup("Habilidad del cofre", current, _options);

                if (next != current)
                    property.objectReferenceValue = next == 0 ? null : _abilities[next - 1];

                // Botón de refresco: si se crean assets con el Inspector abierto, la lista cacheada
                // se queda corta y no hay forma de saberlo desde aquí.
                if (GUILayout.Button("↻", GUILayout.Width(26))) RefreshOptions();
            }

            if (property.objectReferenceValue is AbilityDefinition chosen)
                EditorGUILayout.HelpBox(chosen.DisplayName + "\n" + chosen.ShortStats(), MessageType.None);

            DrawPropertiesExcluding(serializedObject, "forcedAbility", "m_Script");

            serializedObject.ApplyModifiedProperties();
        }

        private void RefreshOptions()
        {
            AbilityLibrary.Invalidate();

            _abilities = new List<AbilityDefinition>(AbilityLibrary.All);
            _options = new string[_abilities.Count + 1];
            _options[0] = RandomLabel;

            for (int i = 0; i < _abilities.Count; i++)
                _options[i + 1] = CategoryPrefix(_abilities[i].Category) + _abilities[i].DisplayName;
        }

        private int IndexOf(AbilityDefinition ability)
        {
            if (ability == null) return 0;

            int index = _abilities.IndexOf(ability);

            // Una habilidad asignada que ya no está en la lista (asset movido fuera de Resources)
            // se refresca una vez antes de darla por perdida.
            if (index < 0)
            {
                RefreshOptions();
                index = _abilities.IndexOf(ability);
            }

            return index < 0 ? 0 : index + 1;
        }

        /// <summary>Agrupa visualmente el desplegable por familia usando submenús de Unity.</summary>
        private static string CategoryPrefix(AbilityCategory category) => category switch
        {
            AbilityCategory.Melee => "Cuerpo a cuerpo/",
            AbilityCategory.Ranged => "A distancia/",
            AbilityCategory.Area => "Área/",
            _ => "Utilidad/"
        };
    }
}
