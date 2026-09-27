using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Qué mecánica activa una <see cref="LegendaryPassive"/>. <see cref="LegendaryPassiveEffects"/>
    /// es quien lee esto y lo aplica; el asset sólo declara la intención. Valores explícitos
    /// porque se serializan como int en los assets.
    /// </summary>
    public enum LegendaryPassiveEffectKind
    {
        /// <summary>Codex Aurum — objeto(s) gratis al derrotar a cada jefe de mundo.</summary>
        FreeItemAfterBoss = 0,
        /// <summary>Tomo del Destino — elegir entre 3 objetos (nivel 2: 1 reroll gratis).</summary>
        ItemChoiceOptions = 1,
        /// <summary>Grimorio del Umbral — 3 armas en el cofre (nivel 2: la elegida con 1 mejora).</summary>
        ChestWeaponChoice = 2,
        /// <summary>Páginas del Eco — rerolls permanentes por run (nivel 2: +4, 2 opciones).</summary>
        PermanentRerolls = 3,
        /// <summary>El Libro Sin Nombre — revivir una vez al 30 % (nivel 2: + escudo de 3 golpes).</summary>
        ReviveOnce = 4,
        /// <summary>Volumen Carmesí — +10 % / +20 % de daño.</summary>
        DamageBonus = 5,
        /// <summary>Anales del Vacío — 1ª oleada ralentizada (nivel 2: enemigos de la run con -20 % vida).</summary>
        WeakenedEnemies = 6,
        /// <summary>Manuscrito Eterno — objeto gratis cada 3 niveles (nivel 2: siempre épico/legendario).</summary>
        PeriodicFreeItem = 7,
        /// <summary>El Tomo Roto — arma del cofre con 2 mejoras (nivel 2: además la de más daño base).</summary>
        ChestWeaponUpgrades = 8,
    }

    /// <summary>
    /// Datos de diseño de una pasiva legendaria del espejo: una de las 9 casillas de su rejilla
    /// 3×3. Vive en <c>Assets/Resources/LegendaryPassives/</c> (barrido de carpeta, igual que
    /// <c>Items/Weapons</c>) — añadir una pasiva nueva es crear el asset, sin tocar código.
    ///
    /// Sólo tiene <b>2 niveles</b>: 1 = efecto base al desbloquearla, 2 = el único upgrade.
    /// <see cref="description"/> es el texto del nivel 1, <see cref="upgradeDescription"/> el
    /// texto (ya final, no una plantilla) del nivel 2. Qué hace cada nivel lo decide
    /// <see cref="LegendaryPassiveEffects"/> según <see cref="effectKind"/>.
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

        [Tooltip("Prefijo de sus textos en los ficheros de idioma (<prefijo>.name / .description / " +
                 ".upgrade). Lo rellena Tools ▸ RedMagic ▸ Localización ▸ Sincronizar textos de assets; " +
                 "vacío = se enseñan los textos de abajo tal cual.")]
        public string textKey;

        [Tooltip("Nombre de la pasiva.")]
        public string displayName = "Placeholder";

        [TextArea]
        [Tooltip("Descripción del efecto en el nivel 1 (el que trae al desbloquearse).")]
        public string description = "Efecto placeholder.";

        [TextArea]
        [Tooltip("Descripción del efecto en el nivel 2 (el único upgrade). Texto final, no plantilla.")]
        public string upgradeDescription = "Efecto placeholder mejorado.";

        [Header("Efecto")]
        [Tooltip("Qué mecánica activa. Lo lee LegendaryPassiveEffects.")]
        public LegendaryPassiveEffectKind effectKind;

        [Min(1)]
        [Tooltip("Niveles totales disponibles. Siempre 2 en este diseño: base + un upgrade.")]
        public int maxLevel = 2;

        [Min(0)]
        [Tooltip("Coste en Calaveras del único upgrade (nivel 1 → 2).")]
        public int upgradeCost = 50;

        [Header("Valor de diseño por defecto (ver comentario de la clase)")]
        public bool isUnlocked;
        [Min(0)] public int currentLevel;

        /// <summary>Nombre en el idioma activo.</summary>
        public string DisplayName => Loc.ForAsset(textKey, "name", displayName);

        /// <summary>Efecto del nivel 1 en el idioma activo.</summary>
        public string Description => Loc.ForAsset(textKey, "description", description);

        /// <summary>Efecto del nivel 2 en el idioma activo.</summary>
        public string UpgradeDescription => Loc.ForAsset(textKey, "upgrade", upgradeDescription);

        /// <summary>Texto del efecto tal y como está AHORA, según el nivel (1 o 2).</summary>
        public string EffectDescriptionForLevel(int level) => level >= 2 ? UpgradeDescription : Description;
    }
}
