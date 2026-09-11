using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Items.EditorTools
{
    /// <summary>
    /// Todos los items y armas del juego en una lista, con su icono editable. Cambiar un icono aquí
    /// lo cambia <b>en el asset del item</b> (el dato no vive en otro sitio), igual que hacerlo en su
    /// Inspector; "Editar" abre el item para tocar descripción, tags y efectos.
    /// </summary>
    public class ItemCatalogWindow : EditorWindow
    {
        private const float IconSize = 56f;

        private readonly List<ScriptableObject> _assets = new();
        private Vector2 _scroll;
        private string _filter = "";

        [MenuItem("Tools/RedMagic/Items/Catálogo de items (iconos)")]
        public static void Open() => GetWindow<ItemCatalogWindow>("Catálogo de items").Refresh();

        private void OnEnable() => Refresh();
        private void OnProjectChange() => Refresh();

        private void Refresh()
        {
            _assets.Clear();
            foreach (var type in new[] { "t:ItemDefinition", "t:WeaponDefinition" })
            foreach (var guid in AssetDatabase.FindAssets(type))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !_assets.Contains(asset)) _assets.Add(asset);
            }

            _assets.Sort((a, b) =>
            {
                int byGroup = Group(a).CompareTo(Group(b));
                return byGroup != 0 ? byGroup : string.CompareOrdinal(a.name, b.name);
            });
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Actualizar", EditorStyles.toolbarButton, GUILayout.Width(80))) Refresh();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string group = null;
            foreach (var asset in _assets.Where(Matches))
            {
                string g = GroupName(asset);
                if (g != group)
                {
                    group = g;
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(g, EditorStyles.boldLabel);
                }

                DrawRow(asset);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(ScriptableObject asset)
        {
            var so = new SerializedObject(asset);
            var icon = so.FindProperty("icon");
            var display = so.FindProperty("displayName");
            if (icon == null) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var rect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
                DrawSprite(rect, icon.objectReferenceValue as Sprite);

                using (new EditorGUILayout.VerticalScope())
                {
                    string title = display != null && !string.IsNullOrWhiteSpace(display.stringValue)
                        ? display.stringValue : asset.name;
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(asset.name, EditorStyles.miniLabel);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(icon, new GUIContent("Icono"));
                    if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();
                }

                if (GUILayout.Button("Editar", GUILayout.Width(60), GUILayout.Height(IconSize)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.15f));
            if (sprite == null)
            {
                GUI.Label(rect, "sin\nicono", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
                return;
            }

            var tex = sprite.texture;
            var r = sprite.textureRect;
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);

            // Encajar sin deformar.
            float aspect = r.width / r.height;
            var fit = aspect >= 1f
                ? new Rect(rect.x, rect.y + (rect.height - rect.width / aspect) * 0.5f, rect.width, rect.width / aspect)
                : new Rect(rect.x + (rect.width - rect.height * aspect) * 0.5f, rect.y, rect.height * aspect, rect.height);
            GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
        }

        private bool Matches(ScriptableObject asset) =>
            string.IsNullOrWhiteSpace(_filter) ||
            asset.name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static int Group(ScriptableObject asset) => asset switch
        {
            WeaponDefinition => 0,
            ElementModifier => 1,
            TrajectoryModifier => 2,
            ShapeModifier => 3,
            FreePoolItemDefinition => 4,
            _ => 5,
        };

        private static string GroupName(ScriptableObject asset) => Group(asset) switch
        {
            0 => "Armas",
            1 => "Elemento",
            2 => "Trayectoria",
            3 => "Forma",
            4 => "Pool libre",
            _ => "Otros",
        };
    }
}
