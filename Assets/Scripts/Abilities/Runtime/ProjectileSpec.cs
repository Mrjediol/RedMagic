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

    /// <summary>Instancia proyectiles a partir de un <see cref="ProjectileSpec"/>.</summary>
    public static class ProjectileFactory
    {
        /// <summary>
        /// Lanza un proyectil desde <paramref name="origin"/> en <paramref name="direction"/>.
        /// Devuelve el objeto creado (o null si el prefab estaba mal montado).
        /// </summary>
        public static GameObject Spawn(in AbilityContext ctx, ProjectileSpec spec, Vector2 origin,
                                       Vector2 direction, float damage, float knockbackMultiplier,
                                       Sprite sprite, Color tint)
        {
            if (spec == null) return null;

            GameObject instance = spec.prefab != null
                ? UnityEngine.Object.Instantiate(spec.prefab, origin, Quaternion.identity)
                : BuildProjectile(ctx, spec, origin, sprite, tint);

            // El nivel del arma engorda el proyectil y su explosión. El spec es un asset
            // compartido, así que se escala aquí al lanzar y nunca se toca el original.
            float impactRadius = spec.impactRadius * ctx.SizeScale;

            var projectile = instance.GetComponent<Projectile>();
            if (projectile == null)
            {
                Debug.LogWarning($"[Abilities] El prefab '{instance.name}' no tiene componente Projectile.");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            projectile.Configure(damage, spec.speed, ctx.HitLayers);
            projectile.ConfigureBehaviour(spec.pierce, spec.homingTurnRate, spec.homingRange,
                                          spec.arcGravity, impactRadius, spec.impactDamage,
                                          knockbackMultiplier, spec.lifetime);
            projectile.ConfigureLifesteal(ctx.CasterHealth, ctx.Lifesteal);
            projectile.Launch(direction, ctx.Caster);

            return instance;
        }

        /// <summary>
        /// Monta el proyectil pieza a pieza. El orden importa: <c>Projectile</c> cachea el
        /// Rigidbody2D y sus colliders en <c>Awake</c>, que se ejecuta en cuanto se añade el
        /// componente, así que va el último.
        /// </summary>
        private static GameObject BuildProjectile(in AbilityContext ctx, ProjectileSpec spec,
                                                  Vector2 origin, Sprite sprite, Color tint)
        {
            var go = AbilityFx.SpawnSprite("Ability Projectile", sprite, origin,
                                           spec.size * ctx.SizeScale, tint, 0f, ctx.Caster);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            // El collider vive en espacio local, ya escalado por SpawnSprite: en local el sprite
            // mide su bounds original, así que medio bounds es el radio que se ve en pantalla.
            var renderer = go.GetComponent<SpriteRenderer>();
            var localSize = renderer != null && renderer.sprite != null
                ? (Vector2)renderer.sprite.bounds.size
                : Vector2.one;
            collider.radius = Mathf.Max(localSize.x, localSize.y) * 0.5f;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = go.AddComponent<Projectile>();
            projectile.DestroyWhenDone();

            return go;
        }
    }
}
