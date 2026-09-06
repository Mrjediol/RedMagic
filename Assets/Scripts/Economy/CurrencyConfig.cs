using System;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Asset único de configuración de la economía: el aspecto de cada moneda (icono, nombre,
    /// color) y cuánto suelta cada tipo de enemigo al morir.
    ///
    /// Vive en <c>Assets/Resources/CurrencyConfig.asset</c> y lo cargan
    /// <see cref="CurrencyManager"/> (para las tablas de drop) y <c>CurrencyHud</c> (para los
    /// iconos) con <c>Resources.Load</c>, así que no hay que arrastrarlo a ninguna escena. Para
    /// balancear los drops se edita este asset en el Inspector — es el "Currency Manager" a
    /// efectos de tocar números.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencyConfig", menuName = "RedMagic/Currency Config")]
    public class CurrencyConfig : ScriptableObject
    {
        /// <summary>Rango [min, max] cerrado del que se saca una cantidad aleatoria de moneda.</summary>
        [Serializable]
        public struct DropRange
        {
            [Min(0)] public int min;
            [Min(0)] public int max;

            /// <summary>Cantidad aleatoria dentro del rango. min &gt;= max devuelve min (o 0).</summary>
            public readonly int Roll()
            {
                int lo = Mathf.Max(0, min);
                int hi = Mathf.Max(lo, max);
                return hi <= lo ? lo : UnityEngine.Random.Range(lo, hi + 1);
            }
        }

        /// <summary>Lo que suelta un enemigo de un tier concreto: un rango por moneda.</summary>
        [Serializable]
        public class TierDrops
        {
            public DropRange gold;
            public DropRange diamond;
            public DropRange soulFragment;
            public DropRange skull;

            public DropRange RangeFor(Currency currency) => currency switch
            {
                Currency.Gold => gold,
                Currency.Diamond => diamond,
                Currency.SoulFragment => soulFragment,
                Currency.Skull => skull,
                _ => default
            };
        }

        /// <summary>Aspecto de una moneda en la UI.</summary>
        [Serializable]
        public class Visual
        {
            public Currency currency;
            public Sprite icon;
            [Tooltip("Nombre visible (tooltips, tiendas). El HUD sólo muestra el icono y el número.")]
            public string label;
            public Color tint = Color.white;
        }

        [Header("Aspecto (una entrada por moneda, en el orden en que se muestran en el HUD)")]
        [SerializeField] private Visual[] visuals = Array.Empty<Visual>();

        [Header("Drops por tier de enemigo")]
        [Tooltip("Enemigo común. Pensado para oro + fragmentos de alma (deja diamante y calavera a 0).")]
        [SerializeField] private TierDrops basic = new();

        [Tooltip("Enemigo de élite: de todo, pero por debajo del jefe.")]
        [SerializeField] private TierDrops elite = new();

        [Tooltip("Jefe: de todo, en las cantidades más altas.")]
        [SerializeField] private TierDrops boss = new();

        /// <summary>Las monedas en el orden en que se declararon en <see cref="visuals"/>.</summary>
        public Visual[] Visuals => visuals;

        public TierDrops DropsFor(EnemyTier tier) => tier switch
        {
            EnemyTier.Basic => basic,
            EnemyTier.Elite => elite,
            EnemyTier.Boss => boss,
            _ => basic
        };

        public Visual VisualFor(Currency currency)
        {
            foreach (var v in visuals)
                if (v != null && v.currency == currency) return v;
            return null;
        }
    }
}
