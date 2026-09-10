using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// La ventana del pipeline: todo el proceso en una pantalla.
    ///
    /// Existe para que la primera importación de una lámina no exija leer nada. Crea la receta,
    /// corta, anima, genera el enemigo y viste un prefab existente, en ese orden y con el estado
    /// de cada paso a la vista.
    /// </summary>
    public class SpritePipelineWindow : EditorWindow
    {
        private SpriteSheetRecipe _sheet;
        private EnemyRecipe _enemy;
        private GameObject _targetPrefab;
        private float _dressScale = 1f;
        private Vector2 _scroll;
        private string _log = "";

        [MenuItem("Tools/RedMagic/Pipeline/1 · Ventana de pipeline")]
        public static void Open()
        {
            var window = GetWindow<SpritePipelineWindow>("Sprite Pipeline");
            window.minSize = new Vector2(420f, 460f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Lámina → sprites → clips → prefab. Todo se guarda en la receta, así que " +
                "cualquier paso se puede relanzar sin volver a configurar nada.\n" +
                "Documentación: Assets/_Pipeline/SPRITE_PIPELINE.md",
                MessageType.Info);

            // ---------------------------------------------------------- hoja
            Header("1 · Hoja de sprites");

            _sheet = (SpriteSheetRecipe)EditorGUILayout.ObjectField(
                "Receta", _sheet, typeof(SpriteSheetRecipe), false);

            if (_sheet == null)
            {
                EditorGUILayout.HelpBox(
                    "Crea una con Assets ▸ Create ▸ RedMagic ▸ Pipeline ▸ Sprite Sheet Recipe, " +
                    "asígnale la textura y describe las filas.", MessageType.None);
            }
            else
            {
                using (new EditorGUI.DisabledScope(_sheet.sheet == null))
                {
                    if (GUILayout.Button("Cortar hoja + generar animación", GUILayout.Height(28f)))
                        _log = SpritePipeline.RunSheet(_sheet);
                }

                if (_sheet.sheet != null && HasNoAlpha(_sheet.sheet) && !_sheet.keyBackground)
                {
                    EditorGUILayout.HelpBox(
                        "La lámina no tiene canal alfa y 'Key Background' está apagado: los " +
                        "frames saldrán con el fondo pegado.", MessageType.Warning);
                }
            }

            // ---------------------------------------------------------- enemigo
            Header("2 · Enemigo nuevo");

            _enemy = (EnemyRecipe)EditorGUILayout.ObjectField(
                "Ficha", _enemy, typeof(EnemyRecipe), false);

            using (new EditorGUI.DisabledScope(_enemy == null))
            {
                if (GUILayout.Button("Cortar + generar prefab de enemigo", GUILayout.Height(28f)))
                    _log = SpritePipeline.RunEnemy(_enemy);
            }

            EditorGUILayout.LabelField(
                "Pone solo Health, Knockback, HitFlash, Corpse, CurrencyDropper,\n" +
                "EnemyStats, EnemyBrain, EnemyAnimation, EnemyAttack, cuerpo y collider.",
                EditorStyles.wordWrappedMiniLabel);

            // ---------------------------------------------------------- vestir
            Header("3 · Sustituir un placeholder");

            _targetPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab existente", _targetPrefab, typeof(GameObject), false);

            _dressScale = EditorGUILayout.FloatField("Escala del sprite", _dressScale);

            using (new EditorGUI.DisabledScope(_targetPrefab == null || _sheet == null))
            {
                if (GUILayout.Button("Vestir con la receta de arriba", GUILayout.Height(28f)))
                {
                    var log = new System.Text.StringBuilder();
                    PrefabDresser.Dress(_targetPrefab, _sheet, _dressScale, log);
                    _log = log.ToString();
                }
            }

            EditorGUILayout.LabelField(
                "Sólo cambia sprite y animación. Colliders, scripts y\n" +
                "referencias se quedan como están.",
                EditorStyles.wordWrappedMiniLabel);

            // ---------------------------------------------------------- auditoría
            Header("4 · Revisión");

            if (GUILayout.Button("Auditar enemigos y jefes"))
                _log = ContentAudit.Run(false);

            if (GUILayout.Button("Auditar y reparar lo que falte"))
                _log = ContentAudit.Run(true);

            // ---------------------------------------------------------- salida
            if (!string.IsNullOrEmpty(_log))
            {
                Header("Resultado");
                EditorGUILayout.TextArea(_log, GUILayout.MinHeight(140f));
                if (GUILayout.Button("Limpiar")) _log = "";
            }

            EditorGUILayout.EndScrollView();
        }

        private static void Header(string text)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        /// <summary>Un JPG, o un PNG exportado sin transparencia.</summary>
        private static bool HasNoAlpha(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            return path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
