using System;
using System.Collections.Generic;
using RedMagic.Enemies;
using RedMagic.Items;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.Serialization;

namespace RedMagic.Survival
{
    /// <summary>Qué reglas sigue un enemigo para aparecer (<see cref="SurvivalSpawnFinder"/>).</summary>
    public enum SurvivalSpawnKind
    {
        [InspectorName("Volador")] Flying,
        [InspectorName("Suelo")] Ground,
        [InspectorName("Torreta")] Turret,
        [InspectorName("Jefe")] Boss,
    }

    /// <summary>Cuánto sale un enemigo frente a los demás de su lista (peso en <see cref="SurvivalConfig.rarityWeights"/>).</summary>
    public enum SurvivalRarity
    {
        [InspectorName("Común")] Common,
        [InspectorName("Poco común")] Uncommon,
        [InspectorName("Raro")] Rare,
        [InspectorName("Épico")] Epic,
    }

    /// <summary>Qué pasa al acabar una ronda. Se pueden marcar las dos.</summary>
    [Flags]
    public enum SurvivalReward
    {
        [InspectorName("Nada")] Nothing = 0,
        [InspectorName("Tienda")] Shop = 1,
        [InspectorName("Item que cae")] ItemDrop = 2,
    }

    /// <summary>Cómo es una oleada.</summary>
    public enum SurvivalWaveMode
    {
        [InspectorName("Simples (una lista fija, salen seguidos)")] Simple,
        [InspectorName("Dinámicas (van reapareciendo)")] Dynamic,
    }

    /// <summary>Cuándo acaba una oleada dinámica.</summary>
    public enum SurvivalWaveEnd
    {
        [InspectorName("Por tiempo")] Time,
        [InspectorName("Por bajas")] Kills,
    }

    /// <summary>Qué pasa con los enemigos vivos cuando se acaba el tiempo de una oleada dinámica.</summary>
    public enum SurvivalTimeUp
    {
        [InspectorName("Dejan de salir; hay que acabar con los que quedan")] FinishRemaining,
        [InspectorName("Desaparecen (sin botín)")] RemoveRemaining,
    }

    /// <summary>Los modos de dificultad, en el orden del menú.</summary>
    public enum SurvivalDifficulty
    {
        [InspectorName("Fácil")] Easy,
        [InspectorName("Normal")] Normal,
        [InspectorName("Difícil")] Hard,
    }

