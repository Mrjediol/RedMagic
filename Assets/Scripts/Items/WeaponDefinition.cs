using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Items
{
    public enum ShotDelivery
    {
        Projectile,
        Melee,
        Hitscan,
    }

    /// <summary>
    /// El disparo del arma <b>sin ningún modificador equipado</b>. Los modificadores de Trayectoria
    /// y Forma se aplicarán como transformaciones sobre esto (paso posterior); el de Elemento lo
    /// "pinta" al final.
    /// </summary>
    [System.Serializable]
    public class BaseShot
    {
        [Tooltip("Cómo entrega el daño el arma base.")]
        public ShotDelivery delivery = ShotDelivery.Projectile;

        [Tooltip("Proyectiles/golpes que lanza un disparo base (normalmente 1).")]
        [Min(1)]
        public int count = 1;

        [Header("Projectile / Hitscan")]
        [Min(0f)]
        public float speed = 12f;

        [Tooltip("Segundos de vuelo antes de desaparecer solo.")]
        [Min(0.05f)]
        public float lifetime = 2f;

        [Tooltip("Tamaño del proyectil/hitbox en unidades del mundo.")]
        public Vector2 size = new Vector2(0.35f, 0.35f);

        [Tooltip("Salida del disparo respecto al arma. La X se invierte según hacia dónde mira.")]
        public Vector2 muzzleOffset = new Vector2(0.6f, 0.1f);

        [Header("Melee")]
        [Min(0f)]
        public float meleeRange = 1.2f;

        [Tooltip("Ángulo del arco de golpe en grados.")]
        [Min(0f)]
        public float meleeArc = 90f;
    }

    /// <summary>
    /// Un arma de run: sus stats base, su "shot base" y las tags que aporta a la identidad de
    /// build. El comportamiento real del arma sale de combinar este shot base con los 3
    /// modificadores de slot dedicado que el jugador equipe (<see cref="WeaponModifier"/>).
    ///
    /// El arma aporta al conteo de sinergias su tag universal siempre, y su elemento innato si
    /// tiene uno (<see cref="InnateElement"/> distinto de <see cref="ElementId.None"/>). Un arma
    /// física sin elemento innato sólo aporta la universal hasta que el jugador equipa un
    /// <see cref="ElementModifier"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Items/Weapon", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Ficha")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [SerializeField] private Sprite icon;

        [SerializeField] private Color accent = new Color(0.85f, 0.8f, 0.6f);

        [Header("Stats base")]
        [Min(0f)]
        [SerializeField] private float baseDamage = 20f;

        [Tooltip("Segundos entre disparos base.")]
        [Min(0.05f)]
        [SerializeField] private float baseCooldown = 0.5f;

        [Tooltip("Multiplica el retroceso configurado en el Knockback del objetivo.")]
        [Min(0f)]
        [SerializeField] private float baseKnockbackMultiplier = 1f;

        [Header("Disparo base (sin modificadores)")]
        [SerializeField] private BaseShot baseShot = new BaseShot();

        [Header("Identidad de build")]
        [Tooltip("Elemento con el que nace el arma. None = arma física; cuenta igual para sinergias " +
                 "si no es None. El slot Elemento lo sobrescribe (último equipado gana).")]
        [SerializeField] private ElementId innateElement = ElementId.None;

        [Tooltip("Tag universal que el arma aporta al conteo de sinergias.")]
        [SerializeField] private BuildTag universalTag = BuildTag.Tank;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Accent => accent;
        public float BaseDamage => baseDamage;
        public float BaseCooldown => baseCooldown;
        public float BaseKnockbackMultiplier => baseKnockbackMultiplier;
        public BaseShot Shot => baseShot;
        public ElementId InnateElement => innateElement;
        public BuildTag UniversalTag => universalTag;

        /// <summary>
        /// Tags que el arma aporta al conteo: siempre la universal, más la elemental de su
        /// elemento innato si tiene uno.
        /// </summary>
        public IEnumerable<BuildTag> ContributedTags()
        {
            yield return universalTag;
            var elemental = BuildTags.TagFor(innateElement);
            if (elemental.HasValue) yield return elemental.Value;
        }

        private void OnValidate()
        {
            if (BuildTags.IsElemental(universalTag))
                Debug.LogWarning($"[Items] Arma '{name}': 'universalTag' está puesta a una tag " +
                                 $"elemental ({universalTag}). Usa 'innateElement' para el elemento.", this);
        }
    }
}
