using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Tile de pinchos (cristales): marca las celdas peligrosas. No se dibuja en el juego (el Tilemap de pinchos lleva el
    /// renderer apagado; el icono sólo se ve en la paleta): <see cref="SpikeTilemap"/> mira la roca de alrededor de cada celda
    /// para saber de dónde salen (suelo, techo o pared), agrupa las celdas seguidas y pone un sprite de 2 celdas por cada
    /// pareja y uno de 1 celda en la que sobre. Lo que hace daño y adónde devuelve al jugador vive en <see cref="SpikeTilemap"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Niveles/Tile de pinchos", fileName = "SpikeTile")]
    public class SpikeTile : TileBase
    {
        [Header("Sprites (pivote abajo en el centro, apuntando hacia arriba)")]
        [Tooltip("Ocupan 1 celda de ancho. Se elige uno por celda (variado pero siempre el mismo en la misma celda).")]
        public Sprite[] oneCell;

        [Tooltip("Ocupan 2 celdas de ancho: se usan para cada pareja de celdas seguidas.")]
        public Sprite[] twoCells;

        [Tooltip("Icono para la paleta.")]
        public Sprite icon;

        [Header("Zona de daño")]
        [Tooltip("Hasta dónde llega desde la superficie (celdas). Algo menos que el alto de los cristales: rozar la punta no cuenta.")]
        [Range(0.1f, 1f)] public float hurtDepth = 0.55f;

        [Tooltip("Margen sin daño en cada extremo del tramo (celdas), para no clavarse con el borde de un cristal.")]
        [Range(0f, 0.45f)] public float hurtInset = 0.12f;

        [Tooltip("Cuánto se hunde el sprite en la superficie (celdas): el corte de abajo queda dentro del borde del terreno.")]
        [Range(0f, 0.4f)] public float sink = 0.08f;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = icon;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
        }
    }
}
