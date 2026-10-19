using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Arsenal.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Arsenal ▸ Preparar</b>: deja listo todo lo que las armas del arsenal cargan solas:
    /// <list type="bullet">
    /// <item>el material aditivo de los brillos (<c>Fx_VertexColorAdditive.mat</c>, junto al normal);</item>
    /// <item><c>Resources/Arsenal/ArsenalAssets.asset</c> con los dos materiales (y las reglas comunes, que no se pisan);</item>
    /// <item>un <see cref="WeaponDefinition"/> por cada tipo de kit en <c>Assets/Test/Arsenal/</c> (<c>Weapon_Arsenal_&lt;id&gt;</c>):
    /// si ya existe sólo asegura la entrega Arsenal y el kit de su tipo, respetando lo afinado.</item>
    /// </list>
    /// </summary>
    public static class ArsenalSetup
    {
        public const string WeaponsFolder = "Assets/Test/Arsenal";
        private const string SolidMat = "Assets/Art/Fx/Materials/Fx_VertexColor.mat";
        private const string GlowMat = "Assets/Art/Fx/Materials/Fx_VertexColorAdditive.mat";
        private const string AssetsPath = "Assets/Resources/Arsenal/ArsenalAssets.asset";
        /// <summary>WAV de las armas (tools/render-sonidos-armas.cjs): el nombre del archivo es el id de ArsenalSounds.</summary>
        public const string SoundsFolder = "Assets/Audio/Sounds/Player/Arsenal";

        [MenuItem("Tools/RedMagic/Arsenal/Preparar (materiales, assets y armas)")]
        public static List<WeaponDefinition> Run()
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMat);
            if (glow == null)
            {
                var shader = Shader.Find("RedMagic/Fx Vertex Color Additive");
                if (shader == null) { Debug.LogError("[Arsenal] No encuentro el shader RedMagic/Fx Vertex Color Additive."); return null; }
                glow = new Material(shader) { name = "Fx_VertexColorAdditive" };
                AssetDatabase.CreateAsset(glow, GlowMat);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetsPath));
            var assets = AssetDatabase.LoadAssetAtPath<ArsenalAssets>(AssetsPath);
            if (assets == null) { assets = ScriptableObject.CreateInstance<ArsenalAssets>(); AssetDatabase.CreateAsset(assets, AssetsPath); }
            assets.solidMaterial = AssetDatabase.LoadAssetAtPath<Material>(SolidMat);
            assets.glowMaterial = glow;
            AssignSounds(assets);
            EditorUtility.SetDirty(assets);

            Directory.CreateDirectory(WeaponsFolder);
            var weapons = new List<WeaponDefinition>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ArsenalKit>())
            {
                if (type.IsAbstract) continue;
                var kit = (ArsenalKit)Activator.CreateInstance(type);
                weapons.Add(EnsureWeapon(kit));
            }
            weapons.Sort((a, b) => Order(a).CompareTo(Order(b)));
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arsenal] Preparado: {weapons.Count} armas en {WeaponsFolder}.");
            return weapons;
        }

        /// <summary>Cada WAV de <see cref="SoundsFolder"/> entra en la tabla por su nombre; lo que ya tenga clip no se toca.</summary>
        private static void AssignSounds(ArsenalAssets assets)
        {
            if (!AssetDatabase.IsValidFolder(SoundsFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsFolder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                var entry = assets.sounds.Find(s => s != null && s.id == clip.name);
                if (entry == null) { entry = new ArsenalSoundEntry { id = clip.name }; assets.sounds.Add(entry); }
                if (entry.cue.HasClips) continue;
                bool loop = clip.name.EndsWith("_loop") || clip.name == "moon_spin";
                entry.cue.clips = new[] { clip };
                entry.cue.volume = loop ? 0.55f : 0.8f;
                entry.cue.positional = !loop;
                entry.cue.pitchMin = loop ? 1f : 0.96f; entry.cue.pitchMax = loop ? 1f : 1.04f;
            }
        }

        private static readonly string[] OrderIds = { "fan", "sing", "ray", "moon", "seed", "star" };

        private static int Order(WeaponDefinition w)
        {
            var k = w != null ? w.ArsenalKitAsset : null;
            int i = k != null ? Array.IndexOf(OrderIds, k.WeaponId) : -1;
            return i < 0 ? 99 : i;
        }

        private static WeaponDefinition EnsureWeapon(ArsenalKit fresh)
        {
            string path = $"{WeaponsFolder}/Weapon_Arsenal_{fresh.WeaponId}.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            bool created = weapon == null;
            if (created) { weapon = ScriptableObject.CreateInstance<WeaponDefinition>(); AssetDatabase.CreateAsset(weapon, path); }

            var so = new SerializedObject(weapon);
            if (created)
            {
                so.FindProperty("textKey").stringValue = "weapon.arsenal_" + fresh.WeaponId;
                so.FindProperty("displayName").stringValue = fresh.Tree.evolutions[0].name == "Base" ? fresh.WeaponId : fresh.Tree.evolutions[0].name;
                so.FindProperty("baseShot.muzzleOffset").vector2Value = new Vector2(0.5f, 0.3f);
                so.FindProperty("baseCooldown").floatValue = 0.5f;
            }
            so.FindProperty("baseShot.delivery").enumValueIndex = (int)ShotDelivery.Arsenal;
            var kitProp = so.FindProperty("arsenal");
            if (kitProp.managedReferenceValue == null || kitProp.managedReferenceValue.GetType() != fresh.GetType()) kitProp.managedReferenceValue = fresh;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }
    }
}
