using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// La descripción resuelta de <b>un disparo</b>, tal y como fluye por el pipeline de la
    /// sección 2 del documento de diseño:
    ///
    /// <code>
    /// 1. WeaponShot.FromWeapon(arma)   -> valores base ("shot base")
    /// 2. TrayectoriaModifier.Apply     -> cómo viaja el proyectil
    /// 3. ShapeModifier.Apply           -> cuántos / cómo se dividen
    /// 4. ElementModifier.Apply         -> qué elemento "pinta" el resultado
    /// </code>
    ///
    /// Cada modificador muta este objeto en su sitio, en ese orden fijo. Todo proyectil que se
    /// lance a partir de él —incluidos los hijos de un split— recibe <b>los mismos datos
    /// resueltos</b> (<see cref="SplitChild"/> es una copia), así que la trayectoria y el elemento
    /// se heredan sin ningún código por combinación arma×modificador.
    /// </summary>
    public sealed class WeaponShot
    {
        // -- Entrega (paso 1: base) --------------------------------------------------------------
        public ShotDelivery delivery = ShotDelivery.Projectile;

        // -- Haz / carga (paso 1: base) ---------------------------------------------------------
        public float beamDuration = 0.5f;
        public float beamTickInterval = 0.06f;
        public float beamLength = 14f;
        public float beamWidth = 0.5f;

        /// <summary>Daño relativo a la carga mínima. Lo aplica <see cref="ShotResolver"/> según la carga real.</summary>
        public float minChargeDamage = 0.35f;

        /// <summary>0..1, carga con la que se disparó. 1 = disparo normal / sin carga.</summary>
        public float chargeFraction = 1f;

        // -- Emisión (paso 3: Forma) ----------------------------------------------------------------
        public int projectileCount = 1;
        public float spreadAngle;
        public float randomSpread;
        public float damage;

        // -- Ráfaga base: repite el volley del cañón N veces (paso 1: base) ------------------------
        public int burstCount = 1;
        public float burstInterval = 0.09f;

        // -- Movimiento del proyectil (paso 1: base) -----------------------------------------------
        public float speed = 12f;
        public float lifetime = 2f;
        public Vector2 size = new Vector2(0.35f, 0.35f);
        public Vector2 muzzleOffset = new Vector2(0.6f, 0.1f);

        // -- Arte (paso 1: base) -----------------------------------------------------------------
        /// <summary>Prefab del proyectil, o null para construirlo en código. Lo heredan los hijos de un split.</summary>
        public GameObject projectilePrefab;

        /// <summary>Prefab del haz (armas Hitscan), o null para construirlo en código.</summary>
        public GameObject beamPrefab;

        // Collider propio del arma (ver Gameplay.ProjectileColliderShape).
        public bool overrideCollider;
        public Gameplay.ProjectileColliderType colliderType;
        public Vector2 colliderSize;
        public Vector2 colliderOffset;

        // Aiming (ver Gameplay.ProjectileAim).
        public bool faceDirection;
        public Gameplay.ProjectileFacingAxis facingAxis = Gameplay.ProjectileFacingAxis.Right;
        public Gameplay.ProjectileAimMode aimMode = Gameplay.ProjectileAimMode.Fixed;

        /// <summary>Enemigos que el proyectil atraviesa antes de morir. 0 = muere en el primer impacto.</summary>
        public int pierce;

        /// <summary>Caída en u/s². >0 = parábola. La auto-mira (trayectoria) tiene prioridad si ambas están.</summary>
        public float arcGravity;

        // -- Explosión al terminar (base) ---------------------------------------------------------
        public float impactRadius;
        public float impactDamage;

        // -- Trayectoria (paso 2) ----------------------------------------------------------------
        public TrajectoryKind trajectory = TrajectoryKind.Straight;
        public float homingTurnRate;
        public float homingRange = 9f;

        // -- Forma: split al impacto (paso 3) ---------------------------------------------------
        public int splitCount;
        public float splitDamageFraction = 1f;

        /// <summary>Veces que un proyectil aún puede dividirse. 1 = "split una vez"; los hijos ya llevan 0.</summary>
        public int splitGenerationsLeft;

        // -- Elemento (paso 4) ----------------------------------------------------------------
        public ElementId element = ElementId.None;
        public Color tint = Color.white;
        public float statusChance;
        public float statusDuration;
        public float statusMagnitude;

        /// <summary>Paso 1: valores base del arma, sin ningún modificador.</summary>
        public static WeaponShot FromWeapon(WeaponDefinition weapon)
        {
            var shot = weapon.Shot;
            return new WeaponShot
            {
                delivery = shot.delivery,
                beamDuration = Mathf.Max(0.05f, shot.beamDuration),
                beamTickInterval = Mathf.Max(0.02f, shot.beamTickInterval),
                beamLength = Mathf.Max(0.5f, shot.beamLength),
                beamWidth = Mathf.Max(0.05f, shot.beamWidth),
                minChargeDamage = Mathf.Clamp01(shot.minChargeDamage),
                projectileCount = Mathf.Max(1, shot.count),
                spreadAngle = shot.spreadAngle,
                randomSpread = shot.randomSpread,
                burstCount = Mathf.Max(1, shot.burstCount),
                burstInterval = Mathf.Max(0.02f, shot.burstInterval),
                damage = weapon.BaseDamage,
                speed = shot.speed,
                lifetime = shot.lifetime,
                size = shot.size,
                muzzleOffset = shot.muzzleOffset,
                projectilePrefab = shot.projectilePrefab,
                beamPrefab = shot.beamPrefab,
                faceDirection = shot.faceDirection,
                facingAxis = shot.facingAxis,
                aimMode = shot.aimMode,
                overrideCollider = shot.overrideCollider,
                colliderType = shot.colliderType,
                colliderSize = shot.colliderSize,
                colliderOffset = shot.colliderOffset,
                pierce = Mathf.Max(0, shot.pierce),
                arcGravity = Mathf.Max(0f, shot.arcGravity),
                impactRadius = Mathf.Max(0f, shot.impactRadius),
                impactDamage = Mathf.Max(0f, shot.impactDamage),
                tint = weapon.Accent,
            };
        }

        /// <summary>
        /// Un proyectil derivado de un split: mismos datos (trayectoria, elemento, tinte…), una
        /// generación de split menos, sin volley múltiple y con el daño reducido por
        /// <see cref="splitDamageFraction"/>.
        /// </summary>
        public WeaponShot SplitChild()
        {
            var child = (WeaponShot)MemberwiseClone();
            child.projectileCount = 1;
            child.spreadAngle = 0f;
            child.randomSpread = 0f;
            child.burstCount = 1;   // la ráfaga es del cañón, no se hereda al partirse
            child.splitGenerationsLeft = Mathf.Max(0, splitGenerationsLeft - 1);
            child.damage = damage * Mathf.Clamp01(splitDamageFraction);
            return child;
        }
    }
}
