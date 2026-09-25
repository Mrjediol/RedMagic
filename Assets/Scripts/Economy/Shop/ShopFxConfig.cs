using System;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Todos los números de los efectos de la tienda, en <c>Assets/Resources/ShopFxConfig.asset</c>
    /// (lo genera <b>Tools ▸ RedMagic ▸ Tienda ▸ Generar FX de tienda</b>). Se lee en cada uso, así
    /// que se puede afinar en Play. Los colores NO están aquí: salen de la rareza
    /// (<see cref="ItemRarities.ColorOf"/> → <see cref="ItemRarityColors"/>).
    ///  - Aura en el altar: halo aditivo que late + partículas que suben, más intensa cuanto más rara.
    ///  - Compra: icono crece y destella → estallido → vuela al jugador → anillo en el jugador; el
    ///    contador de oro tiembla y cuenta hacia atrás. Legendario: más estallido, temblor de cámara
    ///    y chispas extra.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopFxConfig", menuName = "RedMagic/Shop FX Config")]
    public class ShopFxConfig : ScriptableObject
    {
        public const string ResourcePath = "ShopFxConfig";

        /// <summary>Intensidad del aura (y del estallido de compra) de una rareza.</summary>
        [Serializable]
        public class RarityAura
        {
            [Tooltip("Diámetro del halo, en unidades de mundo.")]
            [Min(0f)] public float haloSize = 2.4f;
            [Range(0f, 1f)] public float haloAlphaMin = 0.15f;
            [Range(0f, 1f)] public float haloAlphaMax = 0.35f;
            [Tooltip("Latidos por segundo.")]
            [Min(0f)] public float pulseSpeed = 0.6f;
            [Tooltip("Cuánto crece el halo en el latido (0.1 = ±10%).")]
            [Range(0f, 0.5f)] public float pulseScale = 0.06f;
            [Min(0f)] public float particlesPerSecond = 2f;
            [Min(0.01f)] public float particleSize = 0.1f;
            [Tooltip("Cada cuántos segundos salta un chisporroteo (0 = nunca).")]
            [Min(0f)] public float sparkleInterval;
            [Min(0)] public int sparkleCount;
            [Tooltip("Partículas del estallido al comprar.")]
            [Min(0)] public int purchaseBurst = 16;

            public RarityAura() { }

            public RarityAura(float haloAlphaMin, float haloAlphaMax, float pulseSpeed, float pulseScale,
                              float particlesPerSecond, float particleSize, float sparkleInterval,
                              int sparkleCount, int purchaseBurst)
            {
                this.haloAlphaMin = haloAlphaMin;
                this.haloAlphaMax = haloAlphaMax;
                this.pulseSpeed = pulseSpeed;
                this.pulseScale = pulseScale;
                this.particlesPerSecond = particlesPerSecond;
                this.particleSize = particleSize;
                this.sparkleInterval = sparkleInterval;
                this.sparkleCount = sparkleCount;
                this.purchaseBurst = purchaseBurst;
            }
        }

        [Header("Material aditivo (RedMagic/Sprite Additive) — halo, destellos, partículas")]
        public Material additiveMaterial;

        [Header("Aura por rareza")]
        public RarityAura common = new(0.10f, 0.22f, 0.5f, 0.04f, 1.5f, 0.16f, 0f, 0, 14);
        public RarityAura blue = new(0.15f, 0.32f, 0.6f, 0.06f, 2.5f, 0.18f, 0f, 0, 18);
        public RarityAura epic = new(0.22f, 0.45f, 0.7f, 0.08f, 5f, 0.21f, 0f, 0, 26);
        public RarityAura legendary = new(0.30f, 0.65f, 0.9f, 0.12f, 9f, 0.25f, 1.6f, 8, 40);

        [Header("Aura — común a todas")]
        [Tooltip("Multiplicador del brillo del halo y de las partículas del altar seleccionado.")]
        [Min(1f)] public float focusBoost = 1.6f;
        [Tooltip("Dónde salen las partículas respecto al icono (la cara de arriba del altar).")]
        public float particleOriginOffset = -0.9f;
        [Min(0f)] public float particleAreaWidth = 1.4f;
        [Min(0f)] public float particleRiseSpeed = 0.45f;
        [Min(0.05f)] public float particleLifetime = 2.2f;

        [Header("Compra — icono")]
        [Min(1f)] public float popScale = 1.5f;
        [Min(0.01f)] public float popDuration = 0.12f;
        [Min(0.01f)] public float whiteFlashDuration = 0.2f;
        [Tooltip("Brillo del destello blanco (se suma encima del icono; >1 = más blanco).")]
        [Min(0f)] public float whiteFlashIntensity = 2.5f;

        [Header("Compra — estallido")]
        [Min(0f)] public float burstSpeed = 5f;
        [Min(0.05f)] public float burstLifetime = 0.6f;
        [Min(0.01f)] public float burstParticleSize = 0.3f;

        [Header("Compra — vuelo al jugador")]
        [Min(0.05f)] public float flyTime = 0.4f;
        [Min(0f)] public float flyArcHeight = 1.5f;
        [Range(0.05f, 1f)] public float flyEndScale = 0.3f;
        [Min(0f)] public float arrivalRingSize = 2.6f;
        [Min(0.05f)] public float arrivalRingDuration = 0.35f;

        [Header("Compra — el altar se vacía")]
        [Min(0.01f)] public float fadeOutDuration = 0.3f;

        [Header("Compra legendaria")]
        [Min(1f)] public float legendaryBurstMultiplier = 1.8f;
        [Min(0f)] public float legendaryShakeAmplitude = 0.25f;
        [Min(0f)] public float legendaryShakeDuration = 0.25f;
        [Min(0)] public int legendaryExtraSparkles = 18;

        [Header("Contador de oro del HUD")]
        [Min(0f)] public float goldShakeAmplitude = 6f;
        [Min(0.01f)] public float goldShakeDuration = 0.3f;
        [Min(0.01f)] public float goldTickDuration = 0.35f;

        public RarityAura For(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Blue => blue,
            ItemRarity.Epic => epic,
            ItemRarity.Legendary => legendary,
            _ => common,
        };

        private static ShopFxConfig _current;

        public static ShopFxConfig Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<ShopFxConfig>(ResourcePath);
                if (_current == null) _current = CreateInstance<ShopFxConfig>();
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;
    }
}
