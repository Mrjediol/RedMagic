using System;
using System.Collections.Generic;
using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>Un sonido del arsenal por id (los <c>SND.*</c> de la web: <c>fan_release</c>, <c>ray_ignite</c>…).</summary>
    [Serializable]
    public sealed class ArsenalSoundEntry
    {
        public string id;
        public SoundCue cue = new SoundCue();
    }

    /// <summary>
    /// Sonidos de las armas del arsenal por id, sin cablear nada en cada arma: la tabla vive en
    /// <c>ArsenalAssets ▸ Sonidos</c>. Un id sin clip no suena (las piezas se pasan a WAV en la fase de sonido y se califican).
    /// <c>minGap</c> evita que muchos golpes del mismo tipo en el mismo instante suenen como una ametralladora.
    /// </summary>
    public static class ArsenalSounds
    {
        private static readonly Dictionary<string, float> LastPlay = new Dictionary<string, float>();
        private static Dictionary<string, SoundCue> _map;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { LastPlay.Clear(); _map = null; }

        private static SoundCue Cue(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, SoundCue>();
                var a = ArsenalAssets.Load();
                if (a != null && a.sounds != null) foreach (var s in a.sounds) if (s != null && !string.IsNullOrEmpty(s.id)) _map[s.id] = s.cue;
            }
            return _map.TryGetValue(id, out var c) && c != null && c.HasClips ? c : null;
        }

        public static void Play(string id, Vector2 at, float minGap = 0.04f)
        {
            var cue = Cue(id); var am = AudioManager.Instance;
            if (cue == null || am == null) return;
            float now = Time.unscaledTime;
            if (LastPlay.TryGetValue(id, out float last) && now - last < minGap) return;
            LastPlay[id] = now;
            am.Play(cue, at);
        }

        /// <summary>Bucle (zumbido del rayo, giro de la media luna). default = no hay clip.</summary>
        public static SoundLoop StartLoop(string id, Component owner, Transform follow = null)
        {
            var cue = Cue(id); var am = AudioManager.Instance;
            return cue == null || am == null || owner == null ? default : am.StartLoop(cue, owner, follow);
        }

        public static void StopLoop(SoundLoop loop) => AudioManager.Instance?.StopLoop(loop);
    }
}
