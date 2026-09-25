using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Fx;
using UnityEngine;
using UnityEngine.Pool;

namespace RedMagic.Items
{
    /// <summary>
    /// Estallido de hielo en área: daño a todo enemigo del radio + el efecto
    /// <c>Fx_IceExplosion</c> escalado a ese radio. Lo comparten la explosión grande del Yelmo y el
    /// estallido pequeño de Hielo 6 — el mismo dibujo a dos tamaños.
    ///
    /// El daño va por <see cref="PlayerHit.Deal"/>, así que sus bajas cuentan como cualquier otra
    /// (cargan el Bastón, el contador del Yelmo…) y un estallido que mata a un ralentizado puede
    /// encadenar otro con Hielo 6. Por eso la lista de objetivos se copia a una lista propia antes
    /// de golpear: el estallido encadenado reutiliza el buffer de física.
    /// </summary>
    public static class IceBurst
    {
        private static readonly Collider2D[] Buffer = new Collider2D[64];
        private static readonly Color FallbackTint = new Color(0.55f, 0.9f, 1f, 0.6f);

        /// <summary>
        /// Revienta en <paramref name="center"/>. Devuelve cuántos enemigos recibieron el golpe.
        /// </summary>
        /// <param name="vfxPrefab">Prefab pooled (<see cref="VfxOneShot"/>); null = destello de código.</param>
        /// <param name="applySlow">Ralentiza a los supervivientes aunque no haya Hielo 2.</param>
        public static int Detonate(Vector2 center, float radius, float damage, GameObject vfxPrefab,
                                   bool applySlow, float knockbackMultiplier = 1f)
        {
            radius = Mathf.Max(0.1f, radius);

            if (vfxPrefab != null) VfxOneShot.SpawnFitWidth(vfxPrefab, center, radius * 2f);
            else AbilityFx.Flash(null, center, Vector2.one * (radius * 2f), FallbackTint, 0.3f, 0f, 1.15f);

            if (damage <= 0f && !applySlow) return 0;

            var filter = new ContactFilter2D { useLayerMask = false, useTriggers = true };
            int count = Physics2D.OverlapCircle(center, radius, filter, Buffer);

            var targets = ListPool<Health>.Get();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var health = Buffer[i] != null ? Buffer[i].GetComponentInParent<Health>() : null;
                    if (health == null || health.IsDead || targets.Contains(health)) continue;
                    if (Teams.Of(health) != Team.Enemy) continue;   // sólo daña a enemigos
                    targets.Add(health);
                }

                int hits = 0;
                foreach (var target in targets)
                {
                    if (target == null || target.IsDead) continue;
                    if (damage > 0f && PlayerHit.Deal(target, damage, center, knockbackMultiplier, HitKind.Burst))
                        hits++;
                    if (applySlow && !target.IsDead) PlayerHit.ApplySlow(target);
                }

                return hits;
            }
            finally
            {
                ListPool<Health>.Release(targets);
            }
        }
    }
}
