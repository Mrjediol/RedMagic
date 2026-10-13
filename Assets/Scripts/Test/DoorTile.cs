using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Tile de puerta: marca las celdas que ocupa una puerta. No dibuja nada en el juego (el Tilemap de puertas
    /// lleva el renderer apagado; el icono sólo se ve en la paleta): <see cref="DoorTilemap"/> convierte cada
    /// rectángulo pintado con este tile en una <see cref="LevelDoor"/> de ese tamaño exacto.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Niveles/Tile de puerta", fileName = "DoorTile")]
    public class DoorTile : TileBase
    {
        [Tooltip("Puerta que se crea en cada rectángulo pintado.")]
        public LevelDoor doorPrefab;

        [Tooltip("Icono para la paleta.")]
        public Sprite icon;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = icon;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
        }
    }
}
