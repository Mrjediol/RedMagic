using System;
using RedMagic.Combat;
using RedMagic.Gameplay;

namespace RedMagic.Items
{
    /// <summary>
    /// Aplica los efectos de umbral de sinergia que no ocurren "al golpear" (ésos van en
    /// <see cref="PlayerHit.Deal"/>: Hielo 2 ralentiza, Hielo 4 expone). Aquí:
    /// <list type="bullet">
    /// <item><b>Hielo 6</b> — matar a un ralentizado suelta un estallido pequeño de hielo.</item>
    /// <item><b>Rapidez 2</b> — con un ralentizado en pantalla, el enfriamiento corre más rápido.</item>
    /// <item><b>Reset 2</b> — cada baja le quita segundos al cooldown actual.</item>
    /// <item><b>Vampirismo 2</b> — cada baja cura.</item>
    /// <item><b>Oro 2</b> — más moneda de cada enemigo (<see cref="Economy.CurrencyDropModifiers"/>). Oro 4 y 6
    /// los lee <see cref="GoldMark"/> al tirar el dorado y al marcar.</item>
    /// </list>
    /// Consulta el <see cref="SynergyTracker"/> (única fuente de verdad de los umbrales) en cada
    /// baja / frame, así que equipar o quitar items no necesita avisar a nadie. Los números salen de
    /// <see cref="SynergyConfig"/> ▸ <see cref="SynergyTuning"/>. Vive dentro de
    /// <see cref="WeaponLoadout"/>, igual que <see cref="ItemEffectRunner"/>.
    /// </summary>
    public sealed class SynergyEffectRunner : IDisposable
    {
        private readonly SynergyTracker _synergy;
        private readonly object _hasteKey = new object();
        private readonly object _goldKey = new object();

        public SynergyEffectRunner(SynergyTracker synergy)
        {
            _synergy = synergy;
            PlayerHit.Killed += OnKilled;
        }

        public void Dispose()
        {
            PlayerHit.Killed -= OnKilled;
            PlayerStats.Remove(_hasteKey);
            Economy.CurrencyDropModifiers.Remove(_goldKey);
        }

        public void Tick()
        {
            var t = SynergyConfig.CurrentTuning;
            bool haste = _synergy.IsThresholdActive(BuildTag.Haste, 2) && SlowStatus.AnyOnScreen();
            PlayerStats.SetMultiplier(_hasteKey, PlayerStat.CooldownRate, haste ? t.haste2CooldownRate : 1f);

            bool gold = _synergy.IsThresholdActive(BuildTag.Gold, 2);
            Economy.CurrencyDropModifiers.Set(_goldKey, gold ? t.gold2DropBonus : 0f);
        }

        /// <summary>
        /// Vampirismo 2: motas ROJAS del muerto al jugador en CADA baja (aunque esté a tope); la
        /// curación se aplica cuando llegan, no al matar. Motas VERDES sobre el jugador sólo si de
        /// verdad ha recuperado vida.
        /// </summary>
        private static void Lifesteal(KillInfo kill, SynergyTuning t)
        {
            var player = ItemEffectRunner.PlayerHealth;
            if (player == null) return;

            // La vida viaja con las motas rojas: cada una cura su parte al LLEGAR al jugador, y la
            // primera que sube la vida suelta las verdes (LifeMotes.Drain / DrainPacket).
            Fx.LifeMotes.Sprite = t.lifeMoteSprite;
            Fx.LifeMotes.Drain(kill.Position, player, t.lifestealDrainMotes, t.lifesteal2HealPerKill,
                               t.lifestealHealMotes, player.gameObject);
        }

        private void OnKilled(KillInfo kill)
        {
            var t = SynergyConfig.CurrentTuning;

            if (_synergy.IsThresholdActive(BuildTag.Reset, 2))
                WeaponUser.Current?.ReduceCooldown(t.reset2CooldownReduction);

            if (_synergy.IsThresholdActive(BuildTag.Lifesteal, 2)) Lifesteal(kill, t);

            // Al final: el estallido puede matar a otro ralentizado y reentrar aquí (cadena).
            if (kill.WasSlowed && _synergy.IsThresholdActive(BuildTag.Ice, 6))
                IceBurst.Detonate(kill.Position, t.ice6BurstRadius, t.ice6BurstDamage, t.ice6BurstPrefab,
                                  applySlow: false);
        }
    }
}
