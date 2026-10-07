using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// El jugador que pinta el HUD: su <see cref="Health"/>, sus <see cref="ICooldownSource"/> y sus
    /// <see cref="IShieldSource"/>. El jugador del hub y el de la run son instancias distintas, así
    /// que se vuelve a buscar (por tag, como mucho cuatro veces por segundo) en cuanto el que se
    /// tenía desaparece. Lo comparten <see cref="GameHud"/> y los botones táctiles.
    /// </summary>
    public static class HudPlayer
    {
        private const float SearchInterval = 0.25f;

        private static GameObject _player;
        private static Health _health;
        private static float _nextSearch;
        private static int _lastRefreshFrame = -1;
        private static readonly List<ICooldownSource> Cooldowns = new();
        private static readonly List<IShieldSource> Shields = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _player = null;
            _health = null;
            _nextSearch = 0f;
            _lastRefreshFrame = -1;
            Cooldowns.Clear();
            Shields.Clear();
        }

        /// <summary>Vida del jugador actual, o null si no hay jugador en escena.</summary>
        public static Health Health
        {
            get
            {
                Refresh();
                return _health;
            }
        }

        /// <summary>
        /// La fuente que manda ahora en <paramref name="slot"/> (la primera activa), o null si la
        /// casilla no la usa nadie — entonces no se enseña.
        /// </summary>
        public static ICooldownSource Cooldown(CooldownSlot slot)
        {
            Refresh();
            foreach (var source in Cooldowns)
                if (source is Object unityObject && unityObject != null && source.Slot == slot && source.CooldownActive)
                    return source;
            return null;
        }

        /// <summary>Suma de todos los escudos del jugador (actual, máximo).</summary>
        public static (float current, float max) Shield()
        {
            Refresh();
            float current = 0f, max = 0f;
            foreach (var source in Shields)
            {
                if (!(source is Object unityObject) || unityObject == null) continue;
                current += source.Shield;
                max += source.ShieldMax;
            }
            return (current, max);
        }

        private static void Refresh()
        {
            if (_lastRefreshFrame == Time.frameCount) return;
            _lastRefreshFrame = Time.frameCount;

            if (_player != null && _player.activeInHierarchy) return;
            if (Time.unscaledTime < _nextSearch) return;
            _nextSearch = Time.unscaledTime + SearchInterval;

            _player = GameObject.FindWithTag("Player");
            _health = _player != null ? _player.GetComponent<Health>() : null;

            Cooldowns.Clear();
            Shields.Clear();
            if (_player == null) return;

            _player.GetComponents(Cooldowns);
            _player.GetComponents(Shields);
        }
    }
}
