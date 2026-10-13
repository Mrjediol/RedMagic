using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Terreno para cualquier cuerpo que no sea el jugador: qué capas son terreno y cómo hacer que un volador
    /// las atraviese. El juego no tiene rampas (los niveles son celdas con esquinas cuadradas), así que andar es
    /// escribir la X y dejar la Y a la física; no hay seguimiento de pendientes.
    /// </summary>
    public static class GroundMotion
    {
        /// <summary>
        /// Las capas que son <b>terreno</b> en este proyecto: <c>Ground</c> (lo macizo) y
        /// <c>Platform</c> (las plataformas, que el jugador además atraviesa de abajo arriba).
        ///
        /// Se resuelve por nombre y no con un <c>1 &lt;&lt; 6</c> a mano para que renumerar una capa
        /// no lo rompa en silencio. Es la referencia con la que la auditoría de contenido repara
        /// las máscaras de los enemigos: mirar sólo <c>Ground</c> dejaba a un enemigo plantado al
        /// borde de una plataforma creyendo que era un precipicio.
        /// </summary>
        public static LayerMask TerrainMask => LayerMask.GetMask("Ground", "Platform");

        /// <summary>Velocidad de andar en plano: la X pedida y la caída que traiga la física.</summary>
        public static Vector2 Walk(float signedSpeed, Vector2 current) => new Vector2(signedSpeed, current.y);

        /// <summary>Quedarse quieto: X a cero, la caída se respeta (en el aire no se queda flotando).</summary>
        public static Vector2 Halt(Vector2 current) => new Vector2(0f, current.y);

        /// <summary>
        /// Hace que un objeto atraviese el terreno (o deje de hacerlo), sin capas nuevas ni tocar
        /// la matriz de colisiones del proyecto: es un <c>excludeLayers</c> por collider.
        ///
        /// Es lo que hace que para un volador las plataformas no existan. Sólo se excluye el
        /// terreno: sigue chocando con el jugador, así que el daño por contacto no cambia.
        /// </summary>
        public static void PhaseThroughTerrain(GameObject root, LayerMask terrain, bool on)
        {
            if (root == null) return;

            foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            {
                int mask = col.excludeLayers.value;
                col.excludeLayers = on ? mask | terrain.value : mask & ~terrain.value;
            }
        }
    }
}
