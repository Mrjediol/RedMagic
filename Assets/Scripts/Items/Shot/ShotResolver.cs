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

            return shot;
        }

        /// <summary>Resuelve y lanza el disparo desde <paramref name="ctx"/>.</summary>
        public static void Fire(WeaponInventory inventory, in ShotContext ctx)
        {
            var shot = Resolve(inventory);
            if (shot == null || !ctx.IsValid) return;

            EmitVolley(shot, ctx);
        }

        /// <summary>El volley inicial: 1 proyectil, o N en abanico si la Forma es MultiShot.</summary>
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
                ShotProjectile.Spawn(shot, ctx, origin, DirFromAngle(angle), shot.damage);
            }
        }

        internal static Vector2 DirFromAngle(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
