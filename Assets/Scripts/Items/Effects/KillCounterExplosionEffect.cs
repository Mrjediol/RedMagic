using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Yelmo: cuenta bajas; a la N-ésima queda LISTO y el siguiente disparo suelta una gran
    /// explosión de hielo en su PRIMER IMPACTO (<see cref="CastArgs.OnFirstImpact"/>: el enemigo
    /// golpeado, o donde acabe el proyectil si no toca a nadie) (<see cref="IceBurst"/>, efecto
    /// <c>Fx_IceExplosion</c> escalado al radio). El contador se ve siempre en el HUD mientras el
    /// Yelmo está equipado (0/5 … 4/5 → ¡LISTO!). Estando listo, las bajas no se acumulan; al
    /// soltar la explosión vuelve a 0 (y sus propias bajas ya cuentan para la siguiente).
    /// </summary>
    [Serializable, DisplayName("Al matar N · Explosión de hielo en el siguiente disparo")]
    public sealed class KillCounterExplosionEffect : ItemEffect
    {
        [Tooltip("Bajas para cargar la explosión.")]
        [Min(1)] public int killsRequired = 5;

        [Tooltip("Radio de la explosión (el dibujo se escala a este radio).")]
        [Min(0.5f)] public float radius = 4.5f;

        [Tooltip("Daño a cada enemigo del radio.")]
        [Min(0f)] public float damage = 40f;

        [Tooltip("Ralentiza a los supervivientes (aunque no haya Hielo 2).")]
        public bool slowsTargets = true;

        [Tooltip("Efecto de la explosión: Assets/Prefabs/Fx/Items/Fx_IceExplosion. Vacío = destello " +
                 "azul de código.")]
        public GameObject explosionPrefab;

        [Tooltip("Color de la chapa del HUD.")]
        public Color hudAccent = new Color(0.55f, 0.9f, 1f);

        private sealed class State
        {
            public int Kills;
            public bool Ready;
            public Action<KillInfo> OnKill;
            public Action<CastArgs> OnCast;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();

            state.OnKill = _ =>
            {
                if (state.Ready) return;
                state.Kills++;
                if (state.Kills >= killsRequired) state.Ready = true;
            };
            state.OnCast = args =>
            {
                if (!state.Ready) return;
                state.Ready = false;
                state.Kills = 0;
                // Sale donde impacte este disparo (el enemigo golpeado, o donde acabe el proyectil).
                args.OnFirstImpact(point => IceBurst.Detonate(point, radius, damage, explosionPrefab, slowsTargets));
            };

            PlayerHit.Killed += state.OnKill;
            WeaponUser.Casting += state.OnCast;
            Tick(context, 0f);
        }

        public override void Tick(ItemEffectContext context, float deltaTime)
        {
            var state = context.GetState<State>();
            string text = state.Ready ? Loc.Get("buff.ready") : $"{state.Kills}/{killsRequired}";
            BuffIndicators.Set(context, context.Item.Icon, text, state.Ready, hudAccent, order: 0);
        }

        public override void OnUnequip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            PlayerHit.Killed -= state.OnKill;
            WeaponUser.Casting -= state.OnCast;
            BuffIndicators.Remove(context);
        }

        public override string Summary() =>
            Loc.Get(slowsTargets ? "effect.kill_counter_explosion_slow" : "effect.kill_counter_explosion",
                    killsRequired, radius, damage);
    }
}