    /// <summary>
    /// Lo que cambia un modo de dificultad: más enemigos a la vez (más que farmear: la parte buena) y más vida y daño (la
    /// mala). Se multiplica sobre todo lo demás.
    /// </summary>
    [Serializable]
    public class SurvivalDifficultySettings
    {
        [Tooltip("× enemigos a la vez (dinámicas) y × enemigos por oleada (simples).")]
        [Min(0.1f)] public float enemyMultiplier = 1f;
        [Tooltip("× bajas a conseguir en las oleadas dinámicas por bajas.")]
        [Min(0.1f)] public float killsMultiplier = 1f;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0f)] public float damageMultiplier = 1f;

        public SurvivalDifficultySettings(float enemies, float kills, float health, float damage)
        {
            enemyMultiplier = enemies; killsMultiplier = kills; healthMultiplier = health; damageMultiplier = damage;
        }
    }

    /// <summary>Un enemigo de una lista: su prefab, su rareza y las especiales que puede tener.</summary>
    [Serializable]
    public class SurvivalEnemyEntry
    {
        public GameObject prefab;
        public SurvivalRarity rarity = SurvivalRarity.Common;
        [Tooltip("Versiones especiales (naranja, morada, dorada…). Cada una sólo puede salir cuando la dificultad de la " +
                 "oleada llega a la suya.")]
        public List<EnemyVariant> specials = new List<EnemyVariant>();
    }

    /// <summary>Un tramo de oleadas = un mundo de la run (Hielo 1-5, Bosque 6-10…).</summary>
    [Serializable]
    public class SurvivalStage
    {
        [Tooltip("Clave de idioma del nombre que ve el jugador (p. ej. survival.stage.ice).")]
        public string textKey = "survival.stage.ice";
        [Tooltip("Nombre si la clave no existe.")]
        public string fallbackName = "Hielo";

        [Tooltip("Oleadas normales de este mundo (sin contar la del jefe).")]
        [Min(0)] public int waves = 5;
        [Tooltip("Mundo al que equivale: la tienda y los items que caen usan sus rarezas y precios (ShopConfig).")]
        [Min(1)] public int worldNumber = 1;

        [Tooltip("Color de zona para todos sus enemigos (provisional, hasta tener enemigos propios). Vacío = su color.")]
        public EnemyVariant zoneLook;

        [Header("Ajuste del mundo (× sobre el escalado por oleada)")]
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("× al número de enemigos de cada oleada.")]
        [Min(0f)] public float countMultiplier = 1f;

        [Header("Enemigos")]
        public List<SurvivalEnemyEntry> flying = new List<SurvivalEnemyEntry>();
        public List<SurvivalEnemyEntry> ground = new List<SurvivalEnemyEntry>();
        public List<SurvivalEnemyEntry> turrets = new List<SurvivalEnemyEntry>();

        [Header("Oleadas dinámicas (una por oleada de este mundo; si la lista es más corta, se repite el último valor)")]
        [Tooltip("Segundos de cada oleada: [30, 45, 90] = la 1.ª dura 30 s, la 2.ª 45 s y de la 3.ª en adelante 90 s. " +
                 "Vacío = el valor general del config.")]
        public List<float> waveSeconds = new List<float>();
        [Tooltip("Bajas para acabar cada oleada, igual que arriba. Vacío = el valor general del config.")]
        public List<int> waveKills = new List<int>();

        [Header("Jefe")]
        [Tooltip("Añade una oleada más al final, sólo con el jefe (acaba al matarlo, sea cual sea el tipo de oleada).")]
        public bool bossWave;
        [Tooltip("Uno al azar de esta lista.")]
        public List<GameObject> bosses = new List<GameObject>();

        [Header("Entre rondas")]
        [Tooltip("Usar las recompensas de abajo en vez de las generales.")]
        public bool overrideRewards;
        public SurvivalReward rewards = SurvivalReward.Shop;

        public string DisplayName => Loc.GetOr(textKey, fallbackName);

        /// <summary>El valor de <paramref name="list"/> para la oleada <paramref name="waveInStage"/> (desde 1); vacía = <paramref name="fallback"/>.</summary>
        public static T PerWave<T>(List<T> list, int waveInStage, T fallback) =>
            list == null || list.Count == 0 ? fallback : list[Mathf.Clamp(waveInStage - 1, 0, list.Count - 1)];
        public int TotalWaves => waves + (bossWave && bosses.Count > 0 ? 1 : 0);

        public List<SurvivalEnemyEntry> Pool(SurvivalSpawnKind kind) => kind switch
        {
            SurvivalSpawnKind.Flying => flying,
            SurvivalSpawnKind.Ground => ground,
            SurvivalSpawnKind.Turret => turrets,
            _ => null,
        };
    }

    /// <summary>Cuántos enemigos de un tipo salen: en la oleada 1, cuántos más por oleada y un tope.</summary>
    [Serializable]
    public class SurvivalCount
    {
        [Min(0f)] public float atFirstWave = 4f;
        [Min(0f)] public float perWave = 1f;
        [Min(0)] public int max = 40;

        public SurvivalCount(float first, float per, int cap) { atFirstWave = first; perWave = per; max = cap; }

        public int At(int wave, float multiplier) =>
            Mathf.Min(max, Mathf.RoundToInt((atFirstWave + perWave * (wave - 1)) * multiplier));
    }

    /// <summary>
    /// <b>Todo</b> lo que se ajusta del modo Supervivencia, en un asset: mundos y sus enemigos, cuántos salen, escalado,
    /// dificultad de las especiales, rarezas, recompensas entre rondas, moneda y arranque. El mapa sólo pone los puntos de
    /// aparición y la tienda (<see cref="SurvivalManager"/>), así que otro mapa usa el mismo asset tal cual.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Supervivencia/Configuración", fileName = "SurvivalConfig")]
    public class SurvivalConfig : ScriptableObject
    {
        [Header("Arranque")]
        [Min(0)] public int startingGold;
        public WeaponDefinition startingWeapon;
        [Tooltip("Segunda arma (la de reserva, Tab). Vacío = una sola.")]
        public WeaponDefinition startingSecondWeapon;
        public List<ItemDefinition> startingItems = new List<ItemDefinition>();
        [Tooltip("Armas que el menú DEV (F10) deja poner en los huecos.")]
        public List<WeaponDefinition> devWeapons = new List<WeaponDefinition>();

        [Header("Mundos, en orden")]
        public List<SurvivalStage> stages = new List<SurvivalStage>();
        [Tooltip("Al acabar el último mundo, seguir con oleadas suyas (el escalado sigue subiendo). Apagado = victoria.")]
        public bool repeatLastStage = true;

        [Header("Tipo de oleada")]
        public SurvivalWaveMode waveMode = SurvivalWaveMode.Dynamic;

        [Header("Proporción de tipos (los dos tipos de oleada)")]
        [Tooltip("% de voladores. Los tres se reparten entre sí (no hace falta que sumen 100); un tipo sin enemigos en la " +
                 "lista del mundo cede su parte a los otros.")]
        [Min(0f)] public float flyingPercent = 80f;
        [Tooltip("% de enemigos de suelo.")]
        [Min(0f)] public float groundPercent = 15f;
        [Tooltip("% de torretas.")]
        [Min(0f)] public float turretPercent = 5f;

        [Header("Oleadas simples: cuántos enemigos")]
        [Tooltip("Total de la oleada (× mundo × dificultad), repartido con la proporción de arriba.")]
        public SurvivalCount enemiesPerWave = new SurvivalCount(8f, 3f, 80);

        [Header("Ritmo de aparición")]
        [Tooltip("Segundos desde que empieza la oleada hasta el primer enemigo.")]
        [Min(0f)] public float firstSpawnDelay = 1.5f;
        [Tooltip("Segundos entre un enemigo y el siguiente (simples, y el llenado inicial de las dinámicas).")]
        [Min(0.02f)] public float spawnInterval = 0.6f;
        [Tooltip("Simples: enemigos vivos a la vez como máximo (× dificultad); el resto espera a que muera alguno.")]
        [Min(1)] public int maxAlive = 14;

        [Header("Oleadas dinámicas")]
        public SurvivalWaveEnd dynamicEnd = SurvivalWaveEnd.Time;
        [Tooltip("Enemigos a la vez como máximo (× mundo y × dificultad): en la oleada 1, cuántos más por oleada y tope.")]
        public SurvivalCount dynamicMaxAlive = new SurvivalCount(6f, 1f, 20);
        [Tooltip("Segundos desde que muere un enemigo hasta que sale otro (si hay hueco).")]
        [Min(0f)] public float respawnDelay = 3f;
        [Tooltip("Duración de una oleada por tiempo si el mundo no pone la suya.")]
        [Min(1f)] public float defaultWaveSeconds = 60f;
        [Tooltip("Bajas de una oleada por bajas si el mundo no pone las suyas.")]
        [Min(1)] public int defaultWaveKills = 30;
        public SurvivalTimeUp onTimeUp = SurvivalTimeUp.FinishRemaining;

        [Header("Dónde aparecen (al azar alrededor del jugador; el jefe, en el centro del suelo)")]
        [Tooltip("Nunca más cerca del jugador que esto (que no salgan en su cara).")]
        [FormerlySerializedAs("minSpawnDistance")]
        [Min(0f)] public float minDistance = 6f;
        [Tooltip("Nunca más lejos que esto.")]
        [FormerlySerializedAs("spawnRadius")]
        [Min(1f)] public float maxDistance = 15f;
        [Tooltip("Como mucho tantas unidades por encima del jugador (que no salgan en el techo sin verlos).")]
        [Min(0f)] public float maxAbove = 8f;
        [Tooltip("Como mucho tantas unidades por debajo del jugador.")]
        [Min(0f)] public float maxBelow = 8f;
        [Tooltip("Distancia mínima a cualquier otro enemigo (vivo o recién salido): que no salgan amontonados.")]
        [Min(0f)] public float minSeparation = 3f;
        [Tooltip("Voladores: hueco libre alrededor (sin pared ni suelo a menos de esto).")]
        [Min(0f)] public float flyerClearance = 1f;
        [Tooltip("Suelo y torretas: a cuánto por encima del suelo o plataforma aparecen (caen solos).")]
        [Min(0f)] public float groundSpawnHeight = 1f;
        [Tooltip("Torretas: peso de aparecer en un suelo o plataforma.")]
        [Min(0f)] public float turretOnFloors = 1f;
        [Tooltip("Torretas: peso de aparecer en una pared (de lado, con la base contra la pared).")]
        [Min(0f)] public float turretOnWalls = 1f;
        [Tooltip("Torretas: peso de aparecer en un techo (boca abajo).")]
        [Min(0f)] public float turretOnCeilings = 1f;
        [Tooltip("Capas del terreno macizo (Ground).")]
        public LayerMask solidLayers = 1 << 6;
        [Tooltip("Capas de las plataformas (Platform): valen como suelo, no como pared.")]
        public LayerMask platformLayers = 1 << 8;
        [Tooltip("Intentos por enemigo antes de aflojar las reglas.")]
        [Min(1)] public int maxAttempts = 40;

        [Header("Modos de dificultad (se eligen en el menú)")]
        public SurvivalDifficultySettings easy = new SurvivalDifficultySettings(1f, 1f, 1f, 1f);
        public SurvivalDifficultySettings normal = new SurvivalDifficultySettings(1.5f, 1.5f, 1.25f, 1.2f);
        public SurvivalDifficultySettings hard = new SurvivalDifficultySettings(2f, 2f, 1.6f, 1.5f);

        [Header("Escalado por oleada (vida y daño de los enemigos)")]
        [Tooltip("+ vida por oleada (0,12 = +12 % cada oleada sobre la vida del prefab).")]
        [Min(0f)] public float healthGrowthPerWave = 0.12f;
        [Tooltip("+ daño por oleada (0,08 = +8 % cada oleada).")]
        [Min(0f)] public float damageGrowthPerWave = 0.08f;
        [Tooltip("Apagado = lineal (1 + n·crecimiento). Encendido = compuesto ((1 + crecimiento)^n).")]
        public bool compoundGrowth;

        [Header("Dificultad y especiales")]
        [Tooltip("Dificultad de la oleada 1 (1 = 100 %). Una especial sólo sale si su dificultad es ≤ la de la oleada.")]
        [Min(0f)] public float difficultyAtFirstWave = 1f;
        [Tooltip("+ dificultad por oleada (0,2 = +20 %: la 3 está en 140 %).")]
        [Min(0f)] public float difficultyPerWave = 0.2f;
        [Tooltip("Probabilidad de que un enemigo salga como especial cuando alguna de las suyas ya está permitida.")]
        [Range(0f, 1f)] public float specialChance = 0.3f;
        [Tooltip("Peso de cada rareza (Común, Poco común, Raro, Épico). Con 2 comunes y 1 raro de pesos 1 y 0,4, de 30 " +
                 "enemigos salen ~12,5 de cada común y ~5 raros.")]
        public float[] rarityWeights = { 1f, 0.7f, 0.4f, 0.15f };

        [Header("Entre rondas")]
        public SurvivalReward rewards = SurvivalReward.Shop;
        [Tooltip("Items que caen al acabar la ronda (si 'Item que cae' está marcado).")]
        [Min(1)] public int itemDrops = 1;
        [Tooltip("Fracción de la vida máxima que se cura al acabar cada ronda (0 = nada, 1 = toda).")]
        [Range(0f, 1f)] public float healBetweenWaves;
        [Tooltip("0 = esperar a que el jugador pulse 'Siguiente ronda'. > 0 = empieza sola tras estos segundos.")]
        [Min(0f)] public float autoNextWaveSeconds;

        [Header("Armas: evolución y núcleos en la partida (WeaponRunProgress)")]
        [Tooltip("Al superar estas rondas se gana un Prisma (evolucionar un arma). Diseño: al acabar cada mundo.")]
        public List<int> evolutionAfterWaves = new List<int> { 5, 10 };
        [Tooltip("Probabilidad de que una ronda traiga un portador de núcleo (× la facilidad de prueba de " +
                 "Resources/WeaponProgressionConfig). 0,1 ≈ 1,3 núcleos en 15 rondas.")]
        [Range(0f, 1f)] public float carrierChancePerWave = 0.1f;

        [Header("Moneda")]
        [Tooltip("× al oro que sueltan los enemigos (sólo ≥ 1: se suma a los bonos de items).")]
        [Min(1f)] public float goldDropMultiplier = 1f;
        [Tooltip("Oro extra al superar cada ronda.")]
        [Min(0)] public int goldPerWaveCleared;

        [Header("Aspecto")]
        [Tooltip("Shader con el que se pintan las variantes (RedMagic/Sprite Lit FX). Referenciado aquí para que entre en la build.")]
        public Shader variantShader;

        // ------------------------------------------------------------------ consultas

        /// <summary>
        /// El mundo de la oleada <paramref name="wave"/> (desde 1), qué oleada es dentro de él (desde 1) y si es la del
        /// jefe. Null si se acabaron los mundos y no se repite el último.
        /// </summary>
        public SurvivalStage StageFor(int wave, out int waveInStage, out bool bossWave)
        {
            waveInStage = 0; bossWave = false;
            int left = wave;
            SurvivalStage last = null;
            foreach (var stage in stages)
            {
                if (stage == null || stage.TotalWaves <= 0) continue;
                last = stage;
                if (left <= stage.TotalWaves)
                {
                    waveInStage = left;
                    bossWave = stage.bossWave && stage.bosses.Count > 0 && left == stage.TotalWaves;
                    return stage;
                }
                left -= stage.TotalWaves;
            }
            if (!repeatLastStage || last == null) return null;
            waveInStage = last.TotalWaves + left;   // seguir contando dentro del último mundo, sin jefe
            return last;
        }

        public float HealthScale(int wave) => Growth(healthGrowthPerWave, wave);
        public float DamageScale(int wave) => Growth(damageGrowthPerWave, wave);
        public float Difficulty(int wave) => difficultyAtFirstWave + difficultyPerWave * (wave - 1);

        private float Growth(float perWave, int wave) =>
            compoundGrowth ? Mathf.Pow(1f + perWave, wave - 1) : 1f + perWave * (wave - 1);

        public float RarityWeight(SurvivalRarity rarity)
        {
            int i = (int)rarity;
            return rarityWeights != null && i < rarityWeights.Length ? Mathf.Max(0f, rarityWeights[i]) : 1f;
        }

        /// <summary>El % de un tipo (sin normalizar: los tres se reparten entre sí).</summary>
        public float KindPercent(SurvivalSpawnKind kind) => kind switch
        {
            SurvivalSpawnKind.Flying => flyingPercent,
            SurvivalSpawnKind.Ground => groundPercent,
            SurvivalSpawnKind.Turret => turretPercent,
            _ => 0f,
        };

        public SurvivalDifficultySettings Settings(SurvivalDifficulty difficulty) => difficulty switch
        {
            SurvivalDifficulty.Easy => easy,
            SurvivalDifficulty.Hard => hard,
            _ => normal,
        };

        public float WaveSeconds(SurvivalStage stage, int waveInStage) =>
            SurvivalStage.PerWave(stage?.waveSeconds, waveInStage, defaultWaveSeconds);

        public int WaveKills(SurvivalStage stage, int waveInStage) =>
            SurvivalStage.PerWave(stage?.waveKills, waveInStage, defaultWaveKills);

        public SurvivalReward RewardsFor(SurvivalStage stage) =>
            stage != null && stage.overrideRewards ? stage.rewards : rewards;
    }
}
