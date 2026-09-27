using System;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Números de los efectos de umbral implementados. Vive dentro de <see cref="SynergyConfig"/>
    /// (<c>Assets/Resources/SynergyConfig.asset</c>), que es la fuente de verdad de las sinergias:
    /// el texto de cada umbral y, aquí, sus valores.
    ///
    /// Quién lee cada bloque:
    /// <list type="bullet">
    /// <item>Ralentización, Hielo 2 y Hielo 4 → <see cref="PlayerHit.Deal"/> (al golpear).</item>
    /// <item>Hielo 6, Rapidez 2, Reset 2, Vampirismo 2 → <see cref="SynergyEffectRunner"/>.</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class SynergyTuning
    {
        [Header("Ralentización (la aplica Hielo 2 y los estallidos de hielo)")]
        [Tooltip("Velocidad de movimiento que pierde el enemigo: 0.4 = va al 60%.")]
        [Range(0f, 0.95f)] public float slowStrength = 0.4f;

        [Tooltip("Segundos que dura. Reaplicar la refresca.")]
        [Min(0.1f)] public float slowDuration = 3f;

        [Tooltip("Tinte del enemigo ralentizado (se multiplica sobre su sprite). El alfa es la " +
                 "mezcla máxima: se alcanza cuando la ralentización llega a 'Full Tint At Slow'.")]
        public Color slowTint = new Color(0.4f, 0.8f, 1f, 0.8f);

        [Tooltip("Fuerza de ralentización a la que el tinte llega a su máximo. Por debajo es " +
                 "proporcional: más ralentización, azul más saturado.")]
        [Range(0.05f, 1f)] public float fullTintAtSlow = 0.6f;

        [Header("Hielo")]
        [Tooltip("Hielo 4: daño extra que reciben los ralentizados, de cualquier fuente. 0.15 = +15%.")]
        [Min(0f)] public float ice4DamageTakenBonus = 0.15f;

        [Tooltip("Hielo 6: radio del estallido al matar a un ralentizado.")]
        [Min(0.1f)] public float ice6BurstRadius = 1.8f;

        [Tooltip("Hielo 6: daño del estallido a cada enemigo del radio.")]
        [Min(0f)] public float ice6BurstDamage = 10f;

        [Tooltip("Hielo 6: efecto del estallido (Fx_IceExplosion). Se escala al radio. Vacío = un " +
                 "destello azul de código.")]
        public GameObject ice6BurstPrefab;

        [Header("Rapidez")]
        [Tooltip("Rapidez 2: ritmo de enfriamiento mientras haya un enemigo ralentizado en pantalla. " +
                 "×1.5 = el cooldown se vacía un 50% más rápido.")]
        [Min(1f)] public float haste2CooldownRate = 1.5f;

        [Header("Reset")]
        [Tooltip("Reset 2: segundos que se le quitan al cooldown actual por cada baja.")]
        [Min(0f)] public float reset2CooldownReduction = 2f;

        [Header("Vampirismo")]
        [Tooltip("Vampirismo 2: vida que se recupera por cada baja.")]
        [Min(0f)] public float lifesteal2HealPerKill = 3f;

        [Tooltip("Motas rojas que vuelan del enemigo muerto al jugador en cada baja (siempre, aunque " +
                 "esté a tope de vida).")]
        [Range(0, 20)] public int lifestealDrainMotes = 6;

        [Tooltip("Motas verdes que suben sobre el jugador cuando SÍ recupera vida.")]
        [Range(0, 20)] public int lifestealHealMotes = 5;

        [Tooltip("Sprite de las motas (se tiñen rojo / verde). Vacío = cruz generada en código.")]
        public Sprite lifeMoteSprite;

        [Header("Oro · marca de oro (la pone un proyectil dorado)")]
        [Tooltip("Botín de un enemigo que muere marcado: 2 = el doble de toda moneda.")]
        [Min(1f)] public float markDropMultiplier = 2f;
        [Tooltip("Segundos que dura la marca. Reaplicarla la refresca (no se apila).")]
        [Min(0.1f)] public float markDuration = 8f;
        [Tooltip("Color del aura del enemigo marcado (el alfa es su intensidad).")]
        public Color markAuraColor = new Color(1f, 0.8f, 0.25f, 0.9f);
        [Tooltip("Diámetro del aura respecto al sprite del enemigo.")]
        [Min(0.1f)] public float markAuraSize = 1.5f;
        [Min(0f)] public float markPulseSpeed = 1.2f;
        [Range(0f, 0.5f)] public float markPulseScale = 0.12f;
        [Tooltip("Tinte dorado del cuerpo del marcado (el alfa es la mezcla). Si además está ralentizado, " +
                 "manda el tinte azul del hielo.")]
        public Color markBodyTint = new Color(1f, 0.85f, 0.35f, 0.6f);

        [Header("Oro · proyectil dorado")]
        [Tooltip("Color del aura del proyectil dorado.")]
        public Color gildedAuraColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Diámetro del aura respecto al tamaño del proyectil.")]
        [Min(0.1f)] public float gildedAuraSize = 2.4f;
        [Tooltip("Tinte dorado del sprite del proyectil (el alfa es la mezcla con su color).")]
        public Color gildedTint = new Color(1f, 0.82f, 0.25f, 0.75f);
        [Min(0f)] public float gildedPulseSpeed = 3f;
        [Tooltip("Material aditivo de las auras de oro (Fx_SpriteAdditive). Vacío = sprite normal.")]
        public Material goldAuraMaterial;

        [Header("Oro · umbrales")]
        [Tooltip("Oro 2: toda moneda que sueltan los enemigos, +este %. 0.15 = +15%.")]
        [Min(0f)] public float gold2DropBonus = 0.15f;
        [Tooltip("Oro 4: probabilidad plana de que cualquier proyectil salga dorado (se suma a la de las " +
                 "Botas). 0.05 = +5%.")]
        [Range(0f, 1f)] public float gold4GildChanceBonus = 0.05f;
        [Tooltip("Oro 6: se suma al multiplicador de botín de la marca (2 → 3).")]
        [Min(0f)] public float gold6MarkMultiplierBonus = 1f;
    }
}
