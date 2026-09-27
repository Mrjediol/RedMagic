using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Todo lo que la resolución de disparo necesita saber de quien aprieta el gatillo. Se
    /// construye una vez por disparo y se pasa por valor, igual que <c>AbilityContext</c> en el
    /// sistema de habilidades: así el arma y sus modificadores son datos sin estado del lanzador.
    /// </summary>
    public readonly struct ShotContext
    {
        /// <summary>Quien dispara. Se excluye de sus propios impactos.</summary>
        public readonly GameObject Caster;

        /// <summary>MonoBehaviour vivo para corrutinas (ráfagas, auto-repetición en el aire…). Opcional.</summary>
        public readonly MonoBehaviour Runner;

        public readonly Health CasterHealth;

        /// <summary>Capas contra las que impactan los proyectiles.</summary>
        public readonly LayerMask HitLayers;

        /// <summary>-1 izquierda, 1 derecha.</summary>
        public readonly int Facing;

        /// <summary>Dirección de apuntado ya normalizada.</summary>
        public readonly Vector2 Aim;

        /// <summary>Nada con esta etiqueta recibe daño (bando del lanzador).</summary>
        public readonly string FriendlyTag;

        /// <summary>Multiplicador global de daño (nivel del arma, sinergias, buffs). 1 = neutro.</summary>
        public readonly float DamageScale;

        /// <summary>Aviso de primer impacto del disparo (null si nadie lo pidió). Ver <see cref="ShotImpactHook"/>.</summary>
        public readonly ShotImpactHook Impact;
        /// <summary>Sus proyectiles salen dorados sí o sí (<see cref="GoldMark"/>).</summary>
        public readonly bool ForceGilded;

        public ShotContext(GameObject caster, MonoBehaviour runner, Health casterHealth,
                           LayerMask hitLayers, int facing, Vector2 aim, string friendlyTag,
                           float damageScale = 1f, ShotImpactHook impact = null, bool forceGilded = false)
        {
            Impact = impact;
            ForceGilded = forceGilded;
            Caster = caster;
            Runner = runner;
            CasterHealth = casterHealth;
            HitLayers = hitLayers;
            Facing = facing < 0 ? -1 : 1;
            Aim = aim.sqrMagnitude < 0.0001f ? new Vector2(Facing, 0f) : aim.normalized;
            FriendlyTag = friendlyTag;
            DamageScale = damageScale <= 0f ? 1f : damageScale;
        }

        public bool IsValid => Caster != null;

        public Vector2 Origin => Caster != null ? (Vector2)Caster.transform.position : Vector2.zero;

        /// <summary>Punto de salida: la X del offset se invierte según hacia dónde se mira.</summary>
        public Vector2 Muzzle(Vector2 offset) => Origin + new Vector2(offset.x * Facing, offset.y);
    }
}
