using System.Collections.Generic;
using RedMagic.Localization;
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

        [Tooltip("Proyectiles/golpes que lanza un disparo base (normalmente 1). >1 = abanico tipo escopeta.")]
        [Min(1)]
        public int count = 1;

        [Tooltip("Abanico total en grados repartido entre los proyectiles del disparo base. " +
                 "Sólo hace algo con count > 1.")]
        [Min(0f)]
        public float spreadAngle;

        [Tooltip("Dispersión aleatoria en grados aplicada a cada proyectil (±mitad). Da 'temblor' a " +
                 "una ráfaga sin depender de la Forma.")]
        [Min(0f)]
        public float randomSpread;

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

        [Header("Projectile Collider Override")]
        [Tooltip("Apagado = el collider del prefab (o el círculo del proyectil de código) tal cual. " +
                 "Encendido = esta arma define su propia forma de colisión con los tres campos de " +
                 "abajo, sin tocar el prefab compartido. Vista previa: selecciona el Player (WeaponUser).")]
        public bool overrideCollider;

        public Gameplay.ProjectileColliderType colliderType = Gameplay.ProjectileColliderType.Circle;

        [Tooltip("En unidades del mundo. Circle: X = radio (Y no se usa). Box / Capsule: ancho × alto, " +
                 "con X hacia la punta del proyectil.")]
        public Vector2 colliderSize = new Vector2(0.2f, 0.2f);

        [Tooltip("Centro del collider respecto al del proyectil, en unidades del mundo (X = hacia la punta).")]
        public Vector2 colliderOffset;

        [Tooltip("Enemigos que atraviesa antes de desaparecer. 0 = muere en el primer impacto.")]
        [Min(0)]
        public int pierce;

        [Tooltip("Caída en unidades/s². >0 = trayectoria parabólica tipo granada.")]
        [Min(0f)]
        public float arcGravity;

        [Header("Ráfaga base")]
        [Tooltip("Veces que se repite el volley al pulsar disparo una vez. 1 = disparo único.")]
        [Min(1)]
        public int burstCount = 1;

        [Tooltip("Segundos entre repeticiones de la ráfaga.")]
        [Min(0.02f)]
        public float burstInterval = 0.09f;

        [Header("Explosión al terminar")]
        [Tooltip("Radio de la explosión al impactar o agotar la vida. 0 = sin explosión.")]
        [Min(0f)]
        public float impactRadius;

        [Tooltip("Daño de la explosión (además del impacto directo). Sólo cuenta si el radio es > 0.")]
        [Min(0f)]
        public float impactDamage;

        [Header("Arte (placeholder → sprite real)")]
        [Tooltip("Prefab del proyectil. Vacío = se construye en código con la forma geométrica y " +
                 "el tinte del arma (como hasta ahora). Con prefab, el disparo sale de él por el " +
                 "pool: para poner arte de verdad basta con abrir el prefab y cambiarle el sprite. " +
                 "Si el prefab lleva un FxPlaceholderStyle, se le sigue aplicando tinte/tamaño; si " +
                 "no, se respeta tal cual (arte final).")]
        public GameObject projectilePrefab;

        [Tooltip("Prefab del haz (sólo armas Hitscan). Mismas reglas que projectilePrefab.")]
        public GameObject beamPrefab;

        [Header("Aiming")]
        [Tooltip("Rota el proyectil para que su punta (Facing Axis) mire hacia donde vuela, al salir y " +
                 "en cada paso (autoguiado, parábola). Apagado = decide el prefab (su casilla " +
                 "'Face Travel Direction' del ShotProjectile), así las armas que ya existían no cambian.")]
        public bool faceDirection;

        [Tooltip("Lado del sprite que es la punta. Right = arte dibujado mirando a +X (convención).")]
        public Gameplay.ProjectileFacingAxis facingAxis = Gameplay.ProjectileFacingAxis.Right;

        [Tooltip("Fixed = hacia donde mira el jugador (de siempre). MouseDirection = hacia el cursor al " +
                 "disparar. NearestEnemy = hacia el enemigo vivo más cercano al disparar (sin perseguir). " +
                 "Vale también para haces.")]
        public Gameplay.ProjectileAimMode aimMode = Gameplay.ProjectileAimMode.Fixed;

        [Header("Melee")]
        [Min(0f)]
        public float meleeRange = 1.2f;

        [Tooltip("Ángulo del arco de golpe en grados.")]
        [Min(0f)]
        public float meleeArc = 90f;

        [Header("Carga (mantener para disparar)")]
        [Tooltip("Segundos de mantener el botón hasta plena carga. 0 = dispara al pulsar " +
                 "(comportamiento de todas las armas normales).")]
        [Min(0f)]
        public float chargeTime;

        [Tooltip("Fracción mínima de carga para que salga un disparo al soltar. Por debajo = se " +
                 "cancela sin gastar cooldown.")]
        [Range(0f, 1f)]
        public float minChargeToFire = 0.2f;

        [Tooltip("Daño (respecto al base) a la carga mínima; sube linealmente hasta ×1 a plena carga.")]
        [Range(0f, 1f)]
        public float minChargeDamage = 0.35f;

        [Header("Haz (ShotDelivery.Hitscan)")]
        [Tooltip("Segundos que el haz barre tras dispararse. Escala con la carga.")]
        [Min(0.05f)]
        public float beamDuration = 0.5f;

        [Tooltip("Segundos entre ticks de daño del haz.")]
        [Min(0.02f)]
        public float beamTickInterval = 0.06f;

        [Tooltip("Alcance del haz en unidades. Escala con la carga.")]
        [Min(0.5f)]
        public float beamLength = 14f;

        [Tooltip("Grosor del haz en unidades.")]
        [Min(0.05f)]
        public float beamWidth = 0.5f;
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
        [Tooltip("Prefijo de sus textos en los ficheros de idioma (<prefijo>.name / .description). Lo " +
                 "rellena Tools ▸ RedMagic ▸ Localización ▸ Sincronizar textos de assets; vacío = los textos " +
                 "de aquí tal cual.")]
        [SerializeField] private string textKey;

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

        public string DisplayName => Loc.ForAsset(textKey, "name", string.IsNullOrWhiteSpace(displayName) ? name : displayName);
        public string Description => Loc.ForAsset(textKey, "description", description);
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
#if UNITY_EDITOR
            // La vista previa de tamaño / boca / collider (WeaponUser) vive en la escena: se repinta
            // al tocar el arma en el Inspector, sin entrar en Play.
            UnityEditor.SceneView.RepaintAll();
#endif
            if (BuildTags.IsElemental(universalTag))
                Debug.LogWarning($"[Items] Arma '{name}': 'universalTag' está puesta a una tag " +
                                 $"elemental ({universalTag}). Usa 'innateElement' para el elemento.", this);
        }
    }
}
