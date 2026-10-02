using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// El <b>puente Unity ⇄ web</b> (<c>docs/plan-unity-web-bridge.md</c>): un servidor HTTP mínimo
    /// dentro del editor para que la pestaña 🎬 "Gestos y ataques" de la web lea un enemigo del
    /// proyecto y guarde en él, sin exportar ni importar archivos.
    ///
    /// <list type="bullet">
    /// <item><b>Es una API, no un host</b>: la web la sigue sirviendo el usuario (su biblioteca vive en
    /// IndexedDB, por origen). Unity sólo abre el navegador en esa URL (<see cref="WebUrl"/>) con el
    /// puerto, el token y el prefab en la query.</item>
    /// <item><b>Sólo <c>127.0.0.1</c></b>, puerto <see cref="DefaultPort"/> (el siguiente libre si está
    /// ocupado). <b>Parado hasta que se usa</b>: arranca la primera vez que se pulsa el botón y, tras
    /// una recarga de dominio, sólo si ya estaba en marcha (<see cref="SessionState"/>).</item>
    /// <item><b>Token por sesión, invisible</b>: lo genera Unity y viaja en la URL que abre. Toda
    /// llamada lo lleva; sin él la respuesta es 401. Nadie lo escribe ni lo ve.</item>
    /// <item><b>CORS</b>: sólo el origen de la web configurada (o su gemelo de loopback), con el
    /// preflight contestado (incluido <c>Access-Control-Allow-Private-Network</c>).</item>
    /// <item><b>Las peticiones se atienden en el hilo principal</b> (<see cref="EditorApplication.update"/>
    /// vacía una cola): <c>AssetDatabase</c> y <c>PrefabUtility</c> no se pueden llamar desde el
    /// hilo del listener.</item>
    /// </list>
    /// Las rutas están en <see cref="WebBridgeRoutes"/>; los botones, en <see cref="WebBridgeMenu"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class WebBridgeServer
    {
        public const int DefaultPort = 7810;
        public const string DefaultWebUrl = "http://localhost:3000";

        private const string WebUrlPref = "RedMagic.WebBridge.WebUrl";
        private const string RunningKey = "RedMagic.WebBridge.Running";
        private const string TokenKey = "RedMagic.WebBridge.Token";
        private const string PortKey = "RedMagic.WebBridge.Port";
        private const int PortTries = 20;

        private static HttpListener _listener;
        private static Thread _thread;
        private static readonly ConcurrentQueue<HttpListenerContext> Pending = new ConcurrentQueue<HttpListenerContext>();

        // Leídos desde el hilo del listener: copias de lo que sólo se puede consultar en el principal.
        private static volatile string _token = "";
        private static volatile string _webOrigin = "";

        public static bool Running => _listener != null && _listener.IsListening;
        public static int Port { get; private set; }
        public static string Token => _token;

        /// <summary>La URL donde el usuario sirve la web (<c>npx serve Web/RedMagicWeb</c>). Se edita en Biblioteca Jere.</summary>
        public static string WebUrl
        {
            get => EditorPrefs.GetString(WebUrlPref, DefaultWebUrl);
            set
            {
                string url = (value ?? "").Trim().TrimEnd('/');
                EditorPrefs.SetString(WebUrlPref, url.Length > 0 ? url : DefaultWebUrl);
                _webOrigin = OriginOf(WebUrl);
            }
        }

        static WebBridgeServer()
        {
            EditorApplication.update += Drain;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            // Tras recompilar: sigue en marcha sólo si lo estaba (la pestaña de la web sigue abierta).
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.delayCall += () => Start();
        }

        /// <summary>Arranca el listener (si no lo está). False si no hay puerto libre.</summary>
        public static bool Start()
        {
            if (Running) return true;

            _token = SessionState.GetString(TokenKey, "");
            if (string.IsNullOrEmpty(_token))
            {
                _token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                SessionState.SetString(TokenKey, _token);
            }

            _webOrigin = OriginOf(WebUrl);

            // El mismo puerto de antes primero: la web abierta lo lleva en su URL.
            int first = SessionState.GetInt(PortKey, DefaultPort);
            var candidates = new List<int> { first };
            for (int p = DefaultPort; p < DefaultPort + PortTries; p++)
                if (p != first) candidates.Add(p);

            foreach (int port in candidates)
            {
                var listener = new HttpListener();
                // 127.0.0.1 explícito: no pide permisos de administrador (un prefijo '+' sí).
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                try
                {
                    listener.Start();
                }
                catch (Exception)
                {
                    try { listener.Close(); } catch { /* nunca llegó a abrir */ }
                    continue;
                }

                _listener = listener;
                Port = port;
                SessionState.SetInt(PortKey, port);
                SessionState.SetBool(RunningKey, true);
                _thread = new Thread(Listen) { IsBackground = true, Name = "RedMagic.WebBridge" };
                _thread.Start(listener);
                return true;
            }

            Debug.LogWarning($"[WebBridge] Ningún puerto libre entre {DefaultPort} y {DefaultPort + PortTries - 1}.");
            return false;
        }

        /// <summary>Para el listener. <see cref="RunningKey"/> no se toca: tras una recarga vuelve a arrancar.</summary>
        private static void Stop()
        {
            var listener = _listener;
            _listener = null;
            if (listener == null) return;
            try { listener.Close(); } catch { /* ya cerrado */ }
        }

        /// <summary>Lo apaga del todo (menú): no vuelve tras recompilar hasta que se pulse otra vez el botón.</summary>
        public static void Shutdown()
        {
            Stop();
            SessionState.SetBool(RunningKey, false);
        }

        // ============================================================ hilo del listener

        private static void Listen(object state)
        {
            var listener = (HttpListener)state;
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch (Exception) { return; }   // Close() desde el hilo principal

                try
                {
                    ApplyCors(context);

                    // El preflight no toca nada de Unity: se contesta aquí mismo.
                    if (context.Request.HttpMethod == "OPTIONS")
                    {
                        context.Response.StatusCode = 204;
                        context.Response.Close();
                        continue;
                    }

                    if (!OriginAllowed(context.Request.Headers["Origin"]))
                    {
                        Reply(context, 403, new JObject { ["error"] = "origen no permitido" });
                        continue;
                    }

                    if (context.Request.QueryString["token"] != _token)
                    {
                        Reply(context, 401, new JObject { ["error"] = "token no válido" });
                        continue;
                    }

                    Pending.Enqueue(context);
                }
                catch (Exception)
                {
                    try { context.Response.Abort(); } catch { /* el cliente se fue */ }
                }
            }
        }

        private static void ApplyCors(HttpListenerContext context)
        {
            string origin = context.Request.Headers["Origin"];
            var headers = context.Response.Headers;
            if (!string.IsNullOrEmpty(origin) && OriginAllowed(origin)) headers["Access-Control-Allow-Origin"] = origin;
            headers["Vary"] = "Origin";
            headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            headers["Access-Control-Allow-Headers"] = "Content-Type";
            // Chrome (Private Network Access): una página pide permiso para llamar a una IP de loopback.
            headers["Access-Control-Allow-Private-Network"] = "true";
            headers["Access-Control-Max-Age"] = "600";
            headers["Cache-Control"] = "no-store";
        }

        /// <summary>
        /// El origen de la web configurada, o su gemelo de loopback (<c>localhost</c> ⇄ <c>127.0.0.1</c>,
        /// mismo puerto). Sin cabecera Origin (curl, una navegación directa) decide sólo el token.
        /// </summary>
        private static bool OriginAllowed(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return true;
            string allowed = _webOrigin;
            if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase)) return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var a) || !Uri.TryCreate(allowed, UriKind.Absolute, out var b)) return false;
            return a.Scheme == b.Scheme && a.Port == b.Port && a.IsLoopback && b.IsLoopback;
        }

        private static string OriginOf(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : "";

        // ============================================================ hilo principal

        private static void Drain()
        {
            while (Pending.TryDequeue(out var context))
            {
                try
                {
                    WebBridgeRoutes.Handle(context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Reply(context, 500, new JObject { ["error"] = e.Message });
                }
            }
        }

        // ============================================================ respuestas

        public static void Reply(HttpListenerContext context, int status, JToken body)
        {
            ReplyBytes(context, status, Encoding.UTF8.GetBytes(body.ToString(Formatting.None)), "application/json; charset=utf-8");
        }

        public static void ReplyBytes(HttpListenerContext context, int status, byte[] bytes, string contentType)
        {
            try
            {
                var response = context.Response;
                response.StatusCode = status;
                response.ContentType = contentType;
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();
            }
            catch (Exception)
            {
                // El navegador cerró la conexión (pestaña cerrada, recarga): no hay a quién contestar.
                try { context.Response.Abort(); } catch { /* idem */ }
            }
        }

        public static byte[] ReadBody(HttpListenerContext context)
        {
            using (var memory = new MemoryStream())
            {
                context.Request.InputStream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        /// <summary>La URL de la web con el puente y el enemigo en la query (lo que abre el botón).</summary>
        public static string BuildUrl(string prefabPath, string gesture, int attack)
        {
            var query = new List<string>
            {
                $"bridge={Port}",
                $"token={_token}",
                $"prefab={Uri.EscapeDataString(prefabPath)}",
            };
            if (!string.IsNullOrEmpty(gesture)) query.Add($"gesture={Uri.EscapeDataString(gesture)}");
            if (attack >= 0) query.Add($"attack={attack}");
            return $"{WebUrl}/?{string.Join("&", query)}";
        }
    }
}
