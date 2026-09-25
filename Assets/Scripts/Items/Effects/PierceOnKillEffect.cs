using System;
using System.ComponentModel;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Capa: un proyectil que mata desaparece, espera y reaparece en el cuerpo del muerto con la
    /// misma dirección y velocidad, así que sigue cazando. Vale para cualquier proyectil de arma
    /// (recto, auto-mira, parábola, hijos de split, con o sin prefab): lo aplica
    /// <see cref="ShotProjectile"/> leyendo <see cref="CombatModifiers.PierceOnKill"/>.
    /// </summary>
    [Serializable, DisplayName("Proyectil · Sigue tras matar")]
    public sealed class PierceOnKillEffect : ItemEffect
    {
        [Tooltip("Segundos que el proyectil pasa escondido antes de reaparecer en el cuerpo del muerto.")]
        [Min(0f)] public float respawnDelay = 0.5f;

        public override void OnEquip(ItemEffectContext context) =>
            CombatModifiers.SetPierceOnKill(context, respawnDelay);

        // Se reescribe cada frame para que retocar el valor en Play se note al momento.
        public override void Tick(ItemEffectContext context, float deltaTime) =>
            CombatModifiers.SetPierceOnKill(context, respawnDelay);

        public override void OnUnequip(ItemEffectContext context) => CombatModifiers.Remove(context);

        public override string Summary() =>
            $"Al matar con un proyectil, reaparece en el cuerpo {respawnDelay:0.##} s después y sigue su camino";
    }
}
