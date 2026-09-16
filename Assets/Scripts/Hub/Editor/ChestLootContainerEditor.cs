using System.Collections.Generic;
using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Hub.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="ChestLootContainer"/>. Sustituye el campo de arrastrar-y-soltar del
    /// arma forzada por un <b>desplegable</b> con todas las armas del juego, cuya primera opción es
    /// "Aleatoria". Copiado de la extinta <c>AbilityChestEditor</c> (ver <c>Legacy/</c>), sólo
    /// retargeteado a la clase nueva — el desplegable en sí no tenía nada específico del cofre de
    /// un solo toque.
    /// </summary>
    [CustomEditor(typeof(ChestLootContainer))]
    public class ChestLootContainerEditor : Editor
    {
        private const string RandomLabel = "Aleatoria (como en el juego)";

        private List<WeaponDefinition> _weapons;
        private string[] _options;

        private void OnEnable() => RefreshOptions();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.FindProperty("forcedWeapon");

            using (new EditorGUILayout.HorizontalScope())
            {
                int current = IndexOf(property.objectReferenceValue as WeaponDefinition);
                int next = EditorGUILayout.Popup("Arma del cofre", current, _options);

                if (next != current)
                    property.objectReferenceValue = next == 0 ? null : _weapons[next - 1];

                // Botón de refresco: si se crean assets con el Inspector abierto, la lista cacheada
                // se queda corta y no hay forma de saberlo desde aquí.
                if (GUILayout.Button("↻", GUILayout.Width(26))) RefreshOptions();
            }

            if (property.objectReferenceValue is WeaponDefinition chosen)
                EditorGUILayout.HelpBox(
                    $"{chosen.DisplayName}\n{chosen.BaseDamage:0} dmg · {chosen.BaseCooldown:0.00}s",
                    MessageType.None);

            DrawPropertiesExcluding(serializedObject, "forcedWeapon", "m_Script");

            serializedObject.ApplyModifiedProperties();
        }

        private void RefreshOptions()
        {
            WeaponLibrary.Invalidate();

            _weapons = new List<WeaponDefinition>(WeaponLibrary.All);
            _options = new string[_weapons.Count + 1];
            _options[0] = RandomLabel;

            for (int i = 0; i < _weapons.Count; i++)
                _options[i + 1] = ElementPrefix(_weapons[i].InnateElement) + _weapons[i].DisplayName;
        }

        private int IndexOf(WeaponDefinition weapon)
        {
            if (weapon == null) return 0;

            int index = _weapons.IndexOf(weapon);

            // Un arma asignada que ya no está en la lista (asset movido fuera de Resources) se
            // refresca una vez antes de darla por perdida.
            if (index < 0)
            {
                RefreshOptions();
                index = _weapons.IndexOf(weapon);
            }

            return index < 0 ? 0 : index + 1;
        }

        /// <summary>Agrupa visualmente el desplegable por elemento innato usando submenús de Unity.</summary>
        private static string ElementPrefix(ElementId element) => element switch
        {
            ElementId.Ice => "Hielo/",
            ElementId.Fire => "Fuego/",
            _ => "Físico/",
        };
    }
}
