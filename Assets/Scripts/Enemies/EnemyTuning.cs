using System;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Los cinco tipos de enemigo del juego. Volar y atacar de lejos son cosas independientes,
    /// pero se enumeran juntas a propósito: así el desplegable dice exactamente lo que uno tiene
    /// en la cabeza al crear el enemigo ("es un volador a distancia") en vez de obligar a marcar
    /// dos casillas y deducirlo.
    /// </summary>
    public enum EnemyArchetype
    {
        /// <summary>No se mueve nunca. Sólo tiene rango de ataque: el jugador entra y ataca.</summary>
        Static,

        /// <summary>Camina por el suelo hasta tenerlo cerca y golpea.</summary>
        Melee,

        /// <summary>Camina por el suelo, dispara de lejos y retrocede si se le echan encima.</summary>
        Ranged,

        /// <summary>Vuela hacia el objetivo esquivando obstáculos y golpea de cerca.</summary>
        FlyingMelee,

        /// <summary>Vuela, dispara de lejos y se aparta si se le acercan.</summary>
        FlyingRanged,
    }

    /// <summary>
    /// Con qué ataca. En los cuatro arquetipos que se mueven ya lo dice el propio arquetipo; existe
    /// para el <see cref="EnemyArchetype.Static"/>, que puede ser las dos cosas: una torreta que
    /// dispara o una trampa que golpea a quien se le pone delante.
    /// </summary>
    public enum AttackKind
    {
        Melee,
        Ranged,
    }

    /// <summary>
    /// <b>Todo lo que se toca a mano de un enemigo, en un solo sitio.</b>
    ///
    /// Este bloque es la fuente de verdad: lo lleva <see cref="EnemyStats"/> en el prefab y lo
    /// lleva también la ficha del pipeline (<c>EnemyRecipe</c>), de modo que la lista de valores
    /// está escrita <b>una vez</b>. Los demás scripts del enemigo (cerebro, animación, ataques) no
    /// tienen números propios: preguntan aquí.
    ///
    /// Existe porque afinar un enemigo repartido entre <c>Health</c>, <c>Knockback</c>, la IA, el
    /// ataque y el Animator obliga a recorrer cinco componentes recordando cuál manda. Con esto se
    /// abre un enemigo, se toca un componente y ya está.
    /// </summary>
    [Serializable]
    public class EnemyTuning
    {
        // ============================================================ tipo

        [Header("Tipo")]
        [Tooltip("Decide cómo se mueve y cómo ataca. Static no persigue: sólo espera a que el " +
                 "objetivo entre en su rango de ataque.")]
        public EnemyArchetype archetype = EnemyArchetype.Melee;

        [Tooltip("Sólo para Static: si esa torreta dispara o golpea. Los arquetipos que se mueven " +
                 "ya lo llevan implícito en su propio nombre y este campo se ignora.")]
        public AttackKind staticAttack = AttackKind.Ranged;

        // ============================================================ vida

        [Header("Vida")]
        [Min(1f)] public float maxHealth = 30f;

        [Tooltip("En un enemigo va a 0 a propósito: los i-frames se tragan las armas multigolpe " +
                 "(una escopeta de 5 perdigones acertaría uno). El aturdimiento es trabajo del " +
                 "retroceso, no de la invulnerabilidad.")]
        [Min(0f)] public float invulnerabilityDuration;

        public string hurtSfxId = "";
        public string deathSfxId = "";

        // ============================================================ retroceso

        [Header("Retroceso al recibir (lo define la víctima, no quien pega)")]
        [Min(0f)] public float knockbackHorizontal = 6f;
        [Min(0f)] public float knockbackVertical = 3f;
        [Min(0f)] public float knockbackDuration = 0.18f;
        [Range(0f, 1f)] public float knockbackResistance;

        // ============================================================ rangos

        [Header("Rangos")]
        [Tooltip("El límite de la persecución, en los dos sentidos: entra aquí y el enemigo deja " +
                 "el reposo y va a por él; sale de aquí y, tras 'loseInterestGrace' segundos, se " +
                 "rinde. En un Static se ignora: un enemigo que no se mueve no necesita detectar " +
                 "nada, sólo tener al objetivo a tiro.")]
        [Min(0f)] public float detectionRange = 7f;

        [Tooltip("Segundos que el objetivo tiene que estar SEGUIDOS fuera de 'detectionRange' " +
                 "antes de que el enemigo se rinda y vuelva al reposo. El cronómetro se reinicia " +
                 "en cuanto vuelve a entrar, así que rozar el borde no apaga la persecución; hace " +
                 "de histéresis sin necesidad de un segundo radio. 0 = se rinde al salir.")]
        [Min(0f)] public float loseInterestGrace = 1f;

        [Tooltip("Donde deja de avanzar y empieza la animación de ataque. En uno a distancia es " +
                 "mucho mayor que en uno de melé — es lo que hace que se lea como que dispara.")]
        [Min(0f)] public float attackRange = 1.6f;

        [Tooltip("Espacio propio: si el objetivo se le mete más cerca de esto, retrocede hasta " +
                 "recuperar su distancia de tiro. 0 = no retrocede nunca (lo normal en un melé).")]
        [Min(0f)] public float personalSpace;

        [Tooltip("Al retroceder no vuelve a disparar hasta recuperar este múltiplo de " +
                 "'personalSpace'. >1 crea una banda muerta que quita el tembleque disparar↔" +
                 "retroceder justo en el borde. Si tiene una pared detrás y no puede alejarse más, " +
                 "deja de retroceder y pelea.")]
        [Range(1f, 2f)] public float retreatReleaseFactor = 1.35f;

        [Tooltip("Diferencia de altura máxima para dar al objetivo por alcanzable. Evita que un " +
                 "enemigo de suelo persiga a alguien dos plataformas más arriba. Un volador lo " +
                 "ignora.")]
        [Min(0f)] public float verticalTolerance = 3f;

        // ============================================================ movimiento

        [Header("Movimiento")]
        [Min(0f)] public float moveSpeed = 2.5f;

        [Tooltip("Velocidad al apartarse cuando le invaden el espacio propio.")]
        [Min(0f)] public float retreatSpeed = 2.5f;

        [Tooltip("Sondea el suelo un paso por delante y no avanza si no hay. Sólo enemigos de suelo.")]
        public bool stopAtLedges = true;

        [Min(0f)] public float ledgeProbeDepth = 0.6f;

        [Tooltip("Gravedad del cuerpo. Los voladores la ponen a 0 solos.")]
        [Min(0f)] public float gravityScale = 3f;

        [Header("Vuelo")]
        [Tooltip("Cuánto mira por delante para esquivar. Si choca con el escenario, se prueban " +
                 "desvíos a un lado y a otro hasta encontrar hueco.")]
        [Min(0f)] public float avoidProbeDistance = 1.5f;

        [Tooltip("Altura sobre el objetivo a la que le gusta quedarse. 0 = va a su misma altura.")]
        public float hoverOffset = 0.5f;

        // ============================================================ ataque

        [Header("Ataque")]
        [Min(0f)] public float attackDamage = 10f;

        [Tooltip("Segundos entre el final de un ataque y el permiso para lanzar el siguiente. " +
                 "Mientras corre, el enemigo se queda en reposo.")]
        [Min(0f)] public float attackCooldown = 1.8f;

        [Min(0f)] public float attackKnockbackMultiplier = 1f;

        [Tooltip("Segundo del clip de ataque en el que sale el golpe/proyectil, si el clip no trae " +
                 "un AnimationEvent propio. Con evento (lo normal, lo pone el pipeline) esto sobra.")]
        [Min(0f)] public float attackReleaseFallback = 0.25f;

        [Tooltip("Se queda quieto mientras ataca. Apagarlo deja que siga avanzando durante el golpe.")]
        public bool rootedWhileAttacking = true;

        [Header("Ataque · melé")]
        [Tooltip("Caja de golpe, en unidades de mundo, medida desde el enemigo hacia donde mira.")]
        public Vector2 meleeHitboxSize = new Vector2(1.2f, 1f);

        public Vector2 meleeHitboxOffset = new Vector2(0.8f, 0.5f);

        [Header("Ataque · a distancia")]
        [Tooltip("El proyectil que lanza. Con 'prefab' vacío se construye uno en código con el " +
                 "sprite del ataque; con prefab, sale de ahí (y va por pool igual).")]
        public ProjectileSpec projectile = new ProjectileSpec();

        [Tooltip("Apunta al objetivo en vez de disparar en horizontal. Un tiro plano falla en " +
                 "cuanto hay un desnivel entre los dos.")]
        public bool aimAtTarget = true;

        [Tooltip("Sólo se usa si el proyectil NO tiene prefab: es el sprite del que se construye " +
                 "en código. Con prefab manda el prefab.")]
        public Sprite projectileSprite;

        public Color projectileTint = Color.white;

        // ============================================================ contacto

        [Header("Daño por contacto")]
        [Tooltip("Daño al tocar al objetivo con el cuerpo. 0 = no hace daño por contacto. Un " +
                 "enemigo a distancia normalmente lo lleva a 0 o muy bajo.")]
        [Min(0f)] public float contactDamage;

        [Min(0f)] public float contactDamageCooldown = 1f;
        [Min(0f)] public float contactKnockbackMultiplier = 1f;

        // ============================================================ animación

        [Header("Animación (velocidad de cada estado)")]
        [Tooltip("Multiplicador de velocidad del clip de reposo. 1 = tal cual se generó.")]
        [Min(0.01f)] public float idleAnimSpeed = 1f;

        [Min(0.01f)] public float moveAnimSpeed = 1f;
        [Min(0.01f)] public float attackAnimSpeed = 1f;
        [Min(0.01f)] public float hurtAnimSpeed = 1f;
        [Min(0.01f)] public float deathAnimSpeed = 1f;

        // ============================================================ objetivo

        [Header("Objetivo")]
        [Tooltip("Etiqueta de a quién persigue y a quién daña.")]
        public string targetTag = "Player";

        [Tooltip("Capas a las que puede dañar el ataque.")]
        public LayerMask hitLayers = ~0;

        [Tooltip("Capas que cuentan como suelo/obstáculo para el sondeo de bordes y el esquive.")]
        public LayerMask obstacleLayers = 1 << 6;   // 'Ground'

        // ============================================================ derivados

        /// <summary>True si el arquetipo vuela (sin gravedad, se mueve en los dos ejes).</summary>
        public bool Flies => archetype is EnemyArchetype.FlyingMelee or EnemyArchetype.FlyingRanged;

        /// <summary>
        /// True si ataca lanzando un proyectil en vez de con una caja de golpe. El estático lo
        /// decide con <see cref="staticAttack"/>; los demás lo llevan en el arquetipo.
        /// </summary>
        public bool IsRanged => archetype == EnemyArchetype.Static
            ? staticAttack == AttackKind.Ranged
            : archetype is EnemyArchetype.Ranged or EnemyArchetype.FlyingRanged;

        /// <summary>True si el arquetipo se desplaza. Un Static nunca persigue ni retrocede.</summary>
        public bool Moves => archetype != EnemyArchetype.Static;

        /// <summary>
        /// True si este enemigo <b>huye</b> cuando le invaden el espacio propio. Sólo los que se
        /// mueven <b>y</b> disparan: apartarse es lo que hace que un enemigo a distancia se lea
        /// como tal. Un melé persigue y pega — no tiene ningún motivo para retroceder — y un
        /// <see cref="EnemyArchetype.Static"/> no se mueve en absoluto.
        ///
        /// Existe como propiedad y no como tres comprobaciones sueltas porque la regla la usan el
        /// cerebro, los gizmos y el inspector, y ya se desincronizaron una vez: el inspector
        /// escondía el campo en un melé mientras el cerebro seguía leyéndolo, así que un melé al
        /// que se le había cambiado el arquetipo desde Ranged seguía huyendo con un
        /// <see cref="personalSpace"/> heredado que ya no se veía por ninguna parte.
        /// </summary>
        public bool Retreats => Moves && IsRanged && personalSpace > 0f;

        /// <summary>Copia profunda: la ficha del pipeline no debe compartir instancia con el prefab.</summary>
        public EnemyTuning Clone()
        {
            var copy = (EnemyTuning)MemberwiseClone();
            copy.projectile = CloneSpec(projectile);
            return copy;
        }

        private static ProjectileSpec CloneSpec(ProjectileSpec source)
        {
            if (source == null) return new ProjectileSpec();

            return new ProjectileSpec
            {
                prefab = source.prefab,
                speed = source.speed,
                lifetime = source.lifetime,
                size = source.size,
                muzzleOffset = source.muzzleOffset,
                pierce = source.pierce,
                homingTurnRate = source.homingTurnRate,
                homingRange = source.homingRange,
                arcGravity = source.arcGravity,
                impactRadius = source.impactRadius,
                impactDamage = source.impactDamage,
            };
        }
    }
}
