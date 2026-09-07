using System;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Descripción de un disparo. Es un bloque serializable reutilizable: lo usan la habilidad de
    /// proyectil, el tótem invocado y cualquier cosa futura que dispare, de modo que "cómo vuela un
    /// proyectil" se define en un solo sitio.
    ///
    /// Si no se le da <see cref="prefab"/>, el proyectil se <b>construye en código</b> (sprite +
    /// collider + Rigidbody + <see cref="Projectile"/>). Así una habilidad nueva no necesita que
    /// nadie cree y enganche un prefab: se duplica el asset, se cambian los números y ya dispara.
    /// </summary>
    [Serializable]
    public class ProjectileSpec
    {
        [Tooltip("Prefab con componente Projectile. Vacío = se construye uno en código con el " +
                 "sprite y el color de la habilidad.")]
        public GameObject prefab;

        [Min(0.1f)]
        public float speed = 12f;

        [Tooltip("Segundos de vuelo antes de desaparecer solo.")]
        [Min(0.05f)]
        public float lifetime = 2.5f;

        [Tooltip("Tamaño del proyectil en unidades del mundo.")]
        public Vector2 size = new Vector2(0.35f, 0.35f);

        [Tooltip("Salida del disparo respecto al lanzador. La X se invierte según hacia dónde mira.")]
        public Vector2 muzzleOffset = new Vector2(0.65f, 0.1f);

        [Header("Comportamiento")]
        [Tooltip("Objetivos que atraviesa antes de desaparecer.")]
        [Min(0)]
        public int pierce;

        [Tooltip("Grados/segundo de giro hacia el objetivo más cercano. 0 = va recto.")]
        [Min(0f)]
        public float homingTurnRate;

        [Min(0f)]
        public float homingRange = 9f;

        [Tooltip("Caída en unidades/s². >0 = granada con trayectoria parabólica.")]
        [Min(0f)]
        public float arcGravity;

        [Header("Explosión al terminar")]
        [Min(0f)]
        public float impactRadius;

        [Tooltip("Daño de la explosión. Se suma al impacto directo sólo si el radio es > 0.")]
        [Min(0f)]
        public float impactDamage;
    }

    /// <summary>
    /// Instancia proyectiles a partir de un <see cref="ProjectileSpec"/>, <b>pooled</b>: los de
    /// prefab van por <see cref="Core.PrefabPool"/>, los construidos en código por un
    /// <see cref="Core.Pool{T}"/> propio. Nunca <c>Instantiate</c>/<c>Destroy</c> por disparo.
    /// </summary>
    public static class ProjectileFactory
    {
        private static Core.Pool<Projectile> _codePool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPools() => _codePool = null;

        /// <summary>Devuelve un proyectil construido en código a su pool. Lo llama <see cref="Projectile.ReturnToPool"/>.</summary>
        internal static void ReleaseCodePooled(Projectile projectile) => _codePool?.Release(projectile);

        /// <summary>
        /// Lanza un proyectil desde <paramref name="origin"/> en <paramref name="direction"/>.
        /// Devuelve el objeto (o null si el prefab estaba mal montado).
        /// </summary>
        public static GameObject Spawn(in AbilityContext ctx, ProjectileSpec spec, Vector2 origin,
                                       Vector2 direction, float damage, float knockbackMultiplier,
                                       Sprite sprite, Color tint)
        {
            if (spec == null) return null;

            Projectile projectile;

            if (spec.prefab != null)
            {
                var go = Core.PrefabPool.Spawn(spec.prefab, origin, Quaternion.identity);
                projectile = go != null ? go.GetComponent<Projectile>() : null;
                if (projectile == null)
                {
                    Debug.LogWarning($"[Abilities] El prefab '{spec.prefab.name}' no tiene componente Projectile.");
                    if (go != null) Core.PrefabPool.Despawn(go);
                    return null;
                }
                projectile.PooledPrefab = true;
            }
            else
            {
                _codePool ??= new Core.Pool<Projectile>(BuildCodeProjectile, prewarm: 16);
                projectile = _codePool.Get();
                projectile.ApplyCodeVisual(origin, spec.size * ctx.SizeScale, sprite, tint, ctx.Caster);
            }

            // El nivel del arma engorda el proyectil y su explosión. El spec es un asset
            // compartido, así que se escala aquí al lanzar y nunca se toca el original.
            float impactRadius = spec.impactRadius * ctx.SizeScale;

            projectile.Configure(damage, spec.speed, ctx.HitLayers);
            projectile.ConfigureBehaviour(spec.pierce, spec.homingTurnRate, spec.homingRange,
                                          spec.arcGravity, impactRadius, spec.impactDamage,
                                          knockbackMultiplier, spec.lifetime);
            projectile.ConfigureLifesteal(ctx.CasterHealth, ctx.Lifesteal);
            projectile.Launch(direction, ctx.Caster);

            return projectile.gameObject;
        }

        /// <summary>
        /// Monta el proyectil de código pieza a pieza, <b>una vez por instancia del pool</b>. El
        /// orden importa: <c>Projectile</c> cachea el Rigidbody2D y sus colliders en <c>Awake</c>,
        /// que corre al añadir el componente, así que va el último.
        /// </summary>
        private static Projectile BuildCodeProjectile()
        {
            var go = AbilityFx.SpawnSprite("Ability Projectile", null, Vector3.zero, Vector2.one,
                                           Color.white, 0f, null);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = go.AddComponent<Projectile>();
            projectile.PooledCode = true;
            return projectile;
        }
    }
}
