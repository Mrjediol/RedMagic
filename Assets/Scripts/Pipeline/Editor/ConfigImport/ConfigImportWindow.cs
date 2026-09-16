using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// UI del importador: archivo JSON, biblioteca de proyectiles opcional (para resolver 'use'),
    /// tipo explícito (por si la detección automática no basta) y el flag de reset de tuning —
    /// los dos parámetros que <c>ConfigImportRunner.ImportFile</c> expone y que un simple menú de
    /// "elige un archivo" no tiene dónde pedir.
    ///
    /// Cuando el archivo cargado es un ProjectileConfig <b>suelto</b> (no biblioteca), enseña ya
    /// en la ventana — antes de que se pulse Importar — a qué asset seleccionado en el Project se
    /// va a aplicar, o por qué no se puede. El diálogo de confirmación de
    /// <c>ProjectileConfigImporter.ApplyToSelection</c> es la última salvaguarda, no la única: el
    /// riesgo de "escribir sobre lo que esté seleccionado" tiene que verse antes del clic.
    /// </summary>
    public class ConfigImportWindow : EditorWindow
    {
        private string _configPath = "";
        private string _libraryPath = "";
        private ConfigKind _kind = ConfigKind.Auto;
        private bool _resetEnemyTuning;
        private Vector2 _scroll;
        private string _lastReport = "";

        // Vista previa de "¿es un ProjectileConfig suelto, y a qué se aplicaría?", cacheada por
        // (ruta, tipo elegido) para no reparsear el JSON en cada repintado.
        private string _previewKey;
        private bool _isStandaloneProjectileConfig;

        private void OnEnable() => Repaint();

        // La selección del Project puede cambiar sin que esta ventana reciba ningún otro evento;
        // sin esto, la vista previa del destino se quedaría enseñando un asset que ya no es el
        // seleccionado.
        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            GUILayout.Label("Importar EnemyConfig / ProjectileConfig / BossConfig", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enemy y Boss config se detectan solos por sus campos. Un ProjectileConfig suelto " +
                "(sin 'projectiles') se aplica sobre el asset seleccionado en el Project — " +
                "selecciónalo antes de importar.", MessageType.None);

            EditorGUILayout.Space();
            PathField("Archivo JSON", ref _configPath, "Selecciona un config JSON");
            PathField("Biblioteca de proyectiles (opcional)", ref _libraryPath, "Selecciona una biblioteca de proyectiles");

            _kind = (ConfigKind)EditorGUILayout.EnumPopup("Tipo", _kind);
            _resetEnemyTuning = EditorGUILayout.ToggleLeft(
                "Resetear tuning del enemigo a los valores del JSON (sólo EnemyConfig)", _resetEnemyTuning);

            RefreshProjectilePreview();
            if (_isStandaloneProjectileConfig) DrawProjectileTargetPreview();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_configPath)))
            {
                if (GUILayout.Button("Importar", GUILayout.Height(28))) RunImport();
            }

            if (string.IsNullOrEmpty(_lastReport)) return;

            EditorGUILayout.Space();
            GUILayout.Label("Resultado", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Reparsea el JSON sólo cuando la ruta o el tipo elegido cambian — el resto del repintado
        /// (p.ej. la selección cambiando) es barato y se recalcula siempre en
        /// <see cref="DrawProjectileTargetPreview"/> sin volver a tocar disco.
        /// </summary>
        private void RefreshProjectilePreview()
        {
            string key = $"{_configPath}|{_kind}";
            if (key == _previewKey) return;
            _previewKey = key;
            _isStandaloneProjectileConfig = false;

            if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath)) return;

            try
            {
                var root = JObject.Parse(File.ReadAllText(_configPath));
                var kind = _kind == ConfigKind.Auto ? ConfigImportRunner.DetectKind(root) : _kind;
                _isStandaloneProjectileConfig = kind == ConfigKind.Projectile && root["projectiles"] == null;
            }
            catch
            {
                // JSON inválido: se reportará igual al pulsar Importar. No hace falta duplicar el
                // error aquí — esta vista previa sólo cubre el caso "proyectil suelto".
            }
        }

        private static void DrawProjectileTargetPreview()
        {
            var resolution = ProjectileConfigImporter.ResolveApplyTarget();

            if (resolution.IsValid)
                EditorGUILayout.HelpBox(
                    $"Este archivo es un ProjectileConfig suelto. Se aplicará a:\n" +
                    $"'{resolution.Target.name}' ({resolution.TargetLabel})", MessageType.Warning);
            else
                EditorGUILayout.HelpBox(
                    $"Este archivo es un ProjectileConfig suelto, pero no se puede aplicar: {resolution.Error}",
                    MessageType.Error);
        }

        private static void PathField(string label, ref string path, string dialogTitle)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFilePanel(dialogTitle, Application.dataPath, "json");
                if (!string.IsNullOrEmpty(picked)) path = picked;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RunImport()
        {
            _lastReport = ConfigImportRunner.ImportFile(_configPath,
                string.IsNullOrEmpty(_libraryPath) ? null : _libraryPath,
                _kind, _resetEnemyTuning);

            Debug.Log(_lastReport);
            Repaint();
        }
    }
}
