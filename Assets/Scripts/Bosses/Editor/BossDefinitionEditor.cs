using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RedMagic.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// El "banco de trabajo" de un jefe en el Inspector: sus fases y, en cada una, la baraja de
    /// ataques — añadir un ataque nuevo eligiendo su <b>tipo</b> de la lista de todos los que hay en
    /// el juego, reusar uno existente, reordenar, quitar, y editar cada uno ahí mismo con:
    ///  - el <b>gesto</b> como desplegable de las animaciones del cuerpo del jefe;
    ///  - sus <b>huecos de arte</b> marcados (qué pide y qué sale si se deja vacío).
    ///
    /// Sale al seleccionar el asset <see cref="BossDefinition"/> y también dentro del
    /// <see cref="BossController"/> del prefab (ver <see cref="BossControllerEditor"/>).
    /// </summary>
    [CustomEditor(typeof(BossDefinition))]
    public class BossDefinitionEditor : UnityEditor.Editor
    {
        private const string NoGesture = "(sin gesto)";

        private SerializedProperty _displayName, _title, _description, _phases;

        private readonly Dictionary<string, ReorderableList> _lists = new Dictionary<string, ReorderableList>();
        private readonly Dictionary<BossAttack, SerializedObject> _attackObjects = new Dictionary<BossAttack, SerializedObject>();
        private static readonly HashSet<int> Expanded = new HashSet<int>();
        private static readonly HashSet<string> OpenPhases = new HashSet<string>();
        private static readonly HashSet<string> AdvancedPhases = new HashSet<string>();

        /// <summary>Si el ataque está desplegado en el inspector (la escena dibuja sus cajas y puntos).</summary>
        internal static bool IsExpanded(BossAttack attack) => attack != null && Expanded.Contains(attack.GetInstanceID());

        /// <summary>Ataque cuya pose (frame de suelta del gesto) se superpone en la escena. 0 = ninguno.</summary>
        private static int _poseAttack;

        internal static bool ShowsPose(BossAttack attack) =>
            attack != null && _poseAttack == attack.GetInstanceID() && IsExpanded(attack);

        private GameObject _prefabHint;
        private string[] _gestures = { NoGesture };
        private string _gestureSource = "ningún prefab usa esta definición";

        /// <summary>El prefab (o instancia) cuyo Animator da la lista de gestos. Lo pone BossControllerEditor.</summary>
        public GameObject PrefabHint
        {
            get => _prefabHint;
            set
            {
                if (_prefabHint == value) return;
                _prefabHint = value;
                RefreshGestures();
            }
        }

        private void OnEnable()
        {
            if (target == null) return;
            _displayName = serializedObject.FindProperty("displayName");
            _title = serializedObject.FindProperty("title");
            _description = serializedObject.FindProperty("description");
            _phases = serializedObject.FindProperty("phases");
            RefreshGestures();
        }

        // ============================================================ gestos

        private void RefreshGestures()
        {
            var source = _prefabHint != null ? _prefabHint : FindPrefabUsing((BossDefinition)target);
            var states = new List<string>();

            var animator = source != null ? source.GetComponentInChildren<Animator>(true) : null;
            if (animator != null && animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)
            {
                foreach (var layer in controller.layers)
                    foreach (var child in layer.stateMachine.states)
                        if (!states.Contains(child.state.name)) states.Add(child.state.name);
            }

            states.Sort(StringComparer.Ordinal);
            _gestures = new[] { NoGesture }.Concat(states).ToArray();
            _gestureSource = source == null
                ? "ningún prefab usa esta definición (el gesto se escribe a mano)"
                : states.Count == 0 ? $"'{source.name}' no tiene Animator con estados" : $"animaciones de '{source.name}'";
        }

        private static GameObject FindPrefabUsing(BossDefinition definition)
        {
            if (definition == null) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var boss = go != null ? go.GetComponent<BossController>() : null;
                if (boss != null && boss.Definition == definition) return go;
            }
            return null;
        }

        // ============================================================ inspector

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_displayName, new GUIContent("Nombre"));
            EditorGUILayout.PropertyField(_title, new GUIContent("Epíteto"));
            EditorGUILayout.PropertyField(_description, new GUIContent("Descripción"));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Gestos: {_gestureSource}", EditorStyles.miniLabel);
                if (GUILayout.Button("Recargar", EditorStyles.miniButton, GUILayout.Width(70))) RefreshGestures();
            }

            for (int i = 0; i < _phases.arraySize; i++)
            {
                if (DrawPhase(i)) break;   // se ha borrado una fase: se repinta en el siguiente frame
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Añadir fase")) AddPhase();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Devuelve true si la fase se ha borrado (el array ha cambiado).</summary>
        private bool DrawPhase(int index)
        {
            var phase = _phases.GetArrayElementAtIndex(index);
            var attacks = phase.FindPropertyRelative("attacks");
            string key = $"{target.GetInstanceID()}:{index}";

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool open = OpenPhases.Contains(key) || index == 0 && !OpenPhases.Contains("closed:" + key);
                    string header = $"{phase.FindPropertyRelative("displayName").stringValue}  ·  desde {phase.FindPropertyRelative("startsAtHealth").floatValue * 100f:0}% de vida  ·  {attacks.arraySize} ataque(s)";
                    bool now = EditorGUILayout.Foldout(open, header, true, EditorStyles.foldoutHeader);
                    if (now != open)
                    {
                        if (now) { OpenPhases.Add(key); OpenPhases.Remove("closed:" + key); }
                        else { OpenPhases.Remove(key); OpenPhases.Add("closed:" + key); }
                    }

                    if (index > 0 && GUILayout.Button("Quitar fase", GUILayout.Width(90)) &&
                        EditorUtility.DisplayDialog("Quitar fase", $"¿Quitar '{phase.FindPropertyRelative("displayName").stringValue}'? (los ataques no se borran)", "Quitar", "Cancelar"))
                    {
                        _phases.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                        _lists.Clear();
                        GUIUtility.ExitGUI();
                        return true;
                    }

                    if (!now) return false;
                }

                DrawPhaseFields(phase, key);

                EditorGUILayout.Space(2);
                var list = ListFor(attacks);
                list.DoLayoutList();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Nuevo ataque ▾")) NewAttackMenu(index).ShowAsContext();
                    if (GUILayout.Button("+ Reusar existente ▾")) ReuseAttackMenu(index).ShowAsContext();
                }

                for (int i = 0; i < attacks.arraySize; i++)
                {
                    var attack = attacks.GetArrayElementAtIndex(i).objectReferenceValue as BossAttack;
                    if (attack == null) continue;
                    DrawAttack(attack);
                }
            }

            return false;
        }

        private void DrawPhaseFields(SerializedProperty phase, string key)
        {
            Field(phase, "displayName", "Nombre");
            if (phase.propertyPath.EndsWith("[0]"))
                EditorGUILayout.LabelField("Empieza a vida llena (la primera fase siempre).", EditorStyles.miniLabel);
            else
                Field(phase, "startsAtHealth", "Empieza a (vida)");
            Field(phase, "pauseBetweenAttacks", "Pausa entre ataques (min/max)");
            Field(phase, "speedScale", "Ritmo");
            Field(phase, "damageScale", "Daño ×");
            Field(phase, "damageTakenMultiplier", "Daño recibido ×");
            Field(phase, "accent", "Color de la fase");
            Field(phase, "openingAttack", "Ataque de entrada");

            bool advanced = AdvancedPhases.Contains(key);
            bool now = EditorGUILayout.Foldout(advanced, "Transición y frenesí", true);
            if (now != advanced)
            {
                if (now) AdvancedPhases.Add(key);
                else AdvancedPhases.Remove(key);
            }
            if (!now) return;

            EditorGUI.indentLevel++;
            Field(phase, "transitionSeconds", "Segundos de transición");
            Field(phase, "transitionShake", "Sacudida");
            Field(phase, "transitionSfxId", "Sonido");
            Field(phase, "transitionFx", "Efecto");
            Field(phase, "frenzyBelowHealth", "Frenesí por debajo de");
            Field(phase, "frenzySpeedScale", "Ritmo del frenesí");
            EditorGUI.indentLevel--;
        }

        private static void Field(SerializedProperty parent, string name, string label)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label), true);
        }

        // ============================================================ lista de ataques

        private ReorderableList ListFor(SerializedProperty attacks)
        {
            if (!_lists.TryGetValue(attacks.propertyPath, out var list))
            {
                list = new ReorderableList(serializedObject, attacks, true, true, true, true);
                list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Baraja de ataques (arrastra para reordenar)");
                list.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 6f;
                list.drawElementCallback = (rect, i, active, focused) =>
                {
                    var element = list.serializedProperty.GetArrayElementAtIndex(i);
                    var attack = element.objectReferenceValue as BossAttack;

                    float line = EditorGUIUtility.singleLineHeight;
                    var top = new Rect(rect.x, rect.y + 2f, rect.width, line);
                    var bottom = new Rect(rect.x, rect.y + line + 4f, rect.width, line);

                    EditorGUI.PropertyField(top, element, GUIContent.none);
                    string info = attack == null
                        ? "vacío — arrastra un ataque o usa los botones de abajo"
                        : $"{Nicify(attack.GetType())} · {attack.ShortStats()}" +
                          (string.IsNullOrEmpty(attack.Gesture) ? "" : $" · gesto {attack.Gesture}");
                    EditorGUI.LabelField(bottom, info, EditorStyles.miniLabel);
                };
                _lists[attacks.propertyPath] = list;
            }

            // La SerializedProperty cambia de instancia entre repintados: siempre la actual.
            list.serializedProperty = attacks;
            return list;
        }

        private GenericMenu NewAttackMenu(int phaseIndex)
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<BossAttack>()
                .Where(t => !t.IsAbstract)
                .OrderBy(Nicify)
                .ToList();

            foreach (var type in types)
            {
                var t = type;
                menu.AddItem(new GUIContent(Nicify(t)), false, () => CreateAttack(phaseIndex, t));
            }

            return menu;
        }

        private GenericMenu ReuseAttackMenu(int phaseIndex)
        {
            var menu = new GenericMenu();
            var assets = AssetDatabase.FindAssets("t:BossAttack")
                .Select(g => AssetDatabase.LoadAssetAtPath<BossAttack>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => Nicify(a.GetType())).ThenBy(a => a.name)
                .ToList();

            if (assets.Count == 0) menu.AddDisabledItem(new GUIContent("No hay ataques en el proyecto"));

            foreach (var asset in assets)
            {
                var a = asset;
                menu.AddItem(new GUIContent($"{Nicify(a.GetType())}/{a.name}"), false, () => Append(phaseIndex, a));
            }

            return menu;
        }

        private void CreateAttack(int phaseIndex, Type type)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(target))?.Replace('\\', '/') ?? "Assets/Resources/Bosses";
            string shortType = type.Name.EndsWith("Attack") ? type.Name.Substring(0, type.Name.Length - 6) : type.Name;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/BossAttack_{target.name.Replace("Boss_", "")}_{shortType}.asset");

            var asset = (BossAttack)CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Append(phaseIndex, asset);
            Expanded.Add(asset.GetInstanceID());
            EditorGUIUtility.PingObject(asset);
        }

        private void Append(int phaseIndex, BossAttack attack)
        {
            serializedObject.Update();
            var attacks = _phases.GetArrayElementAtIndex(phaseIndex).FindPropertyRelative("attacks");
            attacks.arraySize++;
            attacks.GetArrayElementAtIndex(attacks.arraySize - 1).objectReferenceValue = attack;
            serializedObject.ApplyModifiedProperties();
        }

        private void AddPhase()
        {
            int i = _phases.arraySize;
            _phases.arraySize++;
            var p = _phases.GetArrayElementAtIndex(i);

            // Un elemento nuevo de array hereda/nace con valores que no son los del C#: se escriben
            // todos (el fallo del jefe invencible por damageTakenMultiplier = 0).
            p.FindPropertyRelative("displayName").stringValue = $"Fase {i + 1}";
            p.FindPropertyRelative("startsAtHealth").floatValue = i == 0 ? 1f : 0.5f;
            p.FindPropertyRelative("attacks").arraySize = 0;
            p.FindPropertyRelative("pauseBetweenAttacks").vector2Value = new Vector2(0.9f, 1.6f);
            p.FindPropertyRelative("damageScale").floatValue = 1f;
            p.FindPropertyRelative("damageTakenMultiplier").floatValue = 1f;
            p.FindPropertyRelative("speedScale").floatValue = 1f;
            p.FindPropertyRelative("accent").colorValue = new Color(0.55f, 0.9f, 0.4f, 1f);
            p.FindPropertyRelative("transitionSeconds").floatValue = i == 0 ? 0f : 1.8f;
            p.FindPropertyRelative("transitionShake").floatValue = 0.5f;
            p.FindPropertyRelative("transitionSfxId").stringValue = string.Empty;
            p.FindPropertyRelative("transitionFx").objectReferenceValue = null;
            p.FindPropertyRelative("openingAttack").objectReferenceValue = null;
            p.FindPropertyRelative("frenzyBelowHealth").floatValue = 0f;
            p.FindPropertyRelative("frenzySpeedScale").floatValue = 1.35f;

            OpenPhases.Add($"{target.GetInstanceID()}:{i}");
        }

        // ============================================================ ataque en línea

        private void DrawAttack(BossAttack attack)
        {
            int id = attack.GetInstanceID();
            bool open = Expanded.Contains(id);
            bool now = EditorGUILayout.Foldout(open, $"Editar · {attack.DisplayName}  ({Nicify(attack.GetType())})", true);
            if (now != open)
            {
                if (now) Expanded.Add(id);
                else Expanded.Remove(id);
            }
            if (!now) return;

            if (!_attackObjects.TryGetValue(attack, out var so) || so.targetObject == null)
            {
                so = new SerializedObject(attack);
                _attackObjects[attack] = so;
            }

            so.Update();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int users = CountUsers(attack);
                if (users > 1)
                    EditorGUILayout.HelpBox($"Este ataque está en {users} barajas: cambiarlo cambia todas.", MessageType.Info);

                if (!string.IsNullOrEmpty(attack.Gesture))
                {
                    bool pose = _poseAttack == id;
                    bool want = GUILayout.Toggle(pose, pose ? "◉ Viendo pose del golpe en la escena (clic para quitar)"
                                                            : "○ Ver pose del golpe en la escena", "Button");
                    if (want != pose)
                    {
                        _poseAttack = want ? id : 0;
                        SceneView.RepaintAll();
                    }
                }

                var hidden = HiddenPaths(attack.GetType());
                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.name == "m_Script" || hidden.Contains(it.propertyPath)) continue;

                    if (it.name == "gesture")
                    {
                        DrawGesture(it);
                        continue;
                    }

                    var slot = ArtSlotOf(attack.GetType(), it.name);
                    if (slot == null)
                    {
                        DrawFiltered(it, new GUIContent(it.displayName, it.tooltip), hidden);
                        continue;
                    }

                    DrawFiltered(it, new GUIContent($"Arte · {slot.Label}", it.tooltip), hidden);
                    if (IsEmptyArt(it))
                        EditorGUILayout.LabelField($"      vacío → {slot.Placeholder}", EditorStyles.miniLabel);
                }
            }
            so.ApplyModifiedProperties();
        }

        private void DrawGesture(SerializedProperty gesture)
        {
            string current = gesture.stringValue ?? string.Empty;
            var options = _gestures.ToList();
            if (!string.IsNullOrEmpty(current) && !options.Contains(current)) options.Add($"{current} (no existe)");

            int index = string.IsNullOrEmpty(current) ? 0 : Mathf.Max(0, options.FindIndex(o => o == current || o == $"{current} (no existe)"));
            int chosen = EditorGUILayout.Popup(new GUIContent("Gesto (animación)", gesture.tooltip), index,
                                               options.Select(o => new GUIContent(o)).ToArray());
            if (chosen != index)
            {
                string value = options[chosen];
                gesture.stringValue = chosen == 0 ? string.Empty : value.Replace(" (no existe)", string.Empty);
            }

            if (_gestures.Length <= 1) EditorGUILayout.PropertyField(gesture, new GUIContent("Gesto (texto)"));
        }

        private int CountUsers(BossAttack attack)
        {
            int users = 0;
            for (int p = 0; p < _phases.arraySize; p++)
            {
                var attacks = _phases.GetArrayElementAtIndex(p).FindPropertyRelative("attacks");
                for (int i = 0; i < attacks.arraySize; i++)
                    if (attacks.GetArrayElementAtIndex(i).objectReferenceValue == attack) users++;
            }
            return users;
        }

        // ============================================================ utilidades

        private static readonly Dictionary<Type, HashSet<string>> HiddenCache = new Dictionary<Type, HashSet<string>>();

        /// <summary>Campos que el tipo declara como no usados (<see cref="AttackHidesAttribute"/>).</summary>
        private static HashSet<string> HiddenPaths(Type type)
        {
            if (HiddenCache.TryGetValue(type, out var set)) return set;
            set = new HashSet<string>();
            foreach (AttackHidesAttribute a in type.GetCustomAttributes(typeof(AttackHidesAttribute), true))
                foreach (var path in a.Paths) set.Add(path);
            HiddenCache[type] = set;
            return set;
        }

        /// <summary>Como PropertyField, pero sin los hijos ocultos.</summary>
        private static void DrawFiltered(SerializedProperty prop, GUIContent label, HashSet<string> hidden)
        {
            string prefix = prop.propertyPath + ".";
            if (!prop.hasVisibleChildren || !hidden.Any(h => h.StartsWith(prefix, StringComparison.Ordinal)))
            {
                EditorGUILayout.PropertyField(prop, label, true);
                return;
            }

            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, label, true);
            if (!prop.isExpanded) return;

            EditorGUI.indentLevel++;
            var child = prop.Copy();
            var end = prop.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                if (hidden.Contains(child.propertyPath)) continue;
                DrawFiltered(child.Copy(), new GUIContent(child.displayName, child.tooltip), hidden);
            }
            EditorGUI.indentLevel--;
        }

        private static ArtSlotAttribute ArtSlotOf(Type type, string fieldName)
        {
            for (var t = type; t != null && t != typeof(ScriptableObject); t = t.BaseType)
            {
                var field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public |
                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetCustomAttribute<ArtSlotAttribute>();
            }
            return null;
        }

        private static bool IsEmptyArt(SerializedProperty prop)
        {
            if (prop.propertyType == SerializedPropertyType.ObjectReference) return prop.objectReferenceValue == null;
            var prefab = prop.FindPropertyRelative("prefab");
            return prefab != null && prefab.objectReferenceValue == null;
        }

        private static string Nicify(Type type)
        {
            string n = type.Name.EndsWith("Attack") ? type.Name.Substring(0, type.Name.Length - 6) : type.Name;
            return ObjectNames.NicifyVariableName(n);
        }
    }

    /// <summary>
    /// El BossController de siempre y, debajo, el banco de trabajo de su definición: así los
    /// ataques del jefe se gestionan desde su prefab, sin buscar el asset.
    ///
    /// En la escena (jefe seleccionado en una escena o abierto en modo prefab) dibuja las cajas y
    /// puntos de los ataques <b>desplegados</b> en el inspector — sus <see cref="AttackBoxAttribute"/>
    /// y <see cref="AttackPointAttribute"/> — y deja moverlos y redimensionarlos con el ratón. Con
    /// "Ver pose del golpe" superpone el frame de suelta del gesto, para colocar la caja sobre el puño.
    /// </summary>
    [CustomEditor(typeof(BossController))]
    public class BossControllerEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _definitionEditor;
        private bool _showAttacks = true;
        private readonly Dictionary<BossAttack, SerializedObject> _objects = new Dictionary<BossAttack, SerializedObject>();
        private GameObject _poseObject;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var boss = (BossController)target;
            var definition = boss.Definition;

            EditorGUILayout.Space(8);
            if (definition == null)
            {
                EditorGUILayout.HelpBox("Sin BossDefinition: asígnala arriba para editar sus fases y ataques aquí.", MessageType.Info);
                return;
            }

            _showAttacks = EditorGUILayout.BeginFoldoutHeaderGroup(_showAttacks, $"Fases y ataques · {definition.name}");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_showAttacks) return;

            EditorGUILayout.HelpBox("Despliega un ataque ('Editar · …') y selecciona el jefe en la escena (o abre su " +
                                    "prefab): sus cajas de golpe y puntos salen dibujados y se arrastran con el ratón. " +
                                    "Dentro de cada ataque, el botón 'Ver pose del golpe' superpone su frame de suelta.",
                                    MessageType.None);

            CreateCachedEditor(definition, typeof(BossDefinitionEditor), ref _definitionEditor);
            if (_definitionEditor is BossDefinitionEditor editor) editor.PrefabHint = boss.gameObject;

            EditorGUI.BeginChangeCheck();
            _definitionEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            DestroyPose();
            if (_definitionEditor != null) DestroyImmediate(_definitionEditor);
        }

        // ============================================================ escena

        private void OnSceneGUI()
        {
            var boss = (BossController)target;
            var definition = boss.Definition;
            if (definition == null || definition.Phases == null) return;

            var body = boss.GetComponentInChildren<SpriteRenderer>();
            int facing = body != null && body.flipX ? -1 : 1;
            Vector3 origin = boss.transform.position;

            BossAttack poseAttack = null;
            var seen = new HashSet<BossAttack>();

            foreach (var phase in definition.Phases)
            {
                if (phase?.attacks == null) continue;
                foreach (var attack in phase.attacks)
                {
                    if (attack == null || !seen.Add(attack) || !BossDefinitionEditor.IsExpanded(attack)) continue;
                    if (poseAttack == null && BossDefinitionEditor.ShowsPose(attack)) poseAttack = attack;
                    DrawAttack(attack, origin, facing);
                }
            }

            UpdatePose(boss, body, poseAttack);
        }

        private void DrawAttack(BossAttack attack, Vector3 origin, int facing)
        {
            if (!_objects.TryGetValue(attack, out var so) || so.targetObject == null)
            {
                so = new SerializedObject(attack);
                _objects[attack] = so;
            }
            so.Update();

            var type = attack.GetType();
            foreach (AttackBoxAttribute box in type.GetCustomAttributes(typeof(AttackBoxAttribute), true))
                DrawBox(so, box, origin, facing, attack.DisplayName);
            foreach (AttackPointAttribute point in type.GetCustomAttributes(typeof(AttackPointAttribute), true))
                DrawPoint(so, point, origin, facing, attack.DisplayName);

            so.ApplyModifiedProperties();
        }

        private static void DrawBox(SerializedObject so, AttackBoxAttribute box, Vector3 origin, int facing, string name)
        {
            var offsetProp = so.FindProperty(box.OffsetPath);
            var sizeProp = so.FindProperty(box.SizePath);
            if (offsetProp == null || sizeProp == null ||
                offsetProp.propertyType != SerializedPropertyType.Vector2 ||
                sizeProp.propertyType != SerializedPropertyType.Vector2) return;

            Vector2 offset = offsetProp.vector2Value;
            Vector2 size = sizeProp.vector2Value;
            var center = origin + new Vector3(offset.x * facing, offset.y, 0f);

            var half = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            var verts = new[]
            {
                center + new Vector3(-half.x, -half.y), center + new Vector3(-half.x, half.y),
                center + new Vector3(half.x, half.y), center + new Vector3(half.x, -half.y),
            };
            Handles.DrawSolidRectangleWithOutline(verts, new Color(1f, 0.25f, 0.2f, 0.12f), new Color(1f, 0.3f, 0.2f, 1f));

            // Bordes: redimensionar.
            var bounds = new UnityEditor.IMGUI.Controls.BoxBoundsHandle
            {
                axes = UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.X | UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y,
                center = center,
                size = new Vector3(size.x, size.y, 0f),
                handleColor = new Color(1f, 0.5f, 0.3f, 1f),
                wireframeColor = Color.clear,
            };
            EditorGUI.BeginChangeCheck();
            bounds.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                sizeProp.vector2Value = new Vector2(Mathf.Max(0.1f, Mathf.Abs(bounds.size.x)), Mathf.Max(0.1f, Mathf.Abs(bounds.size.y)));
                offsetProp.vector2Value = new Vector2((bounds.center.x - origin.x) * facing, bounds.center.y - origin.y);
                center = bounds.center;
            }

            // Centro: mover.
            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;
            var moved = Handles.FreeMoveHandle(center, handleSize, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
                offsetProp.vector2Value = new Vector2((moved.x - origin.x) * facing, moved.y - origin.y);

            Handles.Label(center + new Vector3(-half.x, half.y + HandleUtility.GetHandleSize(center) * 0.25f, 0f),
                          $"{name} · {box.Label}\ncentro ({offsetProp.vector2Value.x:0.0}, {offsetProp.vector2Value.y:0.0})  tamaño {sizeProp.vector2Value.x:0.0}×{sizeProp.vector2Value.y:0.0}",
                          EditorStyles.whiteMiniLabel);
        }

        private static void DrawPoint(SerializedObject so, AttackPointAttribute point, Vector3 origin, int facing, string name)
        {
            var offsetProp = so.FindProperty(point.OffsetPath);
            if (offsetProp == null || offsetProp.propertyType != SerializedPropertyType.Vector2) return;

            Vector2 offset = offsetProp.vector2Value;
            var at = origin + new Vector3(offset.x * facing, offset.y, 0f);
            float size = HandleUtility.GetHandleSize(at);

            Handles.color = new Color(0.3f, 0.9f, 1f, 1f);
            Handles.DrawWireDisc(at, Vector3.forward, size * 0.12f);
            Handles.DrawLine(at + Vector3.left * size * 0.2f, at + Vector3.right * size * 0.2f);
            Handles.DrawLine(at + Vector3.down * size * 0.2f, at + Vector3.up * size * 0.2f);

            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(at, size * 0.08f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
                offsetProp.vector2Value = new Vector2((moved.x - origin.x) * facing, moved.y - origin.y);

            Handles.Label(at + new Vector3(size * 0.15f, size * 0.25f, 0f),
                          $"{name} · {point.Label} ({offsetProp.vector2Value.x:0.0}, {offsetProp.vector2Value.y:0.0})",
                          EditorStyles.whiteMiniLabel);
        }

        // ============================================================ pose del golpe

        /// <summary>
        /// Superpone, sin tocar el jefe, el frame de suelta del gesto del ataque desplegado: un objeto
        /// oculto que no se guarda en ninguna escena ni prefab.
        /// </summary>
        private void UpdatePose(BossController boss, SpriteRenderer body, BossAttack attack)
        {
            if (attack == null || body == null)
            {
                DestroyPose();
                return;
            }

            var sprite = ReleaseSprite(boss.GetComponent<Animator>(), attack.Gesture);
            if (sprite == null)
            {
                DestroyPose();
                return;
            }

            if (_poseObject == null)
            {
                _poseObject = new GameObject("Boss Pose Preview") { hideFlags = HideFlags.HideAndDontSave };
                _poseObject.AddComponent<SpriteRenderer>();

                // En modo prefab sólo se dibuja lo que está en la escena del prefab.
                var scene = boss.gameObject.scene;
                if (scene.IsValid() && scene != _poseObject.scene)
                {
                    try { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_poseObject, scene); }
                    catch (System.Exception) { /* se queda en la escena activa: sólo se ve fuera del modo prefab */ }
                }
            }

            var renderer = _poseObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = body.flipX;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 20;
            renderer.color = new Color(1f, 1f, 1f, 0.85f);

            var t = body.transform;
            _poseObject.transform.SetPositionAndRotation(t.position, t.rotation);
            _poseObject.transform.localScale = t.lossyScale;

            Handles.Label(renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + HandleUtility.GetHandleSize(t.position) * 0.2f),
                          $"POSE · {attack.DisplayName} · {attack.Gesture} (frame de suelta)", EditorStyles.whiteBoldLabel);
        }

        private void DestroyPose()
        {
            if (_poseObject != null) DestroyImmediate(_poseObject);
            _poseObject = null;
        }

        /// <summary>El sprite del frame en el que el gesto suelta el golpe (evento OnAttackRelease).</summary>
        private static Sprite ReleaseSprite(Animator animator, string state)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return null;
            if (!(animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)) return null;

            AnimationClip clip = null;
            foreach (var layer in controller.layers)
                foreach (var child in layer.stateMachine.states)
                    if (child.state.name == state) clip = child.state.motion as AnimationClip;
            if (clip == null) return null;

            float release = clip.length;
            foreach (var e in AnimationUtility.GetAnimationEvents(clip))
                if (e.functionName == "OnAttackRelease") { release = e.time; break; }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Sprite best = null;
                foreach (var key in keys)
                    if (key.time <= release + 0.0001f && key.value is Sprite s) best = s;
                return best;
            }

            return null;
        }
    }
}
