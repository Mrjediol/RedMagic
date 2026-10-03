using System;
using System.Collections.Generic;
using System.Net;
using RedMagic.Bosses;
using RedMagic.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Las puertas del puente Unity ⇄ web: todas acaban en <see cref="Open"/>, que arranca el
    /// <see cref="WebBridgeServer"/> y abre el navegador en la pestaña 🎬 "Gestos y ataques" con ese
    /// enemigo (y, si se pide, ese gesto o esa carta de la baraja).
    ///
    /// <list type="bullet">
    /// <item>El botón del Inspector (<see cref="DrawInspector"/>): arriba del <c>EnemyStats</c> de un
    /// enemigo por partes y del <c>BossController</c> de un jefe por partes, con un "✎" por gesto y por carta.</item>
    /// <item><c>ProceduralRig ▸ ⋮ ▸ Editar gestos y ataques en la web</c>.</item>
    /// <item>Biblioteca Jere ▸ Enemies ▸ "Editar en la web" (y allí se cambia la URL de la web).</item>
    /// </list>
    /// </summary>
    public static class WebBridgeMenu
    {
        private const string ServeCommand = "npx serve Web/RedMagicWeb";

        [MenuItem("CONTEXT/ProceduralRig/Editar gestos y ataques en la web")]
        private static void OpenFromContext(MenuCommand command)
        {
            var rig = command.context as ProceduralRig;
            if (rig != null) Open(PrefabPathOf(rig.gameObject));
        }

        [MenuItem("Tools/Web/Puente/Apagar el puente con la web")]
        private static void ShutdownMenu() => WebBridgeServer.Shutdown();

        [MenuItem("Tools/Web/Puente/Apagar el puente con la web", true)]
        private static bool CanShutdown() => WebBridgeServer.Running;

        [MenuItem("Tools/Web/Puente/Encender el puente con la web")]
        private static void StartMenu()
        {
            if (WebBridgeServer.Start()) Debug.Log($"[WebBridge] En marcha en 127.0.0.1:{WebBridgeServer.Port}: la web abierta lo encuentra sola.");
        }

        [MenuItem("Tools/Web/Puente/Encender el puente con la web", true)]
        private static bool CanStart() => !WebBridgeServer.Running;

        /// <summary>
        /// El prefab (asset) al que pertenece un objeto: él mismo si es un asset, el que está abierto en
        /// Prefab Mode, o el de la instancia de una escena. Null si no viene de un prefab.
        /// </summary>
        public static string PrefabPathOf(GameObject go)
        {
            if (go == null) return null;
            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null) return stage.assetPath;
            if (AssetDatabase.Contains(go)) return AssetDatabase.GetAssetPath(go.transform.root.gameObject);
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>Abre la web en ese enemigo. <paramref name="attack"/> = índice de la carta en la baraja.</summary>
        public static void Open(string prefabPath, string gesture = null, int attack = -1)
        {
            if (WebBridgeRoutes.LoadRigPrefab(prefabPath, out string problem) == null)
            {
                EditorUtility.DisplayDialog("Editar en la web",
                    problem ?? "Este objeto no viene de un prefab: la web edita el prefab guardado.", "OK");
                return;
            }

            // La web lee siempre el asset guardado: lo que esté a medias en Prefab Mode, antes a disco.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == prefabPath && stage.scene.isDirty)
            {
                if (!EditorUtility.DisplayDialog("Editar en la web",
                        $"'{stage.prefabContentsRoot.name}' tiene cambios sin guardar en Prefab Mode.\n\n" +
                        "La web lee el prefab guardado: hay que guardarlo antes de abrirla.",
                        "Guardar y abrir", "Cancelar"))
                    return;

                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
                stage.ClearDirtiness();
            }

            if (!WebBridgeServer.Start())
            {
                EditorUtility.DisplayDialog("Editar en la web",
                    $"El puente no pudo abrir ningún puerto local ({WebBridgeServer.DefaultPort}…). Mira la consola.", "OK");
                return;
            }

            if (!WebAnswers(WebBridgeServer.WebUrl))
            {
                string command = ServeCommandFor(WebBridgeServer.WebUrl);
                int choice = EditorUtility.DisplayDialogComplex("La web no responde",
                    $"No hay nada en {WebBridgeServer.WebUrl}.\n\nArráncala desde la carpeta del proyecto con:\n\n    {command}\n\n" +
                    "(Si la sirves en otra dirección, cámbiala en Biblioteca Jere, abajo: \"Web\".)",
                    "Copiar el comando", "Cancelar", "Abrir igualmente");
                if (choice == 0) { EditorGUIUtility.systemCopyBuffer = command; return; }
                if (choice == 1) return;
            }

            Application.OpenURL(WebBridgeServer.BuildUrl(prefabPath, gesture, attack));
        }

        private static string ServeCommandFor(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Port != 3000 ? $"{ServeCommand} -l {uri.Port}" : ServeCommand;

        private static bool WebAnswers(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                using (request.GetResponse()) return true;
            }
            catch (WebException e)
            {
                return e.Response != null;   // contestó (aunque sea un 404): hay un servidor
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ============================================================ Inspector

        private static bool _showLinks;

        /// <summary>
        /// El bloque del Inspector de un cuerpo por partes: el botón grande y, desplegable, un "✎" por
        /// gesto y por carta de la baraja (la web se abre con eso ya seleccionado). No dibuja nada si el
        /// objeto no tiene <see cref="ProceduralRig"/>.
        /// </summary>
        public static void DrawInspector(GameObject go)
        {
            var rig = go != null ? go.GetComponent<ProceduralRig>() : null;
            if (rig == null) return;

            string path = PrefabPathOf(go);
            bool boss = go.GetComponent<BossController>() != null;

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
            {
                var content = new GUIContent(boss ? "🌐  Editar gestos en la web" : "🌐  Editar gestos y ataques en la web",
                    string.IsNullOrEmpty(path)
                        ? "Este objeto no viene de un prefab guardado."
                        : "Abre la web en este enemigo, con sus gestos" + (boss ? "" : ", sus ataques") +
                          " y su sonido. Al guardar allí se escribe en este prefab: sin exportar ni importar.");
                if (GUILayout.Button(content, GUILayout.Height(26))) Later(() => Open(path));
            }

            if (string.IsNullOrEmpty(path)) return;

            var deck = boss ? null : go.GetComponent<EnemyStats>()?.Tuning.attacks;
            int gestures = rig.gestures != null ? rig.gestures.Count : 0;
            int cards = deck != null ? deck.Count : 0;
            if (gestures + cards == 0) { EditorGUILayout.Space(4f); return; }

            _showLinks = EditorGUILayout.Foldout(_showLinks, $"Abrir la web en… ({gestures} gestos" + (boss ? ")" : $", {cards} ataques)"), true);
            if (_showLinks)
            {
                EditorGUI.indentLevel++;
                var names = new HashSet<string>();
                foreach (var g in rig.gestures ?? new List<RigGesture>())
                    if (g != null && !string.IsNullOrEmpty(g.name) && names.Add(g.name))
                        LinkRow($"Gesto · {g.name}", () => Open(path, g.name));

                for (int i = 0; i < cards; i++)
                {
                    var attack = deck[i]?.attack;
                    if (attack == null) continue;
                    int index = i;
                    LinkRow($"Ataque · {attack.name}", () => Open(path, null, index));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);
        }

        /// <summary>Fuera del pase de IMGUI: abrir enseña diálogos, y uno a mitad de un grupo de layout lo rompe.</summary>
        private static void Later(Action action) => EditorApplication.delayCall += () => action();

        private static void LinkRow(string label, Action open)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label);
            if (GUILayout.Button(new GUIContent("✎ web", "Abrir la web con esto seleccionado."), EditorStyles.miniButton, GUILayout.Width(64f)))
                Later(open);
            EditorGUILayout.EndHorizontal();
        }
    }
}
