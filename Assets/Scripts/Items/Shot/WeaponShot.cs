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
        // -- Emisión (paso 3: Forma) ----------------------------------------------------------------
        public int projectileCount = 1;
        public float spreadAngle;
        public float damage;

        // -- Movimiento del proyectil (paso 1: base) -----------------------------------------------
        public float speed = 12f;
        public float lifetime = 2f;
        public Vector2 size = new Vector2(0.35f, 0.35f);
        public Vector2 muzzleOffset = new Vector2(0.6f, 0.1f);

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
                projectileCount = Mathf.Max(1, shot.count),
                damage = weapon.BaseDamage,
                speed = shot.speed,
                lifetime = shot.lifetime,
                size = shot.size,
                muzzleOffset = shot.muzzleOffset,
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
            child.splitGenerationsLeft = Mathf.Max(0, splitGenerationsLeft - 1);
            child.damage = damage * Mathf.Clamp01(splitDamageFraction);
            return child;
        }
    }
}
