using UnityEditor;
using UnityEngine;

namespace RedMagic.Items.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Items ▸ Cometa · Crear o actualizar arma</b>: crea
    /// <c>Assets/Resources/Items/Weapons/Weapon_Cometa.asset</c> con los valores del boceto. Si ya existe sólo
    /// asegura lo imprescindible (entrega Comet y materiales de los efectos) y respeta lo que hayas afinado.
    /// </summary>
    public static class CometWeaponSetup
    {
        public const string AssetPath = "Assets/Resources/Items/Weapons/Weapon_Cometa.asset";
        private const string FxMaterial = "Assets/Art/Fx/Materials/Fx_VertexColor.mat";
        private const string SilhouetteMaterial = "Assets/Art/Fx/Materials/Fx_SpriteSilhouette.mat";

        [MenuItem("Tools/RedMagic/Items/Cometa · Crear o actualizar arma")]
        public static WeaponDefinition Run()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetPath);
            bool created = weapon == null;
            if (created)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, AssetPath);
            }

            var so = new SerializedObject(weapon);
            if (created)
            {
                Str(so, "textKey", "weapon.cometa");
                Str(so, "displayName", "Cometa");
                Str(so, "description", "Mantén para cargar un orbe y suéltalo: sale un cometa de luz.");
                so.FindProperty("accent").colorValue = new Color(0.16f, 0.76f, 1f);
                Num(so, "baseDamage", 14f);
                Num(so, "baseCooldown", 0.35f);
                Num(so, "baseKnockbackMultiplier", 1f);
                Num(so, "baseShot.speed", 13f);
                Num(so, "baseShot.lifetime", 2.5f);
                so.FindProperty("baseShot.size").vector2Value = new Vector2(0.4f, 0.4f);
                so.FindProperty("baseShot.muzzleOffset").vector2Value = new Vector2(0.6f, 0.15f);
                so.FindProperty("baseShot.aimMode").enumValueIndex = (int)Gameplay.ProjectileAimMode.Fixed;   // por defecto recto; Mouse Direction / Nearest Enemy o CometSettings.aimAtCursor lo apuntan
                Num(so, "baseShot.chargeTime", 0.55f);
                Num(so, "baseShot.minChargeToFire", 0.25f);
                Num(so, "baseShot.minChargeDamage", 0.5f);
            }

            so.FindProperty("baseShot.delivery").enumValueIndex = (int)ShotDelivery.Comet;
            if (so.FindProperty("baseShot.chargeTime").floatValue <= 0f) Num(so, "baseShot.chargeTime", 0.55f);
            so.FindProperty("comet.fxMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FxMaterial);
            so.FindProperty("comet.silhouetteMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterial);
            AssignSounds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cometa] {(created ? "Creada" : "Actualizada")} {AssetPath}.", weapon);
            return weapon;
        }

        /// <summary>
        /// Los sonidos sintetizados del boceto (<c>docs/bocetos/render-sonidos-cometa.cjs</c>) en sus huecos, sólo
        /// si están vacíos: lo que pongas a mano no se pisa.
        /// </summary>
        private static void AssignSounds(SerializedObject so)
        {
            const string dir = "Assets/Audio/Sounds/Player/Comet/";
            Clip(so, "chargeSound", dir + "comet_charge.wav");
            Clip(so, "fireSound", dir + "comet_fire.wav");
            Clip(so, "impactSound", dir + "comet_impact.wav");
            Clip(so, "comet.readySound", dir + "comet_ready.wav");
            Clip(so, "comet.holdLoopSound", dir + "comet_hold_loop.wav");
            Clip(so, "comet.pierceEntrySound", dir + "comet_pierce_in.wav");
            Clip(so, "comet.pierceExitSound", dir + "comet_pierce_out.wav");
            Clip(so, "comet.chainStickSound", dir + "comet_chain_stick.wav");
            Clip(so, "comet.chainReleaseSound", dir + "comet_chain_release.wav");
        }

        private static void Clip(SerializedObject so, string cue, string clipPath)
        {
            var clips = so.FindProperty(cue + ".clips");
            if (clips == null || clips.arraySize > 0) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null) return;
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            var init = so.FindProperty(cue + ".initialized");
            if (init != null) init.boolValue = true;
        }

        private static void Str(SerializedObject so, string path, string value) => so.FindProperty(path).stringValue = value;
        private static void Num(SerializedObject so, string path, float value) => so.FindProperty(path).floatValue = value;
    }
}
