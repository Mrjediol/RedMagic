using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Va en el Tilemap donde se pintan los <see cref="DoorTile"/>. Cada grupo de celdas pintadas con el mismo tile
    /// es una puerta: se crea una <see cref="LevelDoor"/> del tamaño de su rectángulo (esquina inferior izquierda en la
    /// celda de abajo a la izquierda). Las puertas creadas NO se guardan en la escena: se regeneran al pintar y al
    /// cargar, así que lo único que manda son las celdas pintadas.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap))]
    [DisallowMultipleComponent]
    public class DoorTilemap : MonoBehaviour
    {
        public const string RootName = "Puertas generadas";

        private Tilemap _map;
        private bool _dirty = true;
        private readonly List<LevelDoor> _doors = new List<LevelDoor>();

        /// <summary>Las puertas que hay ahora mismo (para la lógica de salas: cerrar al entrar, abrir al limpiar).</summary>
        public IReadOnlyList<LevelDoor> Doors => _doors;

        private void OnEnable()
        {
            _map = GetComponent<Tilemap>();
            // las celdas sólo marcan dónde va cada puerta: no se dibujan
            if (TryGetComponent<TilemapRenderer>(out var r)) r.enabled = false;
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
                var go = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                root = go.transform;
            }
            for (int i = root.childCount - 1; i >= 0; i--) DestroySafe(root.GetChild(i).gameObject);
            _doors.Clear();

            _map.CompressBounds();
            var b = _map.cellBounds;
            var seen = new HashSet<Vector3Int>();
            var stack = new Stack<Vector3Int>();
            foreach (var cell in b.allPositionsWithin)
            {
                if (seen.Contains(cell) || !(_map.GetTile(cell) is DoorTile tile)) continue;

                // grupo de celdas vecinas con el mismo tile → su rectángulo es la puerta
                int minX = cell.x, maxX = cell.x, minY = cell.y, maxY = cell.y;
                stack.Push(cell); seen.Add(cell);
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                    foreach (var d in Neighbours)
                    {
                        var n = c + d;
                        if (!seen.Contains(n) && _map.GetTile(n) == tile) { seen.Add(n); stack.Push(n); }
                    }
                }

                if (tile.doorPrefab == null) continue;
                int w = maxX - minX + 1, h = maxY - minY + 1;
                var door = Instantiate(tile.doorPrefab, root);
                door.name = $"Puerta {w}x{h}";
                door.transform.localPosition = _map.CellToLocal(new Vector3Int(minX, minY, 0));
                door.SetCells(w, h);
                SetHideFlags(door.transform, HideFlags.DontSave);
                _doors.Add(door);
                if (w > LevelDoor.MaxWide || h > LevelDoor.MaxHigh || h < LevelDoor.MinHigh)
                    Debug.LogWarning($"[DoorTilemap] Puerta de {w}x{h} celdas fuera de rango " +
                                     $"({LevelDoor.MinWide}-{LevelDoor.MaxWide} x {LevelDoor.MinHigh}-{LevelDoor.MaxHigh}).", this);
            }
        }

        private static readonly Vector3Int[] Neighbours =
            { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };

        private static void SetHideFlags(Transform t, HideFlags flags)
        {
            t.gameObject.hideFlags = flags;
            for (int i = 0; i < t.childCount; i++) SetHideFlags(t.GetChild(i), flags);
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
