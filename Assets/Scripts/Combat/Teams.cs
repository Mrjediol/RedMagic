using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>Los dos bandos que hay en el juego. No hay neutrales.</summary>
    public enum Team
    {
        /// <summary>El jugador (y lo que lance).</summary>
        Player,

        /// <summary>Todo lo demás con <see cref="Health"/>: enemigos, jefes, esbirros, anclas, el muñeco.</summary>
        Enemy,
    }

    /// <summary>
    /// Quién puede dañar a quién, en una sola regla: <b>sólo el jugador daña a los enemigos y sólo
    /// los enemigos al jugador</b>.
    ///
    /// Antes esto se apoyaba únicamente en la "etiqueta amiga" que cada atacante pasaba en su
    /// <c>AbilityContext</c> (o el propietario de un <c>Projectile</c>). Funcionaba para los jefes,
    /// que sí etiquetan a los suyos como <c>Enemy</c>, pero <b>no</b> para los enemigos que salen
    /// del pipeline: nacen <c>Untagged</c>, así que la etiqueta amiga quedaba vacía, el filtro no
    /// filtraba nada y bastaba con que una bala de un enemigo cruzara a otro para que se mataran
    /// entre ellos. Depender de que cada prefab lleve bien la etiqueta es un fallo silencioso
    /// esperando a pasar; el bando se deduce, no se configura.
    ///
    /// El bando se deduce de una sola cosa: llevar la etiqueta <c>Player</c>. Lo demás es enemigo,
    /// lo que deja el comportamiento correcto sin tocar ningún prefab — el muñeco de entrenamiento
    /// y las anclas de un jefe siguen recibiendo daño del jugador, y ninguna bala de jefe se lleva
    /// por delante a sus propios esbirros.
    ///
    /// La etiqueta amiga sigue existiendo y sigue filtrando: esto es una regla <b>de más</b>, no
    /// una sustitución (un jefe puede querer no dañar a algo de su bando aunque fuese posible).
    /// </summary>
    public static class Teams
    {
        /// <summary>La única etiqueta que define un bando.</summary>
        public const string PlayerTag = "Player";

        public static Team Of(GameObject go) =>
            go != null && go.CompareTag(PlayerTag) ? Team.Player : Team.Enemy;

        public static Team Of(Component component) =>
            Of(component != null ? component.gameObject : null);

        /// <summary>
        /// True si son del mismo bando, o sea: el golpe no debe entrar.
        ///
        /// Con un atacante desconocido (<paramref name="attacker"/> nulo — un peligro del escenario
        /// sin dueño) devuelve false: sin bando no hay amigos, y el daño entra como siempre.
        /// </summary>
        public static bool Allied(GameObject attacker, GameObject target)
        {
            if (attacker == null || target == null) return false;
            return Of(attacker) == Of(target);
        }

        public static bool Allied(GameObject attacker, Component target) =>
            Allied(attacker, target != null ? target.gameObject : null);
    }
}
