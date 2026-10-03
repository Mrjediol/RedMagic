using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Bosses;
using RedMagic.Bosses.EditorTools;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Las rutas del puente Unity ⇄ web (<see cref="WebBridgeServer"/>). Siempre en el hilo principal.
    ///
    /// <list type="bullet">
    /// <item><c>GET /ping</c> → <c>{ project, unity, bridge }</c>.</item>
    /// <item><c>GET /enemy?prefab=</c> → el enemigo tal como está <b>guardado</b>: <c>rigConfig</c>
    /// (<see cref="RigConfigExporter.Export"/>), <c>enemyConfig</c> con las cartas enteras de su baraja
    /// (<see cref="BossAttackExporter.FillDeck"/>), <c>sprites</c> (recorte, pivote y ppu de cada sprite,
    /// para que la web dibuje el cuerpo), <c>sounds</c> (el sonido de cada gesto), <c>artLibrary</c>
    /// (prefab de efecto → id de la biblioteca web) y <c>revision</c>.</item>
    /// <item><c>GET /file?path=Assets/…</c> → los bytes de un png / wav / ogg / mp3 del proyecto.</item>
    /// <item><c>GET /sounds</c> → <c>{ clips: [rutas] }</c>: los clips de audio del proyecto (para elegir el de un gesto).</item>
    /// <item><c>POST /gestures?prefab=&amp;revision=[&amp;force=1]</c>, cuerpo = el .zip parcial de la web
    /// (<c>rig-export.js ▸ buildGesturesBundle</c>) → <see cref="RigBundleImporter.ImportWithReport"/>.
    /// Si el prefab cambió desde que la web lo leyó (otra <c>revision</c>), o está abierto en Prefab
    /// Mode con cambios sin guardar, o Unity está en Play → <b>409</b> y no se escribe nada.</item>
    /// </list>
    /// </summary>
    public static class WebBridgeRoutes
    {
        private static readonly Dictionary<string, string> FileTypes = new Dictionary<string, string>
        {
            [".png"] = "image/png", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg", [".mp3"] = "audio/mpeg",
        };

        public static void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            string route = request.Url.AbsolutePath.TrimEnd('/');
            string method = request.HttpMethod;

            if (method == "GET" && route == "/ping")
            {
                WebBridgeServer.Reply(context, 200, new JObject
                {
                    ["project"] = Application.productName,
                    ["unity"] = Application.unityVersion,
                    ["bridge"] = 1,
                    ["playing"] = EditorApplication.isPlaying,
                });
                return;
            }

            if (method == "GET" && route == "/enemy") { Enemy(context, request.QueryString["prefab"]); return; }
            if (method == "GET" && route == "/file") { SendFile(context, request.QueryString["path"]); return; }
            if (method == "GET" && route == "/sounds") { WebBridgeServer.Reply(context, 200, new JObject { ["clips"] = AudioClips() }); return; }
            if (method == "POST" && route == "/gestures") { SaveGestures(context); return; }

            Fail(context, 404, $"Ruta desconocida: {method} {route}");
        }

        private static void Fail(HttpListenerContext context, int status, string message, JObject extra = null)
        {
            var body = extra ?? new JObject();
            body["error"] = message;
            WebBridgeServer.Reply(context, status, body);
        }

        // ============================================================ GET /enemy

        /// <summary>El prefab por partes de esa ruta, o null (con el porqué).</summary>
        public static GameObject LoadRigPrefab(string prefabPath, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                problem = $"'{prefabPath}' no es la ruta de un prefab del proyecto.";
                return null;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null) problem = $"'{prefabPath}' no existe.";
            else if (root.GetComponent<ProceduralRig>() == null) problem = $"'{prefabPath}' no es un cuerpo por partes (no tiene ProceduralRig).";
            return problem == null ? root : null;
        }

        private static void Enemy(HttpListenerContext context, string prefabPath)
        {
            var root = LoadRigPrefab(prefabPath, out string problem);
            if (root == null) { Fail(context, 404, problem); return; }

            var warnings = new List<string>();
            if (!Snapshot(root, prefabPath, warnings, out var rigConfig, out var enemyConfig))
            {
                Fail(context, 404, $"'{prefabPath}' no tiene 'Visual': no se puede leer como cuerpo por partes.");
                return;
            }

            var rig = root.GetComponent<ProceduralRig>();
            WebBridgeServer.Reply(context, 200, new JObject
            {
                ["name"] = rigConfig.Value<string>("name"),
                ["kind"] = rigConfig.Value<string>("kind"),
                ["prefab"] = prefabPath,
                ["revision"] = RevisionOf(rigConfig, enemyConfig, GestureSounds(rig)),
                ["rigConfig"] = rigConfig,
                ["enemyConfig"] = enemyConfig,
                ["sprites"] = SpriteInfo(rigConfig),
                ["sounds"] = GestureSounds(rig),
                ["artLibrary"] = ArtLibrary(rig),
                ["warnings"] = new JArray(warnings.ToArray()),
            });
        }

        /// <summary>Lo que la web lee de un prefab: su rig-config y (enemigo) su EnemyConfig con la baraja entera.</summary>
        private static bool Snapshot(GameObject root, string prefabPath, List<string> warnings,
                                     out JObject rigConfig, out JObject enemyConfig)
        {
            enemyConfig = null;
            rigConfig = RigConfigExporter.Export(root, prefabPath, warnings);
            if (rigConfig == null) return false;

            // El nombre es el del archivo: es el que usa el import parcial para encontrar el prefab.
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            rigConfig["name"] = name;

            enemyConfig = RigConfigExporter.EnemyConfig(root, name);
            if (enemyConfig != null) BossAttackExporter.FillDeck(root, enemyConfig);
            return true;
        }

        /// <summary>
        /// Huella de lo que la web edita (gestos, sprites por estado, baraja, defensas): si cambia entre
        /// que la web abre el enemigo y lo guarda, alguien lo tocó en Unity.
        /// </summary>
        private static string RevisionOf(JObject rigConfig, JObject enemyConfig, JArray sounds)
        {
            var tuning = enemyConfig?["tuning"] as JObject;
            var watched = new JObject
            {
                ["gestures"] = rigConfig["gestures"],
                ["idleGestures"] = rigConfig["idleGestures"],
                ["transitionGestures"] = rigConfig["transitionGestures"],
                ["sounds"] = sounds,
                ["spriteSwaps"] = rigConfig["spriteSwaps"],
                ["attacks"] = enemyConfig?["attacks"],
                ["deck"] = tuning?["attacks"],
                ["defences"] = tuning?["defences"],
                ["builtInAttack"] = tuning?["builtInAttack"],
                ["builtInWeight"] = tuning?["builtInWeight"],
            };

            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(watched.ToString(Formatting.None)));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>La revisión actual de un prefab guardado, o null si no se puede leer.</summary>
        public static string CurrentRevision(string prefabPath)
        {
            var root = LoadRigPrefab(prefabPath, out _);
            if (root == null) return null;
            return Snapshot(root, prefabPath, new List<string>(), out var rigConfig, out var enemyConfig)
                ? RevisionOf(rigConfig, enemyConfig, GestureSounds(root.GetComponent<ProceduralRig>()))
                : null;
        }

        /// <summary>
        /// De cada sprite que nombra el rig-config: su archivo, su recorte en la textura (píxeles, origen
        /// abajo-izquierda), su pivote (píxeles desde la esquina del recorte) y su ppu. <c>texW / texH</c>
        /// son los de la textura importada: si el PNG es mayor (tamaño máximo de import), la web escala.
        /// </summary>
        private static JObject SpriteInfo(JObject rigConfig)
        {
            var info = new JObject();
            void Add(JToken token)
            {
                if (token == null || token.Type != JTokenType.String) return;
                string reference = token.Value<string>();
                if (string.IsNullOrEmpty(reference) || info[reference] != null) return;
                var sprite = RigConfigJson.LoadProjectSprite(reference);
                if (sprite == null || sprite.texture == null) return;

                var rect = sprite.rect;
                info[reference] = new JObject
                {
                    ["file"] = AssetDatabase.GetAssetPath(sprite),
                    ["rect"] = new JObject { ["x"] = rect.x, ["y"] = rect.y, ["w"] = rect.width, ["h"] = rect.height },
                    ["pivot"] = new JObject { ["x"] = RigConfigJson.Num(sprite.pivot.x), ["y"] = RigConfigJson.Num(sprite.pivot.y) },
                    ["ppu"] = RigConfigJson.Num(sprite.pixelsPerUnit),
                    ["texW"] = sprite.texture.width,
                    ["texH"] = sprite.texture.height,
                };
            }

            if (rigConfig["parts"] is JArray parts) foreach (var p in parts) Add(p["sprite"]);
            if (rigConfig["spriteSwaps"] is JArray swaps)
                foreach (var s in swaps)
                {
                    Add(s["asleep"]);
                    Add(s["awake"]);
                    Add(s["attacking"]);
                    if (s["stages"] is JArray stages) foreach (var st in stages) Add(st);
                    if (s["named"] is JArray named) foreach (var n in named) Add(n["sprite"]);
                }

            return info;
        }

        /// <summary>Los sonidos de los gestos, uno por fila con el nombre de su gesto (un gesto puede tener varios).</summary>
        private static JArray GestureSounds(ProceduralRig rig)
        {
            var list = new JArray();
            foreach (var g in rig.gestures)
            {
                if (g == null) continue;
                foreach (var sound in RigConfigExporter.GestureSounds(g))
                {
                    sound["gesture"] = g.name;
                    list.Add(sound);
                }
            }

            return list;
        }

        /// <summary>Todos los clips de audio del proyecto (menos los packs de terceros): lo que la web deja elegir.</summary>
        private static JArray AudioClips()
        {
            var list = new JArray();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/Brackeys/", StringComparison.Ordinal) || path.StartsWith("Assets/Cainos/", StringComparison.Ordinal) ||
                    path.StartsWith("Assets/Dragon Warrior Files/", StringComparison.Ordinal) || path.StartsWith("Assets/_Reference/", StringComparison.Ordinal))
                    continue;
                if (!FileTypes.ContainsKey(Path.GetExtension(path).ToLowerInvariant())) continue;
                list.Add(path);
            }

            return list;
        }

        /// <summary>
        /// Prefab de efecto de gesto → id de la biblioteca web del que salió (cuando se sabe): la web
        /// enseña ese efecto como vista previa, y lo exporta tal cual llegó (por su ruta).
        /// </summary>
        private static JObject ArtLibrary(ProceduralRig rig)
        {
            var map = new JObject();
            foreach (var g in rig.gestures)
            {
                if (g?.fx == null) continue;
                foreach (var fx in g.fx)
                {
                    if (fx?.prefab == null) continue;
                    string path = AssetDatabase.GetAssetPath(fx.prefab);
                    if (string.IsNullOrEmpty(path) || map[path] != null) continue;
                    string libraryId = BossAttackExporter.ArtLibraryId(fx.prefab);
                    if (!string.IsNullOrEmpty(libraryId)) map[path] = libraryId;
                }
            }

            return map;
        }

        // ============================================================ GET /file

        private static void SendFile(HttpListenerContext context, string path)
        {
            path = (path ?? "").Replace('\\', '/');
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") ||
                !FileTypes.TryGetValue(extension, out string contentType))
            {
                Fail(context, 403, "Sólo se sirven png / wav / ogg / mp3 de Assets/.");
                return;
            }

            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) { Fail(context, 404, $"'{path}' no existe."); return; }
            WebBridgeServer.ReplyBytes(context, 200, File.ReadAllBytes(full), contentType);
        }

        // ============================================================ POST /gestures

        private static void SaveGestures(HttpListenerContext context)
        {
            var query = context.Request.QueryString;
            string prefabPath = query["prefab"];
            bool force = query["force"] == "1";
            byte[] zip = WebBridgeServer.ReadBody(context);

            if (LoadRigPrefab(prefabPath, out string problem) == null) { Fail(context, 404, problem); return; }

            // Reescribir un prefab a media partida no merece la pena: se guarda al salir de Play.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail(context, 409, "Unity está en Play Mode: sal de Play Mode y vuelve a guardar.", new JObject { ["reason"] = "playmode" });
                return;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool open = stage != null && stage.assetPath == prefabPath;
            bool dirty = open && stage.scene.isDirty;
            string current = CurrentRevision(prefabPath);

            if (!force && (dirty || current != query["revision"]))
            {
                Fail(context, 409,
                     dirty ? "El prefab está abierto en Unity con cambios sin guardar."
                           : "El prefab cambió en Unity desde que se abrió en la web.",
                     new JObject { ["reason"] = dirty ? "dirty" : "changed", ["revision"] = current });
                return;
            }

            if (zip.Length == 0) { Fail(context, 400, "La petición no trae el .zip."); return; }

            // "Sobrescribir": lo no guardado del Prefab Mode se descarta, o al guardarlo después pisaría esto.
            if (dirty) stage.ClearDirtiness();

            string folder = Path.Combine(Path.GetFullPath("Temp"), "WebBridge");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"{Guid.NewGuid():N}.zip");
            ConfigImportReport report;
            try
            {
                File.WriteAllBytes(file, zip);
                report = RigBundleImporter.ImportWithReport(file, new RigBundleImporter.Options
                {
                    NameOverride = Path.GetFileNameWithoutExtension(prefabPath),
                    PrefabFolder = Path.GetDirectoryName(prefabPath).Replace('\\', '/'),
                });
            }
            finally
            {
                try { File.Delete(file); } catch { /* Temp/ se limpia sola */ }
            }

            string summary = report.BuildSummary();
            Debug.Log($"[WebBridge] Guardado desde la web → {prefabPath}\n{summary}");

            if (!report.HasErrors) AfterSave(prefabPath, open);

            WebBridgeServer.Reply(context, 200, new JObject
            {
                ["ok"] = !report.HasErrors,
                ["summary"] = summary,
                ["errors"] = new JArray(report.Errors.ToArray()),
                ["warnings"] = new JArray(report.Warnings.ToArray()),
                ["revision"] = CurrentRevision(prefabPath),
            });
        }

        /// <summary>Tras guardar: el prefab se señala en el Project y, si está abierto en Prefab Mode, se recarga.</summary>
        private static void AfterSave(string prefabPath, bool openInStage)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);

            if (!openInStage) return;
            // Con el escenario limpio Unity lo recarga solo al reimportar el prefab; reabrirlo lo asegura
            // (y deja la selección en la raíz nueva, no en un objeto que ya no existe).
            EditorApplication.delayCall += () =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null || stage.assetPath != prefabPath || stage.scene.isDirty) return;
                PrefabStageUtility.OpenPrefab(prefabPath);
            };
        }
    }
}
