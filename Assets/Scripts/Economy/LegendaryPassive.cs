using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Qué estadística toca una <see cref="LegendaryPassive"/>. <see cref="LegendaryPassiveEffects"/>
    /// es quien lee esto y lo convierte en un bono real; el asset sólo declara la intención.
    /// </summary>
    public enum LegendaryPassiveEffectKind
    {
        MaxHealth,
        GoldPerKill,
        MoveSpeed,
        AttackDamage,
        DashSpeed,
        CooldownReduction,
        Armor,
        XpGain,
        HpRegen,
    }

    /// <summary>
    /// Datos de diseño de una pasiva legendaria del espejo: una de las 9 casillas de su rejilla
    /// 3×3. Vive en <c>Assets/Resources/LegendaryPassives/</c> (barrido de carpeta, igual que
    /// <c>Items/Weapons</c>) — añadir una pasiva nueva es crear el asset, sin tocar código.
    ///
    /// Sólo tiene <b>2 niveles</b>: 1 = efecto base al desbloquearla, 2 = el único upgrade
    /// disponible, que dobla el valor (<see cref="baseValue"/> × nivel — ver
    /// <see cref="LegendaryPassiveEffects"/>). <see cref="description"/> es el texto del nivel 1,
    /// <see cref="upgradeDescription"/> el texto (ya final, no una plantilla) del nivel 2.
    ///
    /// <see cref="isUnlocked"/> y <see cref="currentLevel"/> son el <b>valor de diseño por
    /// defecto</b> (para probar en el editor o dar de salida una pasiva ya desbloqueada), NO el
    /// estado de la partida: eso lo lleva <see cref="LegendaryPassiveManager"/> en
    /// <c>PlayerPrefs</c>, igual que <see cref="UpgradeManager"/> hace con los nodos del caldero —
    /// mutar el asset en Play mode sólo cambiaría el fichero en el editor, nunca en build.
    /// </summary>
    [CreateAssetMenu(fileName = "LegendaryPassive", menuName = "RedMagic/Economy/Legendary Passive")]
    public class LegendaryPassive : ScriptableObject
    {
        [Tooltip("Posición en la rejilla 3×3 del espejo, 0-8 (fila-mayor: 0,1,2 / 3,4,5 / 6,7,8).")]
        [Range(0, 8)] public int id;

        [Tooltip("Icono mostrado en la casilla una vez desbloqueada. Vacío = se usa la inicial del nombre.")]
        public Sprite icon;

        [Tooltip("Nombre de la pasiva.")]
        public string displayName = "Placeholder";

        [TextArea]
        [Tooltip("Descripción del efecto en el nivel 1 (el que trae al desbloquearse).")]
        public string description = "Efecto placeholder.";

        [TextArea]
        [Tooltip("Descripción del efecto en el nivel 2 (el único upgrade). Texto final, no plantilla.")]
        public string upgradeDescription = "Efecto placeholder mejorado.";

        [Header("Efecto real")]
        [Tooltip("Qué estadística modifica. Lo lee LegendaryPassiveEffects para calcular el bono real.")]
        public LegendaryPassiveEffectKind effectKind = LegendaryPassiveEffectKind.MaxHealth;

        [Tooltip("Valor del efecto en el nivel 1. El nivel 2 vale el doble (baseValue × nivel). " +
                 "Fracción para los porcentuales (0.05 = 5%), número llano para el resto.")]
        public float baseValue = 1f;

        [Min(1)]
        [Tooltip("Niveles totales disponibles. Siempre 2 en este diseño: base + un upgrade.")]
        public int maxLevel = 2;

        [Min(0)]
        [Tooltip("Coste en Calaveras del único upgrade (nivel 1 → 2).")]
        public int upgradeCost = 50;

        [Header("Valor de diseño por defecto (ver comentario de la clase)")]
        public bool isUnlocked;
        [Min(0)] public int currentLevel;

        /// <summary>Texto del efecto tal y como está AHORA, según el nivel (1 o 2).</summary>
        public string EffectDescriptionForLevel(int level) => level >= 2 ? upgradeDescription : description;
    }
}
