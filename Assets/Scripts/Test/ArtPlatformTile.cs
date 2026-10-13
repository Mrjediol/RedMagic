using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Tile de plataforma con arte: el sprite depende de los vecinos de la izquierda y la derecha del mismo tile
    /// (extremo izquierdo, centro que se repite, extremo derecho o suelto). Se pinta como cualquier tile y la
    /// plataforma se forma sola con el largo que se pinte. Cada sprite ocupa la celda entera con el arte pegado a la cara de
    /// arriba; lo que cuelga por debajo no tiene colisión (<see cref="ArtPlatformTilemap"/> crea una línea fina arriba).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Niveles/Tile de plataforma con arte", fileName = "ArtTile")]
    public class ArtPlatformTile : TileBase
    {
        [Header("Sprites (celda entera, arte arriba)")]
        public Sprite left;
        public Sprite center;
        public Sprite right;
        [Tooltip("Plataforma de una sola celda (sin vecinos).")]
        public Sprite single;

        [Header("Colisión")]
        [Tooltip("Grosor (unidades) de la línea de colisión bajo la cara de arriba. Una plataforma de un solo sentido: lo de " +
                 "abajo no tiene hitbox.")]
        [Min(0.05f)] public float colliderThickness = 0.12f;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            bool l = tilemap.GetTile(position + Vector3Int.left) == this;
            bool r = tilemap.GetTile(position + Vector3Int.right) == this;
            tileData.sprite = l && r ? center : l ? right : r ? left : single;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
        }

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            base.RefreshTile(position, tilemap);
            // al pintar o borrar cambian los extremos de los vecinos
            tilemap.RefreshTile(position + Vector3Int.left);
            tilemap.RefreshTile(position + Vector3Int.right);
        }
    }
}
