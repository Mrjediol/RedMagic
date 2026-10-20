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
        Weapon = 30,
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
    /// Varias fuentes pueden compartir casilla: manda la primera con <see cref="CooldownActive"/> a true.
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
    /// Una <see cref="ICooldownSource"/> con un segundo hueco intercambiable (las dos armas): el HUD lo enseña como una
    /// insignia pegada a su casilla, con su icono, su recarga y la tecla de cambiar.
    /// </summary>
    public interface IReserveCooldownSource
    {
        /// <summary>Hay algo en el hueco de reserva.</summary>
        bool HasReserve { get; }

        /// <summary>Icono de lo que hay en reserva; null = sin icono.</summary>
        Sprite ReserveIcon { get; }

        /// <summary>Recarga de lo que hay en reserva (sigue corriendo aunque no esté activo).</summary>
        CooldownState ReserveCooldown { get; }

        /// <summary>Time.time del último cambio (el HUD lo anima).</summary>
        float LastSwapTime { get; }
    }

    /// <summary>Qué recarga un cooldown de enemigo (fija el color y el orden en su barra).</summary>
    public enum EnemyCooldownKind
    {
        Attack = 0,
        Shield = 10,
    }

    /// <summary>
    /// Lo implementa cualquier componente de un enemigo con un cooldown que el jugador podría querer
    /// leer (su ataque, un escudo que se recarga…). Las barras de vida de enemigo del HUD lo pintan
    /// como una tira fina bajo la vida, sólo si el jugador los ve (<see cref="EnemyCooldownReveal"/>).
    /// </summary>
    public interface IEnemyCooldownSource
    {
        EnemyCooldownKind Kind { get; }

        /// <summary>
        /// Estado del cooldown. <see cref="CooldownState.Charge"/> &gt; 0 = lo está usando ahora mismo
        /// (el ataque en curso): la barra lo marca como aviso.
        /// </summary>
        CooldownState Cooldown { get; }
    }

    /// <summary>
    /// Si el jugador ve los cooldowns de los enemigos. Lo encienden los items que den esa
    /// información (<see cref="Grant"/> / <see cref="Revoke"/> con su propia clave, como
    /// <c>BuffIndicators</c>) y, para probarlo sin el item, la casilla de pruebas de Opciones
    /// (<c>Settings.HudSettings.EnemyCooldownsDebug</c>, que el HUD suma por su cuenta).
    /// </summary>
    public static class EnemyCooldownReveal
    {
        private static readonly System.Collections.Generic.HashSet<object> Sources = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sources.Clear();

        /// <summary>Algún item lo concede ahora mismo.</summary>
        public static bool Granted => Sources.Count > 0;

        public static void Grant(object source) => Sources.Add(source);

        public static void Revoke(object source) => Sources.Remove(source);
    }

    /// <summary>
    /// Lo implementa lo que pone un escudo al jugador (o a un enemigo). El HUD lo pinta como una barra fina sobre la
    /// de vida, partida en segmentos si <see cref="ShieldMax"/> es un número pequeño de golpes.
    /// </summary>
    public interface IShieldSource
    {
        float Shield { get; }
        float ShieldMax { get; }
    }
}
