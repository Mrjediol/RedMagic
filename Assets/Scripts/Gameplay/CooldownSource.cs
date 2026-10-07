using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Casilla del HUD que ocupa una acción con cooldown. El valor fija el orden de izquierda a
    /// derecha; deja huecos para meter acciones nuevas entre medias.
    /// </summary>
    public enum CooldownSlot
    {
        Attack = 0,
        Dash = 10,
        Parry = 20,
    }

    /// <summary>Foto del cooldown de una acción en este frame.</summary>
    public struct CooldownState
    {
        /// <summary>Segundos que faltan para poder volver a usarla.</summary>
        public float Remaining;

        /// <summary>Lo que duraba el cooldown entero que está corriendo (o el último).</summary>
        public float Duration;

        /// <summary>No se puede usar aunque el tiempo haya acabado (dash gastado en el aire).</summary>
        public bool Blocked;

        /// <summary>0..1 de carga mientras se mantiene el botón (armas con carga); 0 si no carga.</summary>
        public float Charge;

        /// <summary>1 = recién usada, 0 = lista.</summary>
        public float Fraction => Duration > 0f ? Mathf.Clamp01(Remaining / Duration) : 0f;

        public bool Ready => Remaining <= 0f && !Blocked;
    }

    /// <summary>
    /// Lo implementa cualquier componente del jugador cuya acción tenga cooldown (arma, espada,
    /// dash, parry…). El HUD (<c>UI.GameHud</c>) y los botones táctiles los buscan en el jugador
    /// con <c>GetComponents</c>: una acción nueva sólo tiene que implementar esto, sin tocar la UI.
    ///
    /// Varias fuentes pueden compartir casilla (la espada y el arma en <see cref="CooldownSlot.Attack"/>):
    /// manda la primera con <see cref="CooldownActive"/> a true.
    /// </summary>
    public interface ICooldownSource
    {
        CooldownSlot Slot { get; }

        /// <summary>False si ahora mismo esta fuente no es la que usa el botón (espada silenciada por el arma).</summary>
        bool CooldownActive { get; }

        CooldownState Cooldown { get; }

        /// <summary>Icono de la casilla; null = el glifo por defecto de la casilla.</summary>
        Sprite CooldownIcon { get; }
    }

    /// <summary>
    /// Lo implementa lo que pone un escudo al jugador. El HUD lo pinta como una barra fina sobre la
    /// de vida, partida en segmentos si <see cref="ShieldMax"/> es un número pequeño de golpes.
    /// </summary>
    public interface IShieldSource
    {
        float Shield { get; }
        float ShieldMax { get; }
    }
}
