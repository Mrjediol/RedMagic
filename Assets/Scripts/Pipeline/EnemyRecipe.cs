using RedMagic.Economy;
using RedMagic.Enemies;
using UnityEngine;

namespace RedMagic.Pipeline
{
    /// <summary>
    /// La ficha de un enemigo: <b>sólo lo que lo diferencia de los demás</b>.
    ///
    /// Los valores de juego no se listan aquí: son un <see cref="EnemyTuning"/>, exactamente el
    /// mismo bloque que llevará el <c>EnemyStats</c> del prefab. Así la lista de cosas afinables
    /// está escrita una vez, y generar el prefab es copiarla, no traducirla campo a campo.
    ///
    /// Todo lo que comparten todos los enemigos — cuerpo, collider, <c>Health</c>,
    /// <c>Knockback</c>, <c>HitFlash</c>, <c>Corpse</c>, <c>CurrencyDropper</c>, cerebro,
    /// animación y ataque — lo pone <c>EnemyFactory</c> sin preguntar.
    /// </summary>
    [CreateAssetMenu(fileName = "NuevoEnemigo.enemy", menuName = "RedMagic/Pipeline/Enemy Recipe")]
    public class EnemyRecipe : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Da nombre al prefab: Enemy_<nombre>.prefab")]
        public string enemyName = "NuevoEnemigo";

        [Tooltip("La hoja ya cortada. De ahí salen el sprite base y los clips.")]
        public SpriteSheetRecipe art;

        [Tooltip("Vacío = Assets/Prefabs/Enemies.")]
        public string prefabFolder = "";

        [Header("Presencia")]
        [Tooltip("Escala del hijo Sprite. Ajusta el tamaño en pantalla sin tocar el arte.")]
        [Min(0.01f)] public float spriteScale = 1f;

        [Tooltip("Vacío (0,0) = se deduce de los bounds del sprite de reposo.")]
        public Vector2 colliderSize = Vector2.zero;

        public Vector2 colliderOffset = Vector2.zero;

        [Tooltip("Orden de dibujado del sprite.")]
        public int sortingOrder = 5;

        [Tooltip("Etiqueta del prefab. Los enemigos normales van sin etiqueta; los esbirros de un " +
                 "jefe llevan 'Enemy' para que las balas del jefe no los maten.")]
        public string tag = "Untagged";

        [Header("Valores de juego")]
        [Tooltip("El mismo bloque que acaba en el EnemyStats del prefab: tipo, vida, rangos, " +
                 "velocidades, ataque y velocidad de cada animación.")]
        public EnemyTuning tuning = new EnemyTuning();

        [Header("Proyectil (sólo arquetipos a distancia)")]
        [Tooltip("Fila de la lámina cuyo objeto suelto se convierte en el proyectil. El corte lo " +
                 "exporta solo como '<Personaje>_<Fila>_Prop.png' cuando detecta algo separado del " +
                 "personaje (la piedra a medio camino de la mano, por ejemplo).")]
        public string projectilePropState = "Attack";

        [Tooltip("Escala del sprite del proyectil respecto a su tamaño real en la lámina.")]
        [Min(0.01f)] public float projectileScale = 1f;

        [Header("Economía")]
        public EnemyTier tier = EnemyTier.Basic;

        /// <summary>Carpeta efectiva del prefab.</summary>
        public string ResolvedFolder => string.IsNullOrWhiteSpace(prefabFolder)
            ? "Assets/Prefabs/Enemies"
            : prefabFolder.TrimEnd('/');

        /// <summary>Ruta del prefab que genera esta ficha.</summary>
        public string PrefabPath => $"{ResolvedFolder}/Enemy_{enemyName}.prefab";
    }
}
