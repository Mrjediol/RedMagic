using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Texto de los efectos de umbral (sección 6 del documento de diseño): qué da cada sinergia al
    /// llegar a 2 / 4 / 6 puntos. Un ScriptableObject en <c>Assets/Resources/SynergyConfig.asset</c>
    /// para poder reescribirlo sin tocar código.
    ///
    /// <b>Sólo son descripciones</b> — este paso no implementa los efectos. Cuando se implementen,
    /// este mismo asset será su fuente de verdad (y aquí se añadirán los números que hoy son sólo
    /// prosa).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Synergy Config", fileName = "SynergyConfig")]
    public class SynergyConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public BuildTag tag;

            [Tooltip("Efecto al alcanzar el umbral 2.")]
            [TextArea(2, 3)] public string tier1;

            [Tooltip("Efecto al alcanzar el umbral 4.")]
            [TextArea(2, 3)] public string tier2;

            [Tooltip("Efecto al alcanzar el umbral 6.")]
            [TextArea(2, 3)] public string tier3;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Entry For(BuildTag tag) => entries.Find(e => e.tag == tag);

        /// <summary>Descripción del tramo <paramref name="tier"/> (1..3), o null si no está definido.</summary>
        public string Tier(BuildTag tag, int tier)
        {
            var entry = For(tag);
            if (entry == null) return null;
            return tier switch
            {
                1 => entry.tier1,
                2 => entry.tier2,
                3 => entry.tier3,
                _ => null,
            };
        }

        private static SynergyConfig _instance;

        /// <summary>El asset de <c>Resources/SynergyConfig</c>. Puede ser null si falta.</summary>
        public static SynergyConfig Instance =>
            _instance != null ? _instance : _instance = Resources.Load<SynergyConfig>("SynergyConfig");
    }
}
