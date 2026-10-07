using System.Collections.Generic;
using RedMagic.Bosses;
using RedMagic.Combat;
using RedMagic.Enemies;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// Qué enemigos está peleando el jugador ahora mismo, para las barras de vida de enemigo del HUD.
    ///
    /// Sale de <see cref="Health.Active"/> (sin escanear la escena). Cuenta como enemigo lo que lleva
    /// <see cref="EnemyStats"/>, el <see cref="EnemyController"/> antiguo o un <see cref="TrainingDummy"/>,
    /// y no cuelga de un <see cref="BossController"/> (los jefes ya tienen su barra). "Peleando" =
    /// vivo y, o bien su <see cref="EnemyBrain.InCombat"/>, o bien le han dado hace menos de
    /// <see cref="EngageMemory"/> segundos. Ordenados por cercanía al jugador.
    /// </summary>
    public static class HudEnemies
    {
        /// <summary>Segundos que un enemigo golpeado sigue contando como "en combate" sin hacer nada.</summary>
        public const float EngageMemory = 6f;

        public sealed class Info
        {
            public Health Health;
            public EnemyBrain Brain;
            public Collider2D Collider;
            public Renderer Renderer;
            public readonly List<IEnemyCooldownSource> Cooldowns = new();
            public readonly List<IShieldSource> Shields = new();
            public float Distance;

            /// <summary>Punto del mundo justo encima de la cabeza (para la barra flotante).</summary>
            public Vector3 HeadPoint
            {
                get
                {
                    Bounds b = Collider != null && Collider.enabled ? Collider.bounds
                             : Renderer != null ? Renderer.bounds
                             : new Bounds(Health.transform.position, Vector3.one);
                    return new Vector3(b.center.x, b.max.y + 0.35f, 0f);
                }
            }

            public (float current, float max) Shield()
            {
                float current = 0f, max = 0f;
                foreach (var s in Shields)
                {
                    if (!(s is Behaviour b) || b == null || !b.isActiveAndEnabled) continue;
                    current += s.Shield;
                    max += s.ShieldMax;
                }
                return (current, max);
            }
        }

        private static readonly Dictionary<Health, Info> Cache = new();   // null = no es enemigo
        private static readonly List<Info> Engaged = new();
        private static readonly List<Health> Stale = new();
        private static int _frame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            Engaged.Clear();
            _frame = -1;
        }

        /// <summary>Enemigos en combate este frame, del más cercano al más lejano.</summary>
        public static IReadOnlyList<Info> InCombat
        {
            get
            {
                Refresh();
                return Engaged;
            }
        }

        /// <summary>
        /// El enemigo "en foco": el último golpeado si fue hace poco, si no el más cercano en combate.
        /// </summary>
        public static Info Focus
        {
            get
            {
                Refresh();
                Info best = null;
                float last = Time.time - EngageMemory;
                foreach (var info in Engaged)
                    if (info.Health.LastDamagedTime > last)
                    {
                        last = info.Health.LastDamagedTime;
                        best = info;
                    }
                return best ?? (Engaged.Count > 0 ? Engaged[0] : null);
            }
        }

        private static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            Engaged.Clear();

            var player = HudPlayer.Health;
            Vector2 from = player != null ? (Vector2)player.transform.position : Vector2.zero;
            float now = Time.time;

            foreach (var health in Health.Active)
            {
                if (health == null || health.IsDead) continue;
                var info = InfoFor(health);
                if (info == null) continue;

                bool fighting = (info.Brain != null && info.Brain.isActiveAndEnabled && info.Brain.InCombat)
                                || now - health.LastDamagedTime < EngageMemory;
                if (!fighting) continue;

                info.Distance = Vector2.Distance(from, health.transform.position);
                Engaged.Add(info);
            }

            Engaged.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Limpieza de lo destruido (no de lo desactivado: un pool lo vuelve a usar).
            if (Cache.Count > 256)
            {
                Stale.Clear();
                foreach (var key in Cache.Keys) if (key == null) Stale.Add(key);
                foreach (var key in Stale) Cache.Remove(key);
            }
        }

        private static Info InfoFor(Health health)
        {
            if (Cache.TryGetValue(health, out var cached)) return cached;

            Info info = null;
            bool enemy = health.GetComponent<EnemyStats>() != null
                         || health.GetComponent<EnemyController>() != null
                         || health.GetComponent<TrainingDummy>() != null;
            if (enemy && !health.CompareTag("Player") && health.GetComponentInParent<BossController>() == null)
            {
                info = new Info
                {
                    Health = health,
                    Brain = health.GetComponent<EnemyBrain>(),
                    Collider = health.GetComponent<Collider2D>(),
                    Renderer = health.GetComponentInChildren<SpriteRenderer>(),
                };
                health.GetComponentsInChildren(true, info.Cooldowns);
                health.GetComponentsInChildren(true, info.Shields);
            }

            Cache[health] = info;
            return info;
        }
    }
}
