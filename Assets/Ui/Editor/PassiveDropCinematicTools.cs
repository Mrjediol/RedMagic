using RedMagic.Economy;
using UnityEditor;
using UnityEngine;

namespace RedMagic.UI.EditorTools
{
    /// <summary>
    /// Menús de <see cref="PassiveDropCinematic"/>: abrir (creándolo si falta) el asset de ajustes
    /// y reproducir la cinemática en Play con una pasiva al azar, para afinar tiempos a ojo.
    /// </summary>
    public static class PassiveDropCinematicTools
    {
        private const string SettingsPath = "Assets/Resources/" + PassiveDropCinematicSettings.ResourcePath + ".asset";

        [MenuItem("Tools/RedMagic/Hub/Espejo · Ajustes de cinemática de pasiva", priority = 411)]
        public static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PassiveDropCinematicSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PassiveDropCinematicSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/RedMagic/Hub/Espejo · Probar cinemática de pasiva (Play)", priority = 412)]
        public static void Preview()
        {
            var all = LegendaryPassiveLibrary.BySlot;
            LegendaryPassive pick = null;
            for (int tries = 0; tries < 20 && pick == null; tries++) pick = all[Random.Range(0, all.Count)];
            if (pick != null) PassiveDropCinematic.Show(pick);
            else PassiveDropCinematic.Show("Pasiva de prueba", null);
        }

        [MenuItem("Tools/RedMagic/Hub/Espejo · Probar cinemática de pasiva (Play)", true)]
        private static bool PreviewValid() => Application.isPlaying;
    }
}
