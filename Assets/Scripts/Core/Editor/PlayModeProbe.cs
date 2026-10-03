using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace RedMagic.EditorTools
{
    /// <summary>
    /// Sonda de rendimiento del Play Mode <b>en el editor</b> (las builds no la tienen). Sin tocar nada, en cada Play:
    /// <list type="bullet">
    /// <item>reparto del frame (frame / <c>PlayerLoop</c> = el juego / <c>EditorLoop</c> = el editor: inspector, Scene,
    /// ventanas) y basura por frame, de todo el Play;</item>
    /// <item>a los <see cref="CaptureDelay"/> s graba <see cref="CaptureFrames"/> frames con el Profiler y saca lo que más
    /// tiempo propio se lleva (el juego y, aparte, el editor);</item>
    /// <item>al empezar, una foto de lo que hay cargado (objetos, memoria, listas y eventos estáticos del juego) comparada
    /// con la del Play anterior: lo que crece de una sesión a otra es lo que se acumula.</item>
    /// </list>
    /// Escribe <c>Logs/PlayModeProbe/&lt;n&gt;_*.txt</c> (Logs no va a git) y una línea de resumen en la consola.
    /// Apagar/encender: Tools ▸ RedMagic ▸ Sonda de rendimiento (Play).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeProbe
    {
        private const string EnabledPref = "RedMagic.PlayModeProbe.Enabled";
        private const string ReloadsKey = "RedMagic.PlayModeProbe.DomainReloads";
        private const string SessionKey = "RedMagic.PlayModeProbe.Session";
        private const string SnapshotKey = "RedMagic.PlayModeProbe.LastSnapshot";
        private const float CaptureDelay = 8f;
        private const int CaptureFrames = 240;
        private const int Capacity = 20000;
        private const string Menu = "Tools/RedMagic/Sonda de rendimiento (Play)";

        private static ProfilerRecorder _frame, _player, _editor, _gc, _present;
        private static double _enteredAt;
        private static int _captureStart = -1, _captureEnd = -1;
        private static bool _capturing, _captured;
        private static string _dir, _stamp;

        static PlayModeProbe()
        {
            SessionState.SetInt(ReloadsKey, SessionState.GetInt(ReloadsKey, 0) + 1);
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPref, true);
            set => EditorPrefs.SetBool(EnabledPref, value);
        }

        [MenuItem(Menu)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(Menu, true)]
        private static bool ToggleCheck()
        {
            UnityEditor.Menu.SetChecked(Menu, Enabled);
            return true;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (!Enabled) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            else if (change == PlayModeStateChange.ExitingPlayMode) End();
        }

        // ============================================================ sesión

        private static void Begin()
        {
            int session = SessionState.GetInt(SessionKey, 0) + 1;
            SessionState.SetInt(SessionKey, session);
            _dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "PlayModeProbe");
            Directory.CreateDirectory(_dir);
            _stamp = $"{DateTime.Now:yyyyMMdd-HHmmss}_play{session}";

            _frame = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", Capacity);
            _player = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop", Capacity);
            _editor = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "EditorLoop", Capacity);
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Capacity);
            _present = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Gfx.WaitForPresentOnGfxThread", Capacity);

            _enteredAt = EditorApplication.timeSinceStartup;
            _capturing = _captured = false;
            _captureStart = _captureEnd = -1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            File.WriteAllText(Path.Combine(_dir, $"{_stamp}_snapshot.txt"), Snapshot());
        }

        /// <summary>A los <see cref="CaptureDelay"/> s graba <see cref="CaptureFrames"/> frames con el Profiler, una vez.</summary>
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || _captured) return;
            double t = EditorApplication.timeSinceStartup - _enteredAt;
            if (!_capturing && t >= CaptureDelay)
            {
                _capturing = true;
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = false;
                ProfilerDriver.enabled = true;
                _captureStart = -1;
                Debug.Log($"[PlayModeProbe] Grabando {CaptureFrames} frames con el Profiler… sigue jugando normal (moverte, enemigos).");
                return;
            }

            if (_capturing)
            {
                if (_captureStart < 0 && ProfilerDriver.lastFrameIndex >= 0) _captureStart = ProfilerDriver.firstFrameIndex;
                if (_captureStart >= 0 && ProfilerDriver.lastFrameIndex - _captureStart >= CaptureFrames) FinishCapture();
            }
        }

        private static void FinishCapture()
        {
            ProfilerDriver.enabled = false;
            _capturing = false;
            _captured = true;
            _captureEnd = ProfilerDriver.lastFrameIndex;
            try
            {
                File.WriteAllText(Path.Combine(_dir, $"{_stamp}_top.txt"), TopMarkers(_captureStart, _captureEnd));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayModeProbe] No pude leer la captura: {e.Message}");
            }
        }

        private static void End()
        {
            EditorApplication.update -= Tick;
            if (_capturing) FinishCapture();

            var sb = new StringBuilder();
            sb.AppendLine($"Play {_stamp} · recargas de dominio en esta sesión del editor: {SessionState.GetInt(ReloadsKey, 0)}");
            sb.AppendLine($"Unity {Application.unityVersion} · ventanas abiertas: {string.Join(", ", Resources.FindObjectsOfTypeAll<EditorWindow>().Select(w => w.GetType().Name).Distinct())}");
            string frame = Stats("Frame (hilo principal)", _frame, 1e-6);
            string player = Stats("PlayerLoop (el juego)", _player, 1e-6);
            string editor = Stats("EditorLoop (el editor)", _editor, 1e-6);
            string present = Stats("Esperando a la GPU (present)", _present, 1e-6);
            string gc = Stats("Basura por frame (KB)", _gc, 1.0 / 1024);
            sb.AppendLine(frame).AppendLine(player).AppendLine(editor).AppendLine(present).AppendLine(gc);
            File.WriteAllText(Path.Combine(_dir, $"{_stamp}_frames.txt"), sb.ToString());

            Debug.Log($"[PlayModeProbe] {Path.Combine("Logs", "PlayModeProbe", _stamp)}_*.txt\n{frame}\n{player}\n{editor}\n{gc}");
            foreach (var r in new[] { _frame, _player, _editor, _gc, _present }) r.Dispose();
        }

        /// <summary>Media, p50, p95, peor y cuántos frames pasan de 33 ms (menos de 30 fps), de todos los frames del Play.</summary>
        private static string Stats(string label, ProfilerRecorder r, double scale)
        {
            if (!r.Valid || r.Count == 0) return $"{label}: sin datos";
            var values = new List<double>(r.Count);
            for (int i = 0; i < r.Count; i++) values.Add(r.GetSample(i).Value * scale);
            values.Sort();
            double avg = values.Average();
            double P(double q) => values[Math.Min(values.Count - 1, (int)(q * values.Count))];
            string slow = scale < 1e-3 ? $" · >33ms: {values.Count(v => v > 33.3)}/{values.Count}" : "";
            return $"{label}: media {avg:0.00} · p50 {P(0.5):0.00} · p95 {P(0.95):0.00} · peor {values[values.Count - 1]:0.00}{slow}";
        }

        // ============================================================ la captura

        /// <summary>
        /// Lo que más tiempo PROPIO se lleva en los frames grabados (media por frame, ms), en el hilo principal, con su
        /// camino; el juego y el editor por separado. El tiempo propio es el que no está en ningún hijo: señala al culpable.
        /// </summary>
        public static string TopMarkers(int first, int last)
        {
            var self = new Dictionary<string, double>();
            var total = new Dictionary<string, double>();
            int frames = 0;
            var children = new List<int>();
            for (int f = first; f <= last; f++)
            {
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                                                                          HierarchyFrameDataView.columnSelfTime, false))
                {
                    if (view == null || !view.valid) continue;
                    frames++;
                    Walk(view, view.GetRootItemID(), "", 0, self, total, children);
                }
            }

            if (frames == 0) return "sin frames en la captura";
            var sb = new StringBuilder($"Captura de {frames} frames (frames {first}–{last}). Media por frame, ms.\n\n");
            sb.AppendLine("== Más tiempo propio (el culpable directo)");
            foreach (var kv in self.OrderByDescending(kv => kv.Value).Take(40))
                sb.AppendLine($"{kv.Value / frames,8:0.000}  {kv.Key}");
            sb.AppendLine("\n== Más tiempo total (hasta 4 niveles)");
            foreach (var kv in total.OrderByDescending(kv => kv.Value).Take(40))
                sb.AppendLine($"{kv.Value / frames,8:0.000}  {kv.Key}");
            return sb.ToString();
        }

        private static void Walk(HierarchyFrameDataView view, int id, string path, int depth,
                                 Dictionary<string, double> self, Dictionary<string, double> total, List<int> scratch)
        {
            var kids = new List<int>();
            view.GetItemChildren(id, kids);
            foreach (int child in kids)
            {
                string name = view.GetItemName(child);
                string here = path.Length == 0 ? name : $"{path} › {name}";
                float s = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                float t = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                Add(self, here, s);
                if (depth < 4) Add(total, here, t);
                if (depth < 14) Walk(view, child, here, depth + 1, self, total, scratch);
            }
        }

        private static void Add(Dictionary<string, double> d, string key, double v)
        {
            if (v <= 0) return;
            d.TryGetValue(key, out double old);
            d[key] = old + v;
        }

        // ============================================================ la foto

        /// <summary>Lo cargado al entrar en Play y, al lado, cuánto ha cambiado desde el Play anterior de esta sesión del editor.</summary>
        private static string Snapshot()
        {
            var now = new Dictionary<string, long>
            {
                ["mem.monoUsedMB"] = Profiler.GetMonoUsedSizeLong() / (1024 * 1024),
                ["mem.totalAllocatedMB"] = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024),
                ["mem.gfxMB"] = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024),
            };
            foreach (var type in new[] { typeof(GameObject), typeof(MonoBehaviour), typeof(Texture2D), typeof(RenderTexture),
                                         typeof(Material), typeof(Mesh), typeof(Sprite), typeof(AudioClip), typeof(ScriptableObject),
                                         typeof(EditorWindow), typeof(Editor) })
                now[$"objects.{type.Name}"] = Resources.FindObjectsOfTypeAll(type).Length;

            foreach (var kv in StaticCounts()) now[kv.Key] = kv.Value;

            var before = Parse(SessionState.GetString(SnapshotKey, ""));
            SessionState.SetString(SnapshotKey, string.Join("\n", now.Select(kv => $"{kv.Key}={kv.Value}")));

            var sb = new StringBuilder();
            sb.AppendLine(before.Count == 0
                ? "Primera foto de esta sesión del editor (o tras recompilar): sin comparación."
                : "Comparado con el Play anterior de esta sesión del editor. ↑ = crece.");
            sb.AppendLine("\n== Lo que crece");
            foreach (var kv in now.Where(kv => before.TryGetValue(kv.Key, out long b) && kv.Value > b).OrderByDescending(kv => kv.Value - before[kv.Key]))
                sb.AppendLine($"↑ {kv.Key}: {before[kv.Key]} → {kv.Value}");
            sb.AppendLine("\n== Todo");
            foreach (var kv in now.OrderBy(kv => kv.Key)) sb.AppendLine($"{kv.Key} = {kv.Value}");
            return sb.ToString();
        }

        private static Dictionary<string, long> Parse(string text)
        {
            var d = new Dictionary<string, long>();
            foreach (var line in text.Split('\n'))
            {
                int eq = line.LastIndexOf('=');
                if (eq > 0 && long.TryParse(line.Substring(eq + 1), out long v)) d[line.Substring(0, eq)] = v;
            }

            return d;
        }

        /// <summary>
        /// Cada campo estático de las clases del juego que sea una colección (cuántos elementos) o un evento / delegado
        /// (cuántos suscritos). Sin recarga de dominio sobreviven de un Play a otro: si crecen, se acumulan.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, long>> StaticCounts()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Assembly-CSharp", StringComparison.Ordinal));
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    if (type.ContainsGenericParameters) continue;
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsLiteral) continue;
                        object value;
                        try { value = field.GetValue(null); }
                        catch { continue; }

                        long n = value switch
                        {
                            Delegate d => d.GetInvocationList().Length,
                            ICollection c => c.Count,
                            _ => -1,
                        };
                        if (n > 0) yield return new KeyValuePair<string, long>($"static.{type.FullName}.{field.Name}", n);
                    }
                }
            }
        }
    }
}
