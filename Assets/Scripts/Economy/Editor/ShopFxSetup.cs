using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Economy.EditorTools
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Tienda ▸ Generar FX de tienda</b>: crea lo que falte para los efectos de
    /// la tienda — el material aditivo (<c>RedMagic/Sprite Additive</c>), <c>ShopFxConfig</c> e
    /// <c>ItemRarityColors</c> en Resources — y enlaza el material en la config. Idempotente: nunca
    /// pisa valores ya afinados.
    /// </summary>
    public static class ShopFxSetup
    {
        private const string MaterialFolder = "Assets/Art/Fx/Materials";
        private const string MaterialPath = MaterialFolder + "/Fx_SpriteAdditive.mat";
        private const string ConfigPath = "Assets/Resources/" + ShopFxConfig.ResourcePath + ".asset";
        private const string ColorsPath = "Assets/Resources/" + ItemRarityColors.ResourcePath + ".asset";

        [MenuItem("Tools/RedMagic/Tienda/Generar FX de tienda", priority = 421)]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Fx")) AssetDatabase.CreateFolder("Assets/Art", "Fx");
                AssetDatabase.CreateFolder("Assets/Art/Fx", "Materials");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("RedMagic/Sprite Additive");
                if (shader == null)
                {
                    Debug.LogError("[ShopFxSetup] No se encuentra el shader 'RedMagic/Sprite Additive'.");
                    return;
                }

                material = new Material(shader) { name = "Fx_SpriteAdditive" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<ShopFxConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ShopFxConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.additiveMaterial == null)
            {
                config.additiveMaterial = material;
                EditorUtility.SetDirty(config);
            }

            if (AssetDatabase.LoadAssetAtPath<ItemRarityColors>(ColorsPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ItemRarityColors>(), ColorsPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[ShopFxSetup] Material aditivo, ShopFxConfig e ItemRarityColors listos.");
        }
    }
}
