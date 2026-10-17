using System.Collections;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Gameplay;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Va en el Tilemap donde se pintan los <see cref="SpikeTile"/> (<c>Grid/Pinchos</c>, renderer apagado). Por cada celda
    /// mira la roca del <see cref="Level"/>: con roca debajo los cristales salen del suelo, encima del techo, y si no de la
    /// pared que tenga al lado. Las celdas seguidas con la misma orientación forman un tramo: un sprite de 2 celdas por cada
    /// pareja, uno de 1 en la que sobre, y una sola zona de daño para todo el tramo. Un bloque de 2×2 celdas (2 a lo largo de
    /// la superficie y 2 hacia fuera) es un solo cristal grande: el de 1 celda al doble, con su zona de daño el doble de alta. Lo generado NO se guarda en la escena
    /// (se rehace al pintar, al cambiar la roca y al cargar): lo que manda son las celdas pintadas.
    ///
    /// Al tocarlos, el jugador (sólo él: los enemigos pasan por encima) recibe daño con sacudida y hit-stop y vuelve al
    /// punto de reaparición de su sala de parkour (<see cref="ParkourRoom"/>), con un fundido corto. Fuera de una sala
    /// vuelve a la última plataforma de reaparición que encendió; sin ninguna, sólo el daño y el empujón.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap))]
    [DisallowMultipleComponent]
    public class SpikeTilemap : MonoBehaviour
    {
        public const string RootName = "Pinchos generados";

        [Header("Golpe")]
        [Min(0f)] [SerializeField] private float damage = 15f;
        [Tooltip("Multiplicador del empujón del daño (1 = el normal del jugador).")]
        [Min(0f)] [SerializeField] private float knockback = 1f;
        [Tooltip("Amplitud (unidades) y duración (s) de la sacudida de cámara.")]
        [Min(0f)] [SerializeField] private float shakeAmplitude = 0.35f;
        [Min(0f)] [SerializeField] private float shakeDuration = 0.25f;
        [Tooltip("Segundos (reales) de congelación al clavarse.")]
        [Min(0f)] [SerializeField] private float hitStopDuration = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float hitStopTimeScale = 0.05f;

        [Header("Vuelta al inicio de la sala")]
        [Tooltip("Segundos (reales) entre el golpe y el fundido: se ve el golpe antes de volver.")]
        [Min(0f)] [SerializeField] private float respawnDelay = 0.25f;
        [Tooltip("Duración de cada mitad del fundido a negro (s). 0 = salto directo.")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.15f;

        [Header("Dibujo")]
        [SerializeField] private int sortingOrder = LevelSorting.OverTerrain + 5;

        private enum Dir { Up, Down, Right, Left }

        private Tilemap _map;
        private Level _level;
        private bool _dirty = true;
        private static float _busyUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _busyUntil = 0f;

        private void OnEnable()
        {
            _map = GetComponent<Tilemap>();
            _level = GetComponentInParent<Level>();
            // las celdas sólo marcan dónde van los pinchos: no se dibujan
            if (TryGetComponent<TilemapRenderer>(out var r)) r.enabled = false;
            Tilemap.tilemapTileChanged += OnTilesChanged;
            _dirty = true;
            Rebuild();
        }

        private void OnDisable() => Tilemap.tilemapTileChanged -= OnTilesChanged;

        // también al pintar roca: la orientación depende de ella
        private void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (map != _map && map.transform.parent != transform.parent) return;
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (_dirty) Rebuild();
        }

        // ------------------------------------------------------------------ generación

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            _dirty = false;
            if (_map == null) _map = GetComponent<Tilemap>();
            if (_level == null) _level = GetComponentInParent<Level>();

            var root = transform.Find(RootName);
            if (root == null)
            {
                var go = new GameObject(RootName) { hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                root = go.transform;
            }
            for (int i = root.childCount - 1; i >= 0; i--) DestroySafe(root.GetChild(i).gameObject);

            _map.CompressBounds();
            var b = _map.cellBounds;
            var done = new HashSet<Vector3Int>();
            int count = 0;
            foreach (var cell in b.allPositionsWithin)
            {
                // las celdas de fuera (apiladas sobre las de la superficie) sólo cuentan como parte de un cristal grande
                if (done.Contains(cell) || !(_map.GetTile(cell) is SpikeTile tile) || IsOuter(cell)) continue;
                var dir = DirOf(cell);
                // el tramo avanza a lo largo de la superficie: en x para suelo/techo, en y para paredes
                var step = dir == Dir.Up || dir == Dir.Down ? Vector3Int.right : Vector3Int.up;
                var start = cell;
                while (Same(start - step, tile, dir) && !done.Contains(start - step)) start -= step;
                int n = 0;
                while (Same(start + step * n, tile, dir)) { done.Add(start + step * n); n++; }
                AddRun(root, tile, start, step, n, dir, ++count, done);
            }

            if (!Application.isPlaying) SetHideFlags(root, HideFlags.DontSave);
        }

        private bool IsSpike(Vector3Int c) => _map.GetTile(c) is SpikeTile;

        private bool Same(Vector3Int c, SpikeTile tile, Dir dir) => _map.GetTile(c) == tile && !IsOuter(c) && DirOf(c) == dir;

        private bool Solid(int x, int y) => _level != null && _level.IsSolid(x, y);

        private bool TouchesRock(Vector3Int c) =>
            Solid(c.x, c.y - 1) || Solid(c.x, c.y + 1) || Solid(c.x - 1, c.y) || Solid(c.x + 1, c.y);

        private Dir DirOf(Vector3Int c)
        {
            if (Solid(c.x, c.y - 1)) return Dir.Up;
            if (Solid(c.x, c.y + 1)) return Dir.Down;
            if (Solid(c.x - 1, c.y)) return Dir.Right;
            if (Solid(c.x + 1, c.y)) return Dir.Left;
            return Dir.Up;
        }

        private static Vector3Int Outward(Dir dir) => dir switch
        {
            Dir.Up => Vector3Int.up, Dir.Down => Vector3Int.down, Dir.Right => Vector3Int.right, _ => Vector3Int.left,
        };

        /// <summary>Celda sin roca al lado apilada encima (hacia fuera) de una celda de superficie: la segunda fila de un 2×2.</summary>
        private bool IsOuter(Vector3Int c)
        {
            if (TouchesRock(c)) return false;
            foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
            {
                var n = c - Outward(d);
                if (IsSpike(n) && TouchesRock(n) && DirOf(n) == d) return true;
            }
            return false;
        }

        private void AddRun(Transform root, SpikeTile tile, Vector3Int start, Vector3Int step, int n, Dir dir, int index, HashSet<Vector3Int> done)
        {
            var run = new GameObject($"Pinchos {index} ({n})");
            run.transform.SetParent(root, false);
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast"); // con Queries Hit Triggers, que no frene rayos ni balas
            if (ignoreRaycast >= 0) run.layer = ignoreRaycast;
            var o = Outward(dir);

            // sprites: un 2×2 pintado (2 a lo largo y 2 hacia fuera) = el cristal de 1 celda al doble; si no, parejas de
            // 2 celdas y, si sobra una, una de 1
            for (int k = 0; k < n;)
            {
                var c = start + step * k;
                bool big = k + 1 < n && Free(c + o, tile, done) && Free(c + step + o, tile, done) && tile.oneCell != null && tile.oneCell.Length > 0;
                if (big)
                {
                    done.Add(c + o);
                    done.Add(c + step + o);
                    AddSprite(run.transform, Pick(tile.oneCell, c), c, 2, 2f, dir, tile.sink);
                    AddZone(run, tile, c, 2, 2f * tile.hurtDepth, dir);
                    k += 2;
                    continue;
                }
                bool pair = k + 1 < n && tile.twoCells != null && tile.twoCells.Length > 0;
                int w = pair ? 2 : 1;
                var set = pair ? tile.twoCells : tile.oneCell;
                if (set != null && set.Length > 0) AddSprite(run.transform, Pick(set, c), c, w, 1f, dir, tile.sink);
                k += w;
            }

            // zona de daño: el tramo entero, de la superficie hacia fuera (los cristales grandes llevan además la suya)
            AddZone(run, tile, start, n, tile.hurtDepth, dir);
            run.AddComponent<SpikeHazard>().Init(this);
        }

        private bool Free(Vector3Int c, SpikeTile tile, HashSet<Vector3Int> done) => _map.GetTile(c) == tile && IsOuter(c) && !done.Contains(c);

        private void AddZone(GameObject run, SpikeTile tile, Vector3Int start, int n, float depth, Dir dir)
        {
            var box = run.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            float len = n - tile.hurtInset * 2f;
            var o = (Vector2)_map.CellToLocal(start);
            Rect r = dir switch
            {
                Dir.Up => new Rect(o.x + tile.hurtInset, o.y, len, depth),
                Dir.Down => new Rect(o.x + tile.hurtInset, o.y + 1f - depth, len, depth),
                Dir.Right => new Rect(o.x, o.y + tile.hurtInset, depth, len),
                _ => new Rect(o.x + 1f - depth, o.y + tile.hurtInset, depth, len),
            };
            box.offset = r.center;
            box.size = r.size;
        }

        private void AddSprite(Transform parent, Sprite sprite, Vector3Int first, int w, float scale, Dir dir, float sink)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            var o = (Vector2)_map.CellToLocal(first);
            // pivote del sprite = centro de su base, pegado a la superficie (hundido 'sink')
            go.transform.localPosition = dir switch
            {
                Dir.Up => new Vector3(o.x + w * 0.5f, o.y - sink),
                Dir.Down => new Vector3(o.x + w * 0.5f, o.y + 1f + sink),
                Dir.Right => new Vector3(o.x - sink, o.y + w * 0.5f),
                _ => new Vector3(o.x + 1f + sink, o.y + w * 0.5f),
            };
            go.transform.localRotation = Quaternion.Euler(0f, 0f, dir switch { Dir.Up => 0f, Dir.Down => 180f, Dir.Right => -90f, _ => 90f });
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = LevelSorting.Layer;
            sr.sortingOrder = sortingOrder;
            // un tramo de pared con la misma celda arriba y abajo no debe verse idéntico: volteo según la celda
            sr.flipX = ((first.x * 7 + first.y * 13) & 1) == 1;
        }

        private static Sprite Pick(Sprite[] set, Vector3Int c)
        {
            int h = (c.x * 73856093) ^ (c.y * 19349663);
            return set[(h & 0x7fffffff) % set.Length];
        }

        // ------------------------------------------------------------------ golpe

        /// <summary>El jugador ha tocado un tramo (lo llama <see cref="SpikeHazard"/>).</summary>
        public void Hurt(Health player, Vector2 point)
        {
            if (!Application.isPlaying || player == null || player.IsDead) return;
            if (Time.realtimeSinceStartup < _busyUntil) return; // ya está volviendo

            player.TakeDamage(damage, point, knockback); // con i-frames no quita vida, pero igual vuelve al inicio
            CameraFollow.ShakeAll(shakeAmplitude, shakeDuration);
            HitStop.Play(hitStopDuration, hitStopTimeScale);
            if (player.IsDead) return; // la muerte la lleva RunManager

            var target = ParkourRoom.RespawnPointFor(player.transform.position);
            if (target == null) return;
            _busyUntil = Time.realtimeSinceStartup + respawnDelay + fadeDuration * 2f + 0.1f;
            StartCoroutine(Respawn(player, target.Value));
        }

        private IEnumerator Respawn(Health player, Vector3 position)
        {
            yield return new WaitForSecondsRealtime(respawnDelay);
            if (fadeDuration > 0f) yield return ScreenFader.FadeOut(fadeDuration, fadeDuration, Color.black);
            if (player != null && !player.IsDead) ParkourRoom.Teleport(player, position);
            if (fadeDuration > 0f) ScreenFader.FadeIn(fadeDuration);
        }

        // ------------------------------------------------------------------ util

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
