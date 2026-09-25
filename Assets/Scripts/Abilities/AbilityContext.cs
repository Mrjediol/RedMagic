using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Todo lo que una habilidad necesita saber de quien la lanza. Se construye una vez por
    /// lanzamiento en <see cref="AbilityUser"/> y se pasa por valor a
    /// <see cref="AbilityDefinition.Execute"/>.
    ///
    /// Existe para que las habilidades sean <b>ScriptableObjects sin estado</b>: el asset describe
    /// el efecto y nada más, así que el mismo asset lo puede usar el jugador, un enemigo o un
    /// tótem invocado sin copiarlo. Cualquier dato del lanzador vive aquí, nunca en el asset.
    /// </summary>
    public readonly struct AbilityContext
    {
        /// <summary>Quien lanza. Se excluye de sus propios daños y de las colisiones.</summary>
        public readonly GameObject Caster;

        /// <summary>MonoBehaviour vivo sobre el que arrancar corrutinas (el propio AbilityUser).</summary>
        public readonly MonoBehaviour Runner;

        public readonly Health CasterHealth;

        /// <summary>Capas contra las que esta habilidad puede impactar.</summary>
        public readonly LayerMask HitLayers;

        /// <summary>-1 izquierda, 1 derecha.</summary>
        public readonly int Facing;

        /// <summary>Dirección de apuntado ya normalizada (por defecto, la horizontal de Facing).</summary>
        public readonly Vector2 Aim;

        /// <summary>
        /// Etiqueta "amiga": nada con esta etiqueta recibe daño. Es lo que separa un lanzamiento
        /// del jugador de uno enemigo sin necesidad de montar capas ni un sistema de bandos: el
        /// lanzador pasa su propia etiqueta y no puede dañarse a sí mismo ni a los suyos.
        /// </summary>
        public readonly string FriendlyTag;

        /// <summary>Multiplicador global de daño del lanzador (nivel del arma, buffs temporales).</summary>
        public readonly float DamageScale;

        /// <summary>
        /// Multiplicador de tamaño y alcance del lanzamiento (nivel del arma). Cada arquetipo lo
        /// aplica a su geometría: el haz se alarga y engorda, la onda crece de radio, el proyectil
        /// se hace más gordo…
        /// </summary>
        public readonly float SizeScale;

        /// <summary>Fracción del daño hecho que cura al lanzador. 0 = nada.</summary>
        public readonly float Lifesteal;

        /// <summary>Nivel del arma con el que se lanza (1..3). Sólo informativo para efectos.</summary>
        public readonly int Level;

        public AbilityContext(GameObject caster, MonoBehaviour runner, Health casterHealth,
                              LayerMask hitLayers, int facing, Vector2 aim, string friendlyTag,
                              float damageScale = 1f, float sizeScale = 1f, float lifesteal = 0f,
                              int level = 1)
        {
            SizeScale = sizeScale <= 0f ? 1f : sizeScale;
            Lifesteal = Mathf.Clamp01(lifesteal);
            Level = Mathf.Max(1, level);

            Caster = caster;
            Runner = runner;
            CasterHealth = casterHealth;
            HitLayers = hitLayers;
            Facing = facing < 0 ? -1 : 1;
            Aim = aim.sqrMagnitude < 0.0001f ? new Vector2(Facing, 0f) : aim.normalized;
            FriendlyTag = friendlyTag;
            DamageScale = damageScale <= 0f ? 1f : damageScale;
        }

        public bool IsValid => Caster != null && Runner != null;

        public Vector2 Origin => Caster != null ? (Vector2)Caster.transform.position : Vector2.zero;

        /// <summary>Punto de salida: la X del offset se invierte según hacia dónde se mira.</summary>
        public Vector2 Muzzle(Vector2 offset) => Origin + new Vector2(offset.x * Facing, offset.y);
    }

    /// <summary>
    /// Utilidades de impacto compartidas por todas las habilidades: buscar objetivos en una caja o
    /// un círculo y aplicarles daño + retroceso.
    ///
    /// El filtrado de objetivos vive aquí y no en cada habilidad para que la regla sea una sola:
    /// se ignora al propio lanzador, a los muertos, a los que llevan su misma etiqueta y a los
    /// repetidos (un enemigo con varios colliders es un objetivo, no tres).
    /// </summary>
    public static class AbilityHit
    {
        private static readonly Collider2D[] Buffer = new Collider2D[64];
        private static readonly HashSet<Health> Seen = new HashSet<Health>();
        private static readonly List<Health> Results = new List<Health>();

        /// <summary>Daña a un objetivo concreto. Devuelve false si el golpe no ha entrado.</summary>
        public static bool Damage(Health target, in AbilityContext ctx, float damage,
                                  Vector2 sourcePosition, float knockbackMultiplier)
        {
            if (!IsValidTarget(target, ctx)) return false;

            float amount = damage * ctx.DamageScale;

            // Bono plano de las pasivas legendarias de daño — sólo cuenta cuando quien golpea es el
            // jugador, nunca en un ataque de enemigo/jefe que también pase por aquí.
            bool byPlayer = Teams.Of(ctx.Caster) == Team.Player;
            if (byPlayer)
                amount += Economy.LegendaryPassiveEffects.AttackDamageBonus;

            // Lo del jugador pasa por PlayerHit (bonos de items, ralentización, avisos de baja); lo
            // de enemigos y jefes va directo, como siempre.
            bool landed = byPlayer
                ? Items.PlayerHit.Deal(target, amount, sourcePosition, knockbackMultiplier, Items.HitKind.Ability)
                : target.TakeDamage(amount, sourcePosition, knockbackMultiplier);
            if (!landed) return false;

            // Robo de vida: se cura por el daño que se pretendía hacer, no por la vida que le
            // quedaba al objetivo. Rematar a un enemigo con 1 de vida cura igual que golpearlo
            // entero, que es lo que espera quien lleva el arma.
            if (ctx.Lifesteal > 0f && ctx.CasterHealth != null)
                ctx.CasterHealth.Heal(amount * ctx.Lifesteal);

            return true;
        }

        public static bool IsValidTarget(Health target, in AbilityContext ctx)
        {
            if (target == null || target.IsDead) return false;
            if (ctx.CasterHealth != null && target == ctx.CasterHealth) return false;
            if (ctx.Caster != null && target.transform.IsChildOf(ctx.Caster.transform)) return false;
            if (!string.IsNullOrEmpty(ctx.FriendlyTag) && target.CompareTag(ctx.FriendlyTag)) return false;

            // La regla de bandos, por encima de la etiqueta amiga: sólo el jugador daña a los
            // enemigos y sólo los enemigos al jugador. Ver Combat.Teams — la etiqueta amiga no
            // basta porque los enemigos del pipeline nacen sin etiqueta.
            if (Teams.Allied(ctx.Caster, target)) return false;

            return true;
        }

        /// <summary>
        /// Objetivos válidos dentro de un círculo. La lista devuelta es un buffer compartido: úsala
        /// y olvídala, no la guardes (la siguiente llamada la reutiliza).
        /// </summary>
        public static List<Health> OverlapCircle(in AbilityContext ctx, Vector2 center, float radius)
        {
            var filter = MakeFilter(ctx);
            int count = Physics2D.OverlapCircle(center, radius, filter, Buffer);
            return Collect(count, ctx);
        }

        /// <summary>Objetivos válidos dentro de una caja (ángulo en grados).</summary>
        public static List<Health> OverlapBox(in AbilityContext ctx, Vector2 center, Vector2 size, float angle = 0f)
        {
            var filter = MakeFilter(ctx);
            int count = Physics2D.OverlapBox(center, size, angle, filter, Buffer);
            return Collect(count, ctx);
        }

        /// <summary>Daña a todo lo que haya en un círculo. Devuelve cuántos han recibido el golpe.</summary>
        public static int DamageCircle(in AbilityContext ctx, Vector2 center, float radius,
                                       float damage, float knockbackMultiplier)
        {
            int hits = 0;
            var targets = OverlapCircle(ctx, center, radius);
            for (int i = 0; i < targets.Count; i++)
                if (Damage(targets[i], ctx, damage, center, knockbackMultiplier)) hits++;
            return hits;
        }

        /// <summary>Daña a todo lo que haya en una caja. Devuelve cuántos han recibido el golpe.</summary>
        public static int DamageBox(in AbilityContext ctx, Vector2 center, Vector2 size, float angle,
                                    float damage, float knockbackMultiplier, Vector2 knockbackFrom)
        {
            int hits = 0;
            var targets = OverlapBox(ctx, center, size, angle);
            for (int i = 0; i < targets.Count; i++)
                if (Damage(targets[i], ctx, damage, knockbackFrom, knockbackMultiplier)) hits++;
            return hits;
        }

        private static ContactFilter2D MakeFilter(in AbilityContext ctx) => new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = ctx.HitLayers,
            useTriggers = true
        };

        private static List<Health> Collect(int count, in AbilityContext ctx)
        {
            Results.Clear();
            Seen.Clear();

            for (int i = 0; i < count; i++)
            {
                var collider = Buffer[i];
                if (collider == null) continue;

                var health = collider.GetComponentInParent<Health>();
                if (!IsValidTarget(health, ctx)) continue;
                if (!Seen.Add(health)) continue;      // un objetivo con varios colliders es uno

                Results.Add(health);
            }

            return Results;
        }
    }
}
