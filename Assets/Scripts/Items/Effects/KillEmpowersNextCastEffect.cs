using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Bastón: cada baja deja el arma lista al instante (cooldown a 0) y el siguiente disparo hace
    /// ×N de daño. Sin indicador en el HUD a propósito: se carga solo con cada baja y el jugador no
    /// tiene nada que decidir. El disparo que gasta la carga puede volver a cargarla si mata con ella.
    /// </summary>
    [Serializable, DisplayName("Al matar · Siguiente disparo instantáneo y potenciado")]
    public sealed class KillEmpowersNextCastEffect : ItemEffect
    {
        [Tooltip("Multiplicador de daño del disparo cargado.")]
        [Min(1f)] public float damageMultiplier = 3f;

        [Tooltip("La baja pone el cooldown del arma a 0 (el disparo cargado sale ya).")]
        public bool resetCooldown = true;

        private sealed class State
        {
            public bool Armed;
            public Action<KillInfo> OnKill;
            public Action<CastArgs> OnCast;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();

            state.OnKill = _ =>
            {
                state.Armed = true;
                if (resetCooldown) WeaponUser.Current?.ResetCooldown();
            };
            state.OnCast = args =>
            {
                if (!state.Armed) return;
                state.Armed = false;
                args.DamageScale *= damageMultiplier;
            };

            PlayerHit.Killed += state.OnKill;
            WeaponUser.Casting += state.OnCast;
        }

        public override void OnUnequip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            PlayerHit.Killed -= state.OnKill;
            WeaponUser.Casting -= state.OnCast;
            state.Armed = false;
        }

        public override string Summary() =>
            Loc.Get("effect.kill_empowers_next_cast", damageMultiplier);
    }
}
