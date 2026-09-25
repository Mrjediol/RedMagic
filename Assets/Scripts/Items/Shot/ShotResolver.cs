using System.Collections;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// El pipeline de la sección 2 del documento de diseño, en un solo sitio y genérico para
    /// cualquier arma:
    ///
    /// <list type="number">
    /// <item>El arma genera su "shot base" (<see cref="WeaponShot.FromWeapon"/>).</item>
    /// <item>Se aplica el modificador de Trayectoria activo, si hay.</item>
    /// <item>Se aplica el de Forma (fija cuántos proyectiles salen y si se dividen al impactar).</item>
    /// <item>Se aplica el Elemento a TODOS los proyectiles que resulten del paso 3, incluidos los
    /// hijos de un split — automático, porque todos comparten el mismo <see cref="WeaponShot"/>.</item>
    /// </list>
    ///
    /// <see cref="Resolve"/> hace sólo los pasos 1-4 y devuelve el plan (sirve para tests sin
    /// física); <see cref="Fire"/> además lo lanza.
    /// </summary>
    public static class ShotResolver
    {
        /// <summary>Pasos 1-4. Devuelve el <see cref="WeaponShot"/> resuelto, o null si no hay arma.</summary>
        public static WeaponShot Resolve(WeaponInventory inventory)
        {
            var weapon = inventory != null ? inventory.Weapon : null;
            if (weapon == null) return null;

            var shot = WeaponShot.FromWeapon(weapon);   // 1

            inventory.Trajectory?.Apply(shot);          // 2
            inventory.Shape?.Apply(shot);               // 3
            inventory.Element?.Apply(shot);             // 4

            // Sin modificador de Elemento, el elemento innato del arma sigue pintando el disparo
            // (el tinte innato ya viene del Accent del arma en FromWeapon).
            if (shot.element == ElementId.None && weapon.InnateElement != ElementId.None)
                shot.element = weapon.InnateElement;

            // Placeholder de nivel (ver WeaponLevelManager): nivel 2 tiñe de negro, nivel 3 de
            // dorado, por encima de cualquier tinte de elemento. Nivel 1 no toca nada.
            int level = WeaponLevelManager.Instance != null ? WeaponLevelManager.Instance.GetLevel(weapon) : 1;
            if (WeaponLevelManager.TryGetLevelTint(level, out var levelTint))
                shot.tint = levelTint;

            return shot;
        }

        /// <summary>
        /// Resuelve y lanza el disparo desde <paramref name="ctx"/>.
        ///
        /// <paramref name="chargeFraction"/> (0..1) es la carga con la que se soltó el botón: 1 para
        /// un arma normal sin carga. Por debajo de 1 el daño se interpola entre
        /// <see cref="WeaponShot.minChargeDamage"/> y ×1, y —en un haz— el alcance y la duración
        /// también escalan.
        ///
        /// Un disparo base de tipo <see cref="ShotDelivery.Hitscan"/> se entrega como
        /// <see cref="ShotBeam"/>; el resto como proyectiles (con ráfaga si
        /// <see cref="WeaponShot.burstCount"/> &gt; 1).
        /// </summary>
        public static void Fire(WeaponInventory inventory, in ShotContext fireCtx, float chargeFraction = 1f)
        {
            var shot = Resolve(inventory);
            if (shot == null || !fireCtx.IsValid) return;

            // Apuntado (BaseShot ▸ Aiming): se decide UNA vez, en el instante del disparo, y lo hereda
            // todo lo que salga de él — abanico, ráfaga, haz. Ver Gameplay.ProjectileAim.
            var ctx = Aimed(shot, fireCtx);

            shot.chargeFraction = Mathf.Clamp01(chargeFraction);
            if (shot.chargeFraction < 1f)
                shot.damage *= Mathf.Lerp(shot.minChargeDamage, 1f, shot.chargeFraction);

            if (shot.delivery == ShotDelivery.Hitscan)
            {
                EmitBeam(shot, ctx);
                return;
            }

            int bursts = Mathf.Max(1, shot.burstCount);
            if (bursts <= 1)
            {
                EmitVolley(shot, ctx);
                return;
            }

            EmitVolley(shot, ctx);   // el primero, ya

            if (ctx.Runner != null)
                ctx.Runner.StartCoroutine(BurstRest(shot, ctx, bursts));
            else
                for (int i = 1; i < bursts; i++) EmitVolley(shot, ctx);
        }

        /// <summary>El mismo contexto con el apuntado resuelto según <see cref="WeaponShot.aimMode"/>.</summary>
        private static ShotContext Aimed(WeaponShot shot, in ShotContext ctx)
        {
            if (shot.aimMode == Gameplay.ProjectileAimMode.Fixed) return ctx;

            Vector2 aim = Gameplay.ProjectileAim.Resolve(shot.aimMode, ctx.Muzzle(shot.muzzleOffset), ctx.Aim, ctx.Caster);
            return new ShotContext(ctx.Caster, ctx.Runner, ctx.CasterHealth, ctx.HitLayers, ctx.Facing, aim,
                                   ctx.FriendlyTag, ctx.DamageScale, ctx.Impact);
        }

        private static IEnumerator BurstRest(WeaponShot shot, ShotContext ctx, int bursts)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, shot.burstInterval));
            for (int i = 1; i < bursts; i++)
            {
                yield return wait;
                if (!ctx.IsValid) yield break;
                EmitVolley(shot, ctx);
            }
        }

        /// <summary>El volley del cañón: 1 proyectil, o N en abanico (Forma MultiShot o escopeta base).</summary>
        private static void EmitVolley(WeaponShot shot, in ShotContext ctx)
        {
            Vector2 origin = ctx.Muzzle(shot.muzzleOffset);
            float baseAngle = Mathf.Atan2(ctx.Aim.y, ctx.Aim.x) * Mathf.Rad2Deg;

            int count = Mathf.Max(1, shot.projectileCount);
            float step = count > 1 ? shot.spreadAngle / (count - 1) : 0f;
            float start = baseAngle - shot.spreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? start + step * i : baseAngle;
                if (shot.randomSpread > 0f)
                    angle += Random.Range(-shot.randomSpread * 0.5f, shot.randomSpread * 0.5f);
                ShotProjectile.Spawn(shot, ctx, origin, DirFromAngle(angle), shot.damage);
            }
        }

        /// <summary>
        /// Entrega de haz (hitscan): 1 rayo a lo largo del apuntado, o N en abanico si la Forma es
        /// MultiShot. La Trayectoria (auto-mira) y el Elemento van en el <see cref="WeaponShot"/>, así
        /// que el haz los hereda igual que un proyectil. La carga escala alcance y duración.
        /// </summary>
        private static void EmitBeam(WeaponShot shot, in ShotContext ctx)
        {
            float baseAngle = Mathf.Atan2(ctx.Aim.y, ctx.Aim.x) * Mathf.Rad2Deg;

            int count = Mathf.Max(1, shot.projectileCount);
            float step = count > 1 ? shot.spreadAngle / (count - 1) : 0f;
            float start = baseAngle - shot.spreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? start + step * i : baseAngle;
                ShotBeam.Spawn(shot, ctx, DirFromAngle(angle));
            }
        }

        internal static Vector2 DirFromAngle(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
