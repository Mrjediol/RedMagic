using RedMagic.Fx;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Gameplay.EditorTools
{
    /// <summary>
    /// Escribe el combo por defecto (5 golpes, 3 desbloqueados) y el golpe de dash en <c>PlayerAttack</c> de <c>Player.prefab</c>,
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

        [MenuItem("Tools/RedMagic/FX/Tajo melee · Combo del jugador (5 golpes + dash, tajo C)")]
        public static void Setup()
        {
            var slash = AssetDatabase.LoadAssetAtPath<MeleeSlashFx>(SlashPath);
            if (slash == null) { Debug.LogError($"[PlayerComboSetup] Falta {SlashPath}."); return; }

            var hits = new[]
            {
                new HitDef { label = "1 · Descendente", rotation = 0f, reverse = false, power = 1.05f,
                    offset = new Vector2(0.25f, 0.15f), stretch = new Vector2(1.05f, 1.25f), damage = 1f, knockback = 0.6f, windup = 0.03f, recovery = 0.32f },
                new HitDef { label = "2 · Ascendente", rotation = 15f, reverse = true, power = 1.05f,
                    offset = new Vector2(0.25f, 0.05f), stretch = new Vector2(1.05f, 1.25f), damage = 1f, knockback = 0.6f, windup = 0.04f, recovery = 0.34f },
                new HitDef { label = "3 · Diagonal amplio", rotation = 35f, reverse = false, power = 1.15f,
                    offset = new Vector2(0.35f, 0.15f), stretch = new Vector2(1.3f, 1.1f), damage = 1.6f, knockback = 2f, windup = 0.12f, recovery = 0.42f },
                new HitDef { label = "4 · Vertical ascendente", rotation = 0f, reverse = true, power = 1f,
                    offset = new Vector2(0.7f, 0.6f), stretch = new Vector2(1.15f, 1.45f), damage = 1.2f, knockback = 1f, windup = 0.05f, recovery = 0.34f },
                new HitDef { label = "5 · Gran remate", rotation = -20f, reverse = false, power = 1.45f,
                    offset = new Vector2(0.6f, 0.4f), stretch = new Vector2(1.25f, 1f), damage = 2.6f, knockback = 3f, windup = 0.18f, recovery = 0.5f },
            };

            var dash = new HitDef { label = "Dash", rotation = 0f, reverse = false, power = 0.95f,
                offset = new Vector2(0.7f, 0f), stretch = new Vector2(2f, 0.45f), damage = 1.4f, knockback = 1.5f, windup = 0f, recovery = 0.25f };

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
                    Write(combo.GetArrayElementAtIndex(i), hits[i], slash);
                }
                Write(so.FindProperty("dashHit"), dash, slash);
                // unlockedHits no se toca: lo decide quien prueba (3 por defecto; 4.º y 5.º con items).
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
                Debug.Log($"[PlayerComboSetup] Combo de {hits.Length} golpes escrito en {PlayerPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Write(SerializedProperty e, HitDef h, MeleeSlashFx slash)
        {
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
    }
}
