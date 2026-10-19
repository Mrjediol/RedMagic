using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Arsenal
{
    public enum ArsenalElement { None, Ice, Fire }

    /// <summary>
    /// Lo que la build de la run aporta a las armas del arsenal (vía 3 de <c>docs/armas/progresion.md</c>): el
    /// <b>elemento</b> que aplican (con 4 puntos de ese elemento; sólo uno puede llegar) y el <b>Duplicador</b>. Cada arma
    /// decide cómo los expresa. El menú DEV puede forzarlos para probar sin montar la build.
    /// </summary>
    public static class ArsenalBuild
    {
        public enum ElementMode { Auto, None, Ice, Fire }

        /// <summary>DEV: forzar elemento (Auto = el de la build).</summary>
        public static ElementMode ElementOverride = ElementMode.Auto;

        /// <summary>DEV: Duplicador encendido (en el juego aún no hay item que lo dé).</summary>
        public static bool Duplicator;

        /// <summary>Reacciones elementales (añicos, deshielo, vapor). Interruptor de pruebas, como en la web.</summary>
        public static bool Reactions = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ElementOverride = ElementMode.Auto;
            Duplicator = false;
            Reactions = true;
        }

        /// <summary>Elemento que aplican ahora los golpes de las armas del arsenal.</summary>
        public static ArsenalElement Element
        {
            get
            {
                switch (ElementOverride)
                {
                    case ElementMode.None: return ArsenalElement.None;
                    case ElementMode.Ice: return ArsenalElement.Ice;
                    case ElementMode.Fire: return ArsenalElement.Fire;
                }
                int ice = Points(BuildTag.Ice), fire = Points(BuildTag.Fire);
                if (ice >= 4 && ice >= fire) return ArsenalElement.Ice;
                if (fire >= 4) return ArsenalElement.Fire;
                return ArsenalElement.None;
            }
        }

        /// <summary>Cuántas copias de cada proyectil (Duplicador = 2).</summary>
        public static int Copies => Duplicator ? 2 : 1;

        /// <summary>Ritmo al que corren las recargas (items de enfriamiento del juego), igual que <see cref="WeaponUser"/>.</summary>
        public static float CooldownRate => Mathf.Max(0.01f, PlayerStats.Multiplier(PlayerStat.CooldownRate));

        /// <summary>Paleta del elemento de la build, o <paramref name="fallback"/>.</summary>
        public static ArsenalPal ElemPal(ArsenalPal fallback) => ArsenalPal.ForElement(Element, fallback);

        private static int Points(BuildTag tag)
        {
            var loadout = WeaponLoadout.Instance;
            return loadout != null && loadout.Synergy != null ? loadout.Synergy.EffectivePointsFor(tag) : 0;
        }
    }
}
