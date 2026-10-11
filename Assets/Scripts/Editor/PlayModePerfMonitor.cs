// PlayModePerfMonitor.cs
// -----------------------------------------------------------------------------
// Tools > RedMagic > Diagnóstico > Monitor de rendimiento (Play) — editor-only leak/stall probe.
// While enabled and in Play Mode it logs one line every IntervalSeconds with the numbers that tell
// a leak apart from an editor-side stall: FPS over the window, active / total GameObjects (pooled
// reserve included), enabled cameras, playing AudioSources / ParticleSystems, loaded Texture2D /
// Material objects, managed + total allocated memory, and the Console's entry count (a Console
// filling with stack-traced warnings slows the whole Editor down and stays slow until cleared).
//
// Running coroutines are NOT listed: Unity exposes no API to count them.
//
// Off by default (it would otherwise add its own line to every Play session); the toggle is an
// EditorPrefs bool, so it's per-machine and survives restarts.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace RedMagic.Diagnostics.EditorTools
{
    [InitializeOnLoad]
    public static class PlayModePerfMonitor
    {
        private const string MenuPath = "Tools/RedMagic/Diagnóstico/Monitor de rendimiento (Play)";
        private const string PrefKey = "RedMagic.PlayModePerfMonitor.Enabled";
        private const double IntervalSeconds = 5.0;

        private static double _nextSample;
        private static int _lastFrame;
        private static float _lastRealtime;
        private static MethodInfo _consoleCount;

        static PlayModePerfMonitor()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            _nextSample = EditorApplication.timeSinceStartup + IntervalSeconds;
            _lastFrame = Time.frameCount;
            _lastRealtime = Time.realtimeSinceStartup;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || !Enabled) return;
            if (EditorApplication.timeSinceStartup < _nextSample) return;
            _nextSample = EditorApplication.timeSinceStartup + IntervalSeconds;

            int frames = Time.frameCount - _lastFrame;
            float seconds = Mathf.Max(Time.realtimeSinceStartup - _lastRealtime, 0.0001f);
            _lastFrame = Time.frameCount;
            _lastRealtime = Time.realtimeSinceStartup;

            int activeGos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            int allGos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            int playingAudio = 0;
            foreach (var a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (a.isPlaying) playingAudio++;
            int aliveParticles = 0;
            foreach (var p in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (p.IsAlive(false)) aliveParticles++;

            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                "[PerfMonitor] fps={0:0} | GO activos={1} total={2} | cámaras={3} | audio={4} partículas={5} | " +
                "Texture2D={6} Material={7} | mono={8:0.0}MB asignado={9:0.0}MB | consola={10}",
                frames / seconds, activeGos, allGos, Camera.allCamerasCount, playingAudio, aliveParticles,
                Resources.FindObjectsOfTypeAll<Texture2D>().Length, Resources.FindObjectsOfTypeAll<Material>().Length,
                Profiler.GetMonoUsedSizeLong() / 1048576.0, Profiler.GetTotalAllocatedMemoryLong() / 1048576.0,
                ConsoleEntryCount());
        }

        /// <summary>UnityEditor.LogEntries is internal; -1 if a Unity update ever renames it.</summary>
        private static int ConsoleEntryCount()
        {
            try
            {
                _consoleCount ??= Type.GetType("UnityEditor.LogEntries, UnityEditor")
                    ?.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
                return _consoleCount != null ? (int)_consoleCount.Invoke(null, null) : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
