using RedMagic.Fx;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Gameplay.EditorTools
{
    /// <summary>
    /// Escribe el combo de 3 golpes por defecto en <c>PlayerAttack</c> de <c>Player.prefab</c>,
    /// con el tajo C (Tormenta). Sobrescribe la lista: punto de partida, el ajuste fino se hace
    /// después en el Inspector del prefab.
    /// </summary>
    public static class PlayerComboSetup
    {
        private const string PlayerPath = "Assets/Prefabs/Player.prefab";
        private const string SlashPath = "Assets/Prefabs/Fx/Player/Slash/Fx_Slash_C_Tormenta.prefab";

        private struct HitDef
        {
            public string label; public float rotation; public bool reverse; public float power;
            public Vector2 offset, stretch; public float damage, knockback, windup, recovery;
        }

        [MenuItem("Tools/RedMagic/FX/Tajo melee · Combo del jugador (3 golpes, tajo C)")]
        public static void Setup()
        {
            var slash = AssetDatabase.LoadAssetAtPath<MeleeSlashFx>(SlashPath);
            if (slash == null) { Debug.LogError($"[PlayerComboSetup] Falta {SlashPath}."); return; }

            var hits = new[]
            {
                new HitDef { label = "1 · Descendente", rotation = 0f, reverse = false, power = 0.85f,
                    offset = new Vector2(0.3f, 0.3f), stretch = Vector2.one, damage = 1f, knockback = 0.6f, windup = 0.03f, recovery = 0.32f },
                new HitDef { label = "2 · Ascendente", rotation = 15f, reverse = true, power = 0.9f,
                    offset = new Vector2(0.3f, 0.1f), stretch = Vector2.one, damage = 1f, knockback = 0.6f, windup = 0.04f, recovery = 0.34f },
                new HitDef { label = "3 · Remate horizontal", rotation = 0f, reverse = false, power = 1.15f,
                    offset = new Vector2(0.9f, 0.1f), stretch = new Vector2(1.7f, 0.65f), damage = 1.8f, knockback = 2.2f, windup = 0.14f, recovery = 0.45f },
            };

            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var attack = root.GetComponent<PlayerAttack>();
                if (attack == null) { Debug.LogError("[PlayerComboSetup] Player.prefab no tiene PlayerAttack."); return; }

                var so = new SerializedObject(attack);
                var combo = so.FindProperty("combo");
                combo.arraySize = hits.Length;
                for (int i = 0; i < hits.Length; i++)
                {
                    var e = combo.GetArrayElementAtIndex(i);
                    var h = hits[i];
                    e.FindPropertyRelative("label").stringValue = h.label;
                    e.FindPropertyRelative("slash").objectReferenceValue = slash;
                    e.FindPropertyRelative("rotation").floatValue = h.rotation;
                    e.FindPropertyRelative("reverse").boolValue = h.reverse;
                    e.FindPropertyRelative("power").floatValue = h.power;
                    e.FindPropertyRelative("offset").vector2Value = h.offset;
                    e.FindPropertyRelative("stretch").vector2Value = h.stretch;
                    e.FindPropertyRelative("damageMultiplier").floatValue = h.damage;
                    e.FindPropertyRelative("knockbackMultiplier").floatValue = h.knockback;
                    e.FindPropertyRelative("windup").floatValue = h.windup;
                    e.FindPropertyRelative("recovery").floatValue = h.recovery;
                }
                so.FindProperty("unlockedHits").intValue = hits.Length;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
                Debug.Log($"[PlayerComboSetup] Combo de {hits.Length} golpes escrito en {PlayerPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
