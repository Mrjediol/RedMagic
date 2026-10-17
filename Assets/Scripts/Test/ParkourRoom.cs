using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Gameplay;
using RedMagic.Levels;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Sala de parkour opcional. Va junto a un <see cref="LevelRoom"/> (su rectángulo, sus asas y sus puertas) y encuentra
    /// sola, por posición dentro del rectángulo, su <see cref="RespawnPad"/> (el inicio) y su <see cref="LevelChest"/> (la
    /// recompensa). La puerta del final se pinta cerrada (<c>DoorTile_Energia</c>): al abrir el cofre se abren todas las
    /// puertas de la sala. Mientras el jugador está dentro y no la ha terminado, su plataforma se enciende; si se clava en
    /// unos pinchos (<see cref="SpikeTilemap"/>), vuelve a ella.
    /// No la toca el <see cref="WaveManager"/>: es una sala sin oleadas.
    /// </summary>
    [RequireComponent(typeof(LevelRoom))]
    [DisallowMultipleComponent]
    public class ParkourRoom : MonoBehaviour
    {
        private static readonly List<ParkourRoom> Live = new List<ParkourRoom>();

        private LevelRoom _room;
        private RespawnPad _pad;
        private readonly List<LevelChest> _chests = new List<LevelChest>();
        private GameObject _player;
        private Collider2D _playerBody;
        private bool _found;

        public LevelRoom Room => _room != null ? _room : _room = GetComponent<LevelRoom>();
        public RespawnPad Pad { get { Find(); return _pad; } }
        public bool Completed { get; private set; }

        /// <summary>Donde vuelve el jugador: encima de la plataforma, o junto a la esquina de abajo a la izquierda si no hay.</summary>
        public Vector3 SpawnPosition => Pad != null ? Pad.SpawnPosition : (Vector3)(Room.Area.min + new Vector2(1.5f, 1.5f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live.Clear();

        private void OnEnable()
        {
            Live.Add(this);
            _found = false;
        }

        private void OnDisable()
        {
            Live.Remove(this);
            foreach (var c in _chests) if (c != null) c.Opened -= OnChestOpened;
            _chests.Clear();
            _found = false;
        }

        private void Find()
        {
            if (_found) return;
            _found = true;
            var area = Room.Area;
            _pad = null;
            foreach (var pad in FindObjectsByType<RespawnPad>(FindObjectsSortMode.None))
                if (area.Contains(pad.transform.position) && (_pad == null || pad.transform.position.x < _pad.transform.position.x))
                    _pad = pad; // la de más a la izquierda si hubiera varias
            foreach (var chest in FindObjectsByType<LevelChest>(FindObjectsSortMode.None))
                if (area.Contains(chest.transform.position))
                {
                    _chests.Add(chest);
                    if (Application.isPlaying) chest.Opened += OnChestOpened;
                }
            if (Application.isPlaying && _pad == null)
                Debug.LogWarning($"[ParkourRoom] '{name}' no tiene plataforma de reaparición dentro: se vuelve a su esquina.", this);
            if (Application.isPlaying && _chests.Count == 0)
                Debug.LogWarning($"[ParkourRoom] '{name}' no tiene cofre dentro: su puerta no se abrirá nunca.", this);
        }

        private void Update()
        {
            Find();
            if (Completed || Pad == null) return;
            bool inside = PlayerInside();
            if (inside != Pad.Lit) Pad.SetLit(inside);
        }

        private bool PlayerInside()
        {
            if (_player == null || !_player.activeInHierarchy)
            {
                _player = GameObject.FindGameObjectWithTag("Player"); // RunManager desactiva el de prueba de la escena
                _playerBody = _player != null ? _player.GetComponentInChildren<Collider2D>() : null;
            }
            if (_player == null) return false;
            Vector2 p = _playerBody != null ? (Vector2)_playerBody.bounds.center : (Vector2)_player.transform.position;
            return Room.Area.Contains(p);
        }

        private void OnChestOpened(LevelChest chest)
        {
            if (Completed) return;
            Completed = true;
            Room.Unlock();
            if (Pad != null) Pad.SetLit(false);
        }

        // ------------------------------------------------------------------ reaparición

        /// <summary>
        /// Adónde vuelve el jugador que se ha clavado en <paramref name="position"/>: la plataforma de la sala de parkour que
        /// lo contiene; fuera de una sala, la última plataforma encendida; si no hay, null (sólo daño).
        /// </summary>
        public static Vector3? RespawnPointFor(Vector2 position)
        {
            foreach (var room in Live)
                if (room != null && room.Room.Area.Contains(position)) return room.SpawnPosition;
            return RespawnPad.Last != null ? RespawnPad.Last.SpawnPosition : (Vector3?)null;
        }

        /// <summary>Lleva al jugador a <paramref name="position"/> sin inercia, empujón ni dash a medias.</summary>
        public static void Teleport(Health player, Vector3 position)
        {
            var body = player.GetComponentInParent<Rigidbody2D>();
            if (body == null) body = player.GetComponentInChildren<Rigidbody2D>();
            var root = body != null ? body.transform : player.transform;
            root.position = position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            root.GetComponentInChildren<Knockback>()?.Cancel();
            root.GetComponentInChildren<PlayerMovement>()?.CancelDash();

            foreach (var room in Live)
                if (room != null && room.Room.Area.Contains(position) && room.Pad != null) { room.Pad.Pulse(); return; }
            if (RespawnPad.Last != null) RespawnPad.Last.Pulse();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            _found = false;
            _chests.Clear();
            Find();
            var c = Room.Area.center;
            if (_pad != null) { Gizmos.color = new Color(0.3f, 1f, 0.9f); Gizmos.DrawLine(c, _pad.transform.position); }
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            foreach (var chest in _chests) if (chest != null) Gizmos.DrawLine(c, chest.transform.position);
            UnityEditor.Handles.Label(new Vector3(Room.Area.xMin + 0.2f, Room.Area.yMax - 0.7f),
                _pad == null ? "⚠ sin plataforma de reaparición" : _chests.Count == 0 ? "⚠ sin cofre" : "Parkour",
                new GUIStyle(UnityEditor.EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 1f, 0.9f) } });
        }
#endif
    }
}
