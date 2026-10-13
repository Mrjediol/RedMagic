using System.Collections.Generic;
using RedMagic.Levels;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RedMagic.Run
{
    /// <summary>
    /// Una sala del nivel para el <see cref="WaveManager"/>: un rectángulo en celdas enteras (origen = esquina inferior
    /// izquierda, se ajusta con las asas en la escena). Al entrar el jugador del todo, el WaveManager cierra sus puertas
    /// y lanza las oleadas que esperan a esta sala; al terminarla, las abre.
    ///
    /// <b>Sus puertas se encuentran solas</b>: toda <see cref="LevelDoor"/> que toque el borde del rectángulo (las pintadas
    /// en el Tilemap de puertas no se guardan en la escena, así que no se podrían arrastrar). Una puerta entre dos salas
    /// es de las dos. El gizmo une la sala con cada puerta suya para comprobarlo.
    ///
    /// No usa un collider: con <i>Queries Hit Triggers</i> activado en el proyecto, un trigger del tamaño de una sala lo
    /// verían los rayos de proyectiles y sensores. Basta con mirar cada frame si la caja del jugador está dentro.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelRoom : MonoBehaviour
    {
        [Tooltip("Tamaño en celdas. La posición del objeto es la esquina inferior izquierda.")]
        [SerializeField] private Vector2Int size = new Vector2Int(12, 6);

        /// <summary>El rectángulo de la sala en mundo.</summary>
        public Rect Area => new Rect((Vector2)transform.position, size);

        public Vector2 Center => Area.center;

        private List<LevelDoor> _doors;

        private void OnValidate() => size = Vector2Int.Max(size, Vector2Int.one);

        /// <summary>Cambia posición y tamaño (lo usan las asas del editor).</summary>
        public void SetArea(Vector2 min, Vector2Int newSize)
        {
            transform.position = new Vector3(min.x, min.y, transform.position.z);
            size = Vector2Int.Max(newSize, Vector2Int.one);
            _doors = null;
        }

        /// <summary>True si la caja <paramref name="bounds"/> está entera dentro de la sala.</summary>
        public bool ContainsFully(Bounds bounds)
        {
            var a = Area;
            return bounds.min.x >= a.xMin && bounds.max.x <= a.xMax && bounds.min.y >= a.yMin && bounds.max.y <= a.yMax;
        }

        /// <summary>Las puertas que tocan el borde de la sala.</summary>
        public IReadOnlyList<LevelDoor> Doors
        {
            get
            {
                if (_doors == null || _doors.Exists(d => d == null)) FindDoors();
                return _doors;
            }
        }

        /// <summary>Cierra todas sus puertas (al entrar el jugador).</summary>
        public void Lock()
        {
            foreach (var door in Doors) door.Close();
        }

        /// <summary>Abre todas sus puertas (sala terminada).</summary>
        public void Unlock()
        {
            foreach (var door in Doors) door.Open();
        }

        private void FindDoors()
        {
            _doors = new List<LevelDoor>();
            var area = Area;
            var touch = new Rect(area.x - 0.5f, area.y - 0.5f, area.width + 1f, area.height + 1f);

            var candidates = new HashSet<LevelDoor>(FindObjectsByType<LevelDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            // las generadas por un Tilemap de puertas no se guardan (y en edición FindObjectsByType no las ve)
            foreach (var map in FindObjectsByType<DoorTilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var d in map.Doors) if (d != null) candidates.Add(d);

            foreach (var door in candidates)
            {
                var p = (Vector2)door.transform.position;
                var rect = new Rect(p, new Vector2(door.CellsWide, door.CellsHigh));
                if (rect.Overlaps(touch)) _doors.Add(door);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var a = Area;
            var color = new Color(1f, 0.6f, 0.15f);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.06f);
            Gizmos.DrawCube(a.center, a.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(a.center, a.size);

            // línea a cada puerta suya: verde si empieza abierta, roja si cerrada
            _doors = null;
            foreach (var door in Doors)
            {
                var c = (Vector2)door.transform.position + new Vector2(door.CellsWide, door.CellsHigh) * 0.5f;
                Gizmos.color = door.IsOpen ? Color.green : Color.red;
                Gizmos.DrawLine(a.center, c);
                Gizmos.DrawWireSphere(c, 0.3f);
            }

            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
            Handles.Label(new Vector3(a.xMin + 0.2f, a.yMax - 0.2f), $"{name}  ({size.x}×{size.y})", style);
        }
#endif
    }
}
