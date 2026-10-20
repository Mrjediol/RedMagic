using System.Collections.Generic;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Números y precios de las mejoras permanentes de armas (<see cref="WeaponMastery"/>), en
    /// <c>Resources/WeaponMasteryConfig.asset</c>. Precios en fragmentos de alma.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Armas/Mejoras permanentes", fileName = "WeaponMasteryConfig")]
    public class WeaponMasteryConfig : ScriptableObject
    {
        [Tooltip("Armas con pestaña en el menú de mejoras del hub, en este orden.")]
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();

        [Header("Daño")]
        [Tooltip("+ daño por nivel (0,08 = +8 %).")] [Min(0f)] public float damagePerLevel = 0.08f;
        [Min(1)] public int damageLevels = 5;
        [Min(0)] public int damageCost = 15;
        [Tooltip("Lo que sube el precio por nivel ya comprado.")] [Min(0)] public int damageCostStep = 10;

        [Header("Recarga")]
        [Tooltip("− recarga por nivel (0,05 = −5 %).")] [Min(0f)] public float cooldownPerLevel = 0.05f;
        [Min(1)] public int cooldownLevels = 4;
        [Min(0)] public int cooldownCost = 20;
        [Min(0)] public int cooldownCostStep = 12;

        [Header("Firma (la estadística propia de cada arma)")]
        [Min(0)] public int signatureCost = 40;
        [Min(0)] public int signatureCostStep = 30;

        [Header("Mejoras de evolución (la 1.ª de cada evolución viene de serie)")]
        [Min(0)] public int unlockSecondCost = 60;
        [Min(0)] public int unlockThirdCost = 90;

        private static WeaponMasteryConfig _instance;

        public static WeaponMasteryConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<WeaponMasteryConfig>("WeaponMasteryConfig");
                if (_instance == null)
                {
                    Debug.LogWarning("[Arsenal] No se carga Resources/WeaponMasteryConfig: el menú no tendrá pestañas de armas.");
                    _instance = CreateInstance<WeaponMasteryConfig>();   // valores por defecto
                }
                return _instance;
            }
        }
    }
}
