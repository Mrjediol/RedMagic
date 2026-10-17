using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Congela el juego un instante (<see cref="Time.timeScale"/> muy bajo, en tiempo real) para que un golpe se sienta:
    /// la parada que acierta, el jugador que se clava en unos pinchos… Un solo punto para todos, así dos golpes seguidos
    /// alargan la misma congelación en vez de pelearse por el timeScale.
    ///
    /// Sólo actúa con el juego a velocidad normal: no pisa la pausa (timeScale 0) ni una cámara lenta de depuración. Si se
    /// pausa a mitad, la pausa manda y es ella quien lo devuelve a 1.
    /// </summary>
    public static class HitStop
    {
        private static Runner _runner;
        private static bool _owns;
        private static float _scale;
        private static float _until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
            _owns = false;
            _until = 0f;
        }

        /// <summary>Congela <paramref name="duration"/> segundos reales a <paramref name="timeScale"/> (0 = parado del todo).</summary>
        public static void Play(float duration, float timeScale = 0.05f)
        {
            if (duration <= 0f) return;
            if (!_owns && !Mathf.Approximately(Time.timeScale, 1f)) return;

            if (_runner == null)
            {
                var go = new GameObject("[HitStop]") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }

            _owns = true;
            _scale = Mathf.Clamp01(timeScale);
            Time.timeScale = _scale;
            _until = Mathf.Max(_until, Time.realtimeSinceStartup + duration);
            _runner.enabled = true;
        }

        private static void End()
        {
            if (!_owns) return;
            _owns = false;
            if (Mathf.Approximately(Time.timeScale, _scale)) Time.timeScale = 1f;
            if (_runner != null) _runner.enabled = false;
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (!_owns) { enabled = false; return; }
                if (Time.realtimeSinceStartup >= _until) End();
            }

            private void OnDestroy() => End();
        }
    }
}
