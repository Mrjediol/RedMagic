using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Todo lo que un ataque de jefe necesita saber del combate en curso. Se construye por
    /// lanzamiento en <see cref="BossController"/> y se pasa a <see cref="BossAttack.Run"/>.
    ///
    /// Existe por la misma razón que <see cref="AbilityContext"/>: los ataques son
    /// <b>ScriptableObjects sin estado</b> (el asset describe el patrón y nada más), así que todo
    /// lo que dependa del jefe concreto, de la arena o de la fase vive aquí.
    ///
    /// Lleva dentro un <see cref="AbilityContext"/> montado a partir del jefe, de modo que los
    /// ataques reutilizan tal cual <see cref="AbilityHit"/> (filtrado de objetivos, daño en caja y
    /// en círculo) y <see cref="ProjectileFactory"/> (proyectiles <b>pooled</b>) en vez de tener su
    /// propio sistema paralelo.
    /// </summary>
    public readonly struct BossContext
    {
        public readonly BossController Boss;
        public readonly Transform Player;

        /// <summary>Contexto de habilidad equivalente: es lo que se pasa a AbilityHit / ProjectileFactory.</summary>
        public readonly AbilityContext Ability;

        /// <summary>Altura del suelo de la arena, medida por raycast al empezar el combate.</summary>
        public readonly float GroundY;

        /// <summary>Media anchura de la arena respecto al jefe. Marca hasta dónde barren los ataques.</summary>
        public readonly float ArenaHalfWidth;

        /// <summary>Altura útil de la arena sobre el suelo (de dónde cae la lluvia de proyectiles).</summary>
        public readonly float ArenaHeight;

        /// <summary>
        /// Multiplicador de ritmo de la fase: >1 acorta todas las esperas del ataque. Es lo que
        /// hace que la fase 2 sea el mismo patrón pero agobiante, sin duplicar assets.
        /// </summary>
        public readonly float SpeedScale;

        /// <summary>Sprite de los efectos del jefe. Vacío = el cuadrado por defecto de AbilityFx.</summary>
        public readonly Sprite FxSprite;

        /// <summary>Color de la fase, para teñir avisos y proyectiles sin arte propio.</summary>
        public readonly Color Accent;

        public BossContext(BossController boss, Transform player, in AbilityContext ability,
                           float groundY, float arenaHalfWidth, float arenaHeight,
                           float speedScale, Sprite fxSprite, Color accent)
        {
            Boss = boss;
            Player = player;
            Ability = ability;
            GroundY = groundY;
            ArenaHalfWidth = Mathf.Max(1f, arenaHalfWidth);
            ArenaHeight = Mathf.Max(1f, arenaHeight);
            SpeedScale = speedScale <= 0f ? 1f : speedScale;
            FxSprite = fxSprite;
            Accent = accent.a <= 0f ? Color.white : accent;
        }

        public bool IsValid => Boss != null && Boss.gameObject.activeInHierarchy;

        public Vector2 Origin => Boss != null ? (Vector2)Boss.transform.position : Vector2.zero;

        public Vector2 PlayerPosition => Player != null ? (Vector2)Player.position : Origin;

        /// <summary>Dirección normalizada hacia el jugador. Sin jugador apunta hacia donde mira el jefe.</summary>
        public Vector2 AimAtPlayer
        {
            get
            {
                Vector2 delta = PlayerPosition - Origin;
                return delta.sqrMagnitude < 0.0001f ? new Vector2(Facing, 0f) : delta.normalized;
            }
        }

        /// <summary>-1 si el jugador está a la izquierda, 1 si está a la derecha.</summary>
        public int Facing
        {
            get
            {
                if (Player == null || Boss == null) return 1;
                return Player.position.x < Boss.transform.position.x ? -1 : 1;
            }
        }

        /// <summary>Borde izquierdo / derecho de la arena, en X.</summary>
        public float ArenaMinX => Origin.x - ArenaHalfWidth;
        public float ArenaMaxX => Origin.x + ArenaHalfWidth;

        /// <summary>Techo de la arena: de aquí caen los proyectiles de lluvia.</summary>
        public float CeilingY => GroundY + ArenaHeight;

        /// <summary>
        /// Convierte una espera "de diseño" en la espera real de esta fase. Todo tiempo dentro de
        /// un ataque debe pasar por aquí para que <see cref="SpeedScale"/> signifique algo.
        /// </summary>
        public float Scaled(float seconds) => seconds / SpeedScale;
    }
}
