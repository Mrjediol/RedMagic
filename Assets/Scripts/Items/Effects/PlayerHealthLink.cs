using System;
using RedMagic.Combat;

namespace RedMagic.Items
{
    /// <summary>
    /// Enganche de un efecto a la vida del jugador <b>actual</b>. El jugador cambia entre hub y run (son
    /// instancias distintas), así que un efecto que se suscribe a su <see cref="Health"/> o le cambia la
    /// vida máxima llama a <see cref="Sync"/> en cada Tick: si el jugador ya no es el mismo, se suelta del
    /// viejo y se engancha al nuevo. Va en el estado del contexto (uno por equipado).
    /// </summary>
    public sealed class PlayerHealthLink
    {
        public Health Current { get; private set; }

        /// <summary>Engancha al jugador actual si ha cambiado (<paramref name="detach"/> del viejo, <paramref name="attach"/> al nuevo).</summary>
        public void Sync(ItemEffectContext context, Action<Health> attach, Action<Health> detach)
        {
            var player = context.PlayerHealth;
            if (player == Current) return;

            if (Current != null) detach(Current); // un jugador destruido es null para Unity: no se toca
            Current = player;
            if (Current != null) attach(Current);
        }

        /// <summary>Se suelta del jugador enganchado (al quitar el item).</summary>
        public void Release(Action<Health> detach)
        {
            if (Current != null) detach(Current);
            Current = null;
        }
    }
}
