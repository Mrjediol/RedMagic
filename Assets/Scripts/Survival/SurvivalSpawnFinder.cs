using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Survival
{
    /// <summary>
    /// Dónde sale cada enemigo, sin puntos colocados a mano: un sitio al azar alrededor del jugador que cumpla las reglas
    /// de su tipo (<see cref="SurvivalConfig"/> ▸ Dónde aparecen).
    /// <list type="bullet">
    /// <item>Todos: entre <see cref="SurvivalConfig.minDistance"/> y <see cref="SurvivalConfig.maxDistance"/> del jugador,
    /// no más de <see cref="SurvivalConfig.maxAbove"/> por encima ni <see cref="SurvivalConfig.maxBelow"/> por debajo, a
    /// <see cref="SurvivalConfig.minSeparation"/> de cualquier enemigo vivo o recién salido, y nunca dentro de la roca.</item>
    /// <item><b>Volador</b>: en el aire, con <see cref="SurvivalConfig.flyerClearance"/> de hueco alrededor.</item>
    /// <item><b>Suelo</b> y <b>Torreta</b>: sobre un suelo o una plataforma (rayo hacia abajo), un poco por encima. (Torretas en
    /// paredes y techos: tarea aparte.)</item>
    /// <item><b>Jefe</b>: en el suelo de abajo, en el centro del nivel.</item>
    /// </list>
    /// "Dentro de la roca" sale de la cuadrícula del <see cref="Level"/> (el terreno pintado tiene colliders de borde, que
    /// por dentro no detectan nada); en un mapa sin <see cref="Level"/>, de los colliders de <see cref="SurvivalConfig.solidLayers"/>.
    /// Si no encuentra sitio, afloja la separación y luego la distancia mínima; si aun así no hay, devuelve false (se
    /// reintenta más tarde).
    /// </summary>
    public sealed class SurvivalSpawnFinder
    {
        /// <summary>Segundos que un sitio recién usado cuenta para la separación (el enemigo aún no se ha movido).</summary>
        private const float RecentSeconds = 2.5f;

        public struct Spot
        {
            public Vector2 position;
            public SurvivalSpawnKind kind;
            public float time;
        }

        private readonly SurvivalConfig _config;
        private readonly Level _level;
        private readonly List<Spot> _recent = new List<Spot>();

        /// <summary>Los sitios usados hace poco (los dibujan los gizmos del manager).</summary>
        public IReadOnlyList<Spot> Recent => _recent;

        public SurvivalSpawnFinder(SurvivalConfig config, Scene scene)
        {
            _config = config;
            foreach (var root in scene.GetRootGameObjects())
            {
                _level = root.GetComponentInChildren<Level>();
                if (_level != null) break;
            }
        }

        public bool TryFind(SurvivalSpawnKind kind, Vector2 player, List<Health> alive, System.Random rng, out Vector2 position)
        {
            _recent.RemoveAll(s => Time.time - s.time > RecentSeconds);
            if (kind == SurvivalSpawnKind.Boss) return BossSpot(player, out position) && Remember(position, kind);

            float separation = _config.minSeparation, minDistance = _config.minDistance;
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 1) separation *= 0.5f;
                if (pass == 2) { separation = 0f; minDistance *= 0.5f; }

                for (int i = 0; i < _config.maxAttempts; i++)
                {
                    var p = Sample(player, rng);
                    bool ok = kind == SurvivalSpawnKind.Flying ? FreeForFlyer(p) : GroundSpot(p, out p);
                    if (!ok || !InRing(p, player, minDistance) || Crowded(p, alive, separation)) continue;
                    position = p;
                    return Remember(p, kind);
                }
            }
            position = default;
            return false;
        }

        // ------------------------------------------------------------------ reglas

        private Vector2 Sample(Vector2 player, System.Random rng)
        {
            float x = player.x + ((float)rng.NextDouble() * 2f - 1f) * _config.maxDistance;
            float y = player.y - _config.maxBelow + (float)rng.NextDouble() * (_config.maxAbove + _config.maxBelow);
            return new Vector2(x, y);
        }

        private bool InRing(Vector2 p, Vector2 player, float minDistance)
        {
            Vector2 d = p - player;
            float dist = d.magnitude;
            return dist >= minDistance && dist <= _config.maxDistance && d.y <= _config.maxAbove && d.y >= -_config.maxBelow;
        }

        private bool Crowded(Vector2 p, List<Health> alive, float separation)
        {
            if (separation <= 0f) return false;
            float s2 = separation * separation;
            foreach (var h in alive)
                if (h != null && ((Vector2)h.transform.position - p).sqrMagnitude < s2) return true;
            foreach (var s in _recent)
                if ((s.position - p).sqrMagnitude < s2) return true;
            return false;
        }

        private bool FreeForFlyer(Vector2 p) =>
            !IsRock(p) && Physics2D.OverlapCircle(p, _config.flyerClearance, _config.solidLayers) == null;

        /// <summary>El suelo o la plataforma que hay debajo de <paramref name="from"/>, y un poco por encima de él.</summary>
        private bool GroundSpot(Vector2 from, out Vector2 spot)
        {
            spot = from;
            if (IsRock(from)) return false;
            float reach = _config.maxAbove + _config.maxBelow;
            var hit = Physics2D.Raycast(from, Vector2.down, reach, _config.solidLayers | _config.platformLayers);
            if (hit.collider == null || hit.normal.y < 0.7f) return false;
            spot = hit.point + Vector2.up * _config.groundSpawnHeight;
            return !IsRock(spot) && Physics2D.OverlapCircle(spot, 0.3f, _config.solidLayers) == null;
        }

        /// <summary>Centro del nivel, sobre el primer suelo desde abajo; sin <see cref="Level"/>, el suelo bajo el jugador.</summary>
        private bool BossSpot(Vector2 player, out Vector2 spot)
        {
            if (_level != null)
            {
                var rect = _level.WorldRect;
                int cx = Mathf.FloorToInt(rect.width * 0.5f);
                spot = new Vector2(rect.x + cx + 0.5f, rect.y + _level.GroundTop(cx) + _config.groundSpawnHeight + 0.5f);
                return true;
            }
            var hit = Physics2D.Raycast(player, Vector2.down, 50f, _config.solidLayers);
            spot = hit.collider != null ? hit.point + Vector2.up * (_config.groundSpawnHeight + 0.5f) : player;
            return true;
        }

        private bool IsRock(Vector2 p)
        {
            if (_level == null) return Physics2D.OverlapPoint(p, _config.solidLayers) != null;
            Vector2 local = p - (Vector2)_level.transform.position;
            return _level.IsRock(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        private bool Remember(Vector2 p, SurvivalSpawnKind kind)
        {
            _recent.Add(new Spot { position = p, kind = kind, time = Time.time });
            return true;
        }
    }
}
