using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>Qué sale en una suelta del gesto.</summary>
    public enum ReleaseShotKind
    {
        /// <summary>Nada: un amago (el gesto hace el movimiento, pero no sale ataque).</summary>
        Nothing,

        /// <summary>Un proyectil.</summary>
        Projectile,

        /// <summary>Varios proyectiles abiertos en abanico.</summary>
        Fan,

        /// <summary>Un anillo de proyectiles en todas direcciones.</summary>
        Ring,

        /// <summary>Un golpe cuerpo a cuerpo: una caja de daño delante del cuerpo.</summary>
        Strike,
    }

    /// <summary>Hacia dónde sale.</summary>
    public enum ReleaseAim
    {
        /// <summary>
        /// A donde estaba el objetivo cuando EMPEZÓ el ataque (lo que apunta el brazo durante el
        /// aviso): se puede esquivar moviéndose. Quien no congela un punto (un jefe) apunta en vivo.
        /// </summary>
        Frozen,

        /// <summary>A donde está el objetivo en el instante de esta suelta.</summary>
        Live,

        /// <summary>Recto hacia donde mira el cuerpo (más <see cref="ReleaseShot.angle"/>).</summary>
        Forward,
    }

    /// <summary>
    /// Lo que sale en UNA suelta de un gesto: un proyectil, un abanico, un anillo, un golpe… o nada.
    /// Sale del punto de esa suelta (<c>RigRelease.point</c>; por defecto, la boca del arma).
    /// </summary>
    [Serializable]
    public class ReleaseShot
    {
        public ReleaseShotKind kind = ReleaseShotKind.Projectile;

        [Tooltip("Hacia dónde sale: a donde estaba el objetivo al empezar el ataque (se puede esquivar), " +
                 "a donde está en el instante de esta suelta, o recto hacia delante.")]
        [ShowIf("kind", "Projectile", "Fan", "Ring")]
        public ReleaseAim aim = ReleaseAim.Frozen;

        [Tooltip("Grados que se suman a la dirección (con 'Forward', el ángulo desde el frente: 90 = hacia arriba).")]
        [ShowIf("kind", "Projectile", "Fan", "Ring")]
        public float angle;

        [Tooltip("Proyectiles del abanico o del anillo.")]
        [ShowIf("kind", "Fan", "Ring")]
        [Min(1)] public int count = 5;

        [Tooltip("Abanico: apertura total en grados.")]
        [ShowIf("kind", "Fan")]
        [Range(0f, 360f)] public float arcDegrees = 50f;

        [Tooltip("Los proyectiles nacen a esta distancia del punto de salida, en su dirección.")]
        [ShowIf("kind", "Fan", "Ring")]
        [Min(0f)] public float spawnRadius;

        [Tooltip("Cómo vuela cada proyectil de esta suelta. Sin arte = el placeholder (bola del color del ataque).")]
        [ShowIf("kind", "Projectile", "Fan", "Ring")]
        public ProjectileSpec projectile = new ProjectileSpec();

        [Tooltip("Golpe: tamaño de la caja de daño.")]
        [ShowIf("kind", "Strike")]
        public Vector2 strikeSize = new Vector2(1.4f, 1.2f);

        [Tooltip("Golpe: centro de la caja respecto al cuerpo, mirando a la derecha (la X se voltea).")]
        [ShowIf("kind", "Strike")]
        public Vector2 strikeOffset = new Vector2(1f, 0f);

        [Tooltip("Daño de esta suelta. 0 = el del ataque ('damage').")]
        [ShowIf("kind", "Projectile", "Fan", "Ring", "Strike")]
        [Min(0f)] public float damage;
    }

    /// <summary>
    /// El ataque que <b>sale en las sueltas de su gesto</b>: el gesto del cuerpo es el aviso y, en cada
    /// instante en que suelta, sale lo que diga <see cref="shots"/> para esa suelta — un proyectil, un
    /// abanico, un anillo, un golpe o nada. "Dispara dos veces, se le encasquilla, dispara otra vez" son
    /// cuatro sueltas con Projectile · Projectile · Nothing · Projectile.
    ///
    /// Es genérico: sirve a un jefe y a un enemigo normal, con rig por partes (cada suelta sale de su
    /// punto) o con sprite sheet (una sola suelta; las demás van a ritmo de
    /// <see cref="timeBetweenShots"/>). El disparo simple de toda la vida es este ataque con una suelta.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Release Attack", fileName = "BossAttack_Release")]
    public class ReleaseAttack : BossAttack
    {
        [Header("Qué sale en cada suelta del gesto")]
        [Tooltip("Una entrada por suelta del gesto, en orden. Si el gesto tiene más sueltas que entradas, " +
                 "las que sobran no lanzan nada.")]
        [PerRelease]
        [SerializeField] private List<ReleaseShot> shots = new List<ReleaseShot> { new ReleaseShot() };

        [Tooltip("Segundos entre una entrada y la siguiente cuando el cuerpo no tiene una suelta para " +
                 "ella (sin gesto, o un cuerpo de sprite sheet, que suelta una sola vez).")]
        [Min(0.02f)]
        [SerializeField] private float timeBetweenShots = 0.25f;

        [Header("Arte")]
        [Tooltip("Sprite de los proyectiles sin prefab propio. Vacío = el del dueño.")]
        [SerializeField] private Sprite projectileSprite;

        public IReadOnlyList<ReleaseShot> Shots => shots;

        public override string ShortStats() => $"{Damage:0} dmg · {shots.Count} suelta(s)";

        // El aviso es el gesto: no pinta marcas.

        public override IEnumerator Run(BossContext ctx)
        {
            var body = ctx.HasHost ? ctx.Host.Body : null;

            for (int i = 0; i < shots.Count; i++)
            {
                if (!ctx.IsValid) yield break;

                if (i > 0)
                {
                    // La entrada i sale en la suelta i del gesto; sin esa suelta, por tiempo.
                    if (body != null && body.ReleaseTotal > i)
                    {
                        float timeout = 8f;
                        while (ctx.IsValid && body.ReleaseCount <= i && body.ReleaseTotal > i && timeout > 0f)
                        {
                            timeout -= Time.deltaTime;
                            yield return null;
                        }
                    }
                    else
                    {
                        yield return new WaitForSeconds(ctx.Scaled(timeBetweenShots));
                    }

                    if (!ctx.IsValid) yield break;
                }

                Fire(ctx, shots[i]);
            }
        }

        private void Fire(in BossContext ctx, ReleaseShot shot)
        {
            if (shot == null || shot.kind == ReleaseShotKind.Nothing) return;

            Impact();
            ctx.Host.OnShotFired();

            float damage = shot.damage > 0f ? shot.damage * ctx.Ability.DamageScale : ScaledDamage(ctx);

            if (shot.kind == ReleaseShotKind.Strike)
            {
                int facing = Facing(ctx);
                Vector2 center = (Vector2)ctx.Host.transform.position +
                                 new Vector2(shot.strikeOffset.x * facing, shot.strikeOffset.y);
                AbilityHit.DamageBox(ctx.Ability, center, shot.strikeSize, 0f, damage, KnockbackMultiplier, center);
                BossHitboxDebug.Box(center, shot.strikeSize, ctx.Accent);
                return;
            }

            Vector2 origin = ctx.CastPoint;
            float baseAngle = AimAngle(ctx, shot, origin);
            var sprite = projectileSprite != null ? projectileSprite : ctx.FxSprite;

            int count = shot.kind == ReleaseShotKind.Projectile ? 1 : Mathf.Max(1, shot.count);
            bool ring = shot.kind == ReleaseShotKind.Ring;

            // Anillo: los 360º repartidos entre todos; abanico: entre extremos (uno solo sale recto).
            float arc = ring ? 360f : shot.arcDegrees;
            float step = ring ? 360f / count : (count > 1 ? arc / (count - 1) : 0f);
            float start = ring || count == 1 ? baseAngle : baseAngle - arc * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float radians = (start + step * i) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                ProjectileFactory.Spawn(ctx.Ability, shot.projectile, origin + direction * shot.spawnRadius, direction,
                                        damage, KnockbackMultiplier, sprite, ctx.Accent);
            }
        }

        /// <summary>Lado hacia el que va el ataque: el del punto apuntado, o el del objetivo.</summary>
        private static int Facing(in BossContext ctx)
        {
            var frozen = ctx.Host.AimPoint;
            if (frozen.HasValue) return frozen.Value.x >= ctx.Host.transform.position.x ? 1 : -1;
            return ctx.Facing;
        }

        /// <summary>Grados de la dirección de salida de esa suelta, desde su punto de salida.</summary>
        private static float AimAngle(in BossContext ctx, ReleaseShot shot, Vector2 origin)
        {
            int facing = Facing(ctx);

            if (shot.aim == ReleaseAim.Forward) return facing >= 0 ? shot.angle : 180f - shot.angle;

            var frozen = ctx.Host.AimPoint;
            Vector2 target = shot.aim == ReleaseAim.Frozen && frozen.HasValue ? frozen.Value : ctx.PlayerCenter;
            Vector2 delta = target - origin;
            if (delta.sqrMagnitude < 0.0001f) delta = new Vector2(facing, 0f);

            // El ángulo extra se suma "hacia arriba" mire a donde mire.
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + shot.angle * (delta.x >= 0f ? 1f : -1f);
        }
    }
}
