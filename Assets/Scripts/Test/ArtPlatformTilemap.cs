using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Va en el Tilemap donde se pintan los <see cref="ArtPlatformTile"/>. Cada tramo seguido de un mismo tile se convierte
    /// en UNA plataforma: un objeto en la capa Platform con una línea fina de colisión de un solo sentido en la cara de arriba.
    /// Uno por tramo, no una sola colisión para todo, porque el jugador, al bajarse, ignora la plataforma concreta que pisa
    /// (<c>PlayerMovement.TryDropThroughPlatform</c>). Se regenera al pintar y al cargar.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap))]
    [DisallowMultipleComponent]
    public class ArtPlatformTilemap : MonoBehaviour
    {
        public const string RootName = "Colisiones";

        private Tilemap _map;
        private bool _dirty = true;

        private void OnEnable()
        {
            _map = GetComponent<Tilemap>();
            Tilemap.tilemapTileChanged += OnTilesChanged;
            _dirty = true;
            Rebuild();
        }

        private void OnDisable() => Tilemap.tilemapTileChanged -= OnTilesChanged;

        private void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (map != _map) return;
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (_dirty) Rebuild();
        }

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            _dirty = false;
            if (_map == null) _map = GetComponent<Tilemap>();
            var root = transform.Find(RootName);
            if (root == null)
            {
                var go = new GameObject(RootName);
                go.transform.SetParent(transform, false);
                root = go.transform;
            }
            for (int i = root.childCount - 1; i >= 0; i--) DestroySafe(root.GetChild(i).gameObject);

            int layer = LayerMask.NameToLayer("Platform");
            _map.CompressBounds();
            var b = _map.cellBounds;
            int count = 0;
            for (int y = b.yMin; y < b.yMax; y++)
            {
                int x = b.xMin;
                while (x < b.xMax)
                {
                    var tile = _map.GetTile(new Vector3Int(x, y, 0)) as ArtPlatformTile;
                    if (tile == null) { x++; continue; }
                    int start = x;
                    while (x < b.xMax && _map.GetTile(new Vector3Int(x, y, 0)) == tile) x++;
                    AddPlatform(root, tile, start, y, x - start, layer, ++count);
                }
            }
        }

        private void AddPlatform(Transform root, ArtPlatformTile tile, int x, int y, int run, int layer, int index)
        {
            var go = new GameObject("Plataforma " + index);
            go.transform.SetParent(root, false);
            go.transform.localPosition = _map.CellToLocal(new Vector3Int(x, y + 1, 0)); // esquina superior izquierda de la celda
            if (layer >= 0) go.layer = layer;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(run, tile.colliderThickness);
            box.offset = new Vector2(run * 0.5f, -tile.colliderThickness * 0.5f);
            box.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
