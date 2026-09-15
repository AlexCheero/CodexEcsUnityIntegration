using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using SystemEntry = CodexFramework.CodexEcsUnityIntegration.ECSPipelineBehaviour.SystemEntry;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(SystemsOverride))]
    public sealed class SystemsOverrideInspector : UnityEditor.Editor
    {
        private sealed class SelectionInspector
        {
            public SerializedProperty Property;
            public readonly Dictionary<ESystemCategory, ReorderableList> Lists = new();
            public string Search = "";
        }

        private readonly Dictionary<ESystemCategory, List<MonoScript>> _scripts = new();
        private SelectionInspector _add;
        private SelectionInspector _remove;

        private void OnEnable()
        {
            foreach (var category in IntegrationHelper.SystemCategories)
                _scripts[category] = new List<MonoScript>();
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                var type = script.GetClass();
                if (type == null || !IntegrationHelper.SystemTypes.ContainsKey(type.FullName))
                    continue;
                var categories = type.GetCustomAttribute<SystemAttribute>()?.Categories ?? ESystemCategory.Update;
                foreach (var category in IntegrationHelper.SystemCategories)
                    if (categories.Has(category))
                        _scripts[category].Add(script);
            }
            foreach (var scripts in _scripts.Values)
                scripts.Sort((a, b) => string.Compare(a.GetClass().FullName, b.GetClass().FullName, StringComparison.Ordinal));
            _add = CreateSelection("_systemsToAdd", true);
            _remove = CreateSelection("_systemsToRemove", false);
        }

        private SelectionInspector CreateSelection(string propertyName, bool adding)
        {
            var selection = new SelectionInspector { Property = serializedObject.FindProperty(propertyName) };
            foreach (var category in IntegrationHelper.SystemCategories)
            {
                var entries = selection.Property.FindPropertyRelative(GetFieldName(category));
                var list = new ReorderableList(serializedObject, entries, true, true, true, true);
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, ObjectNames.NicifyVariableName(category.ToString()));
                list.elementHeight = EditorGUIUtility.singleLineHeight + 4;
                list.drawElementCallback = (rect, index, _, _) => DrawEntry(rect, entries.GetArrayElementAtIndex(index), adding);
                list.onAddDropdownCallback = (rect, _) => ShowSystemMenu(rect, entries, category);
                selection.Lists.Add(category, list);
            }
            return selection;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.HelpBox("Applied to the starting pipeline before systems initialize. Removals run first; new systems are appended in list order. Adding an existing system updates its options in place.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawSelection(_add, "Systems to Add");
                DrawSelection(_remove, "Systems to Remove");
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSelection(SelectionInspector selection, string title)
        {
            selection.Property.isExpanded = EditorGUILayout.Foldout(selection.Property.isExpanded, title, true);
            if (!selection.Property.isExpanded)
                return;
            selection.Search = EditorGUILayout.TextField("Search systems", selection.Search);
            foreach (var category in IntegrationHelper.SystemCategories)
            {
                var list = selection.Lists[category];
                list.DoLayoutList();
                if (string.IsNullOrWhiteSpace(selection.Search))
                    continue;
                foreach (var script in _scripts[category])
                {
                    if (!IntegrationHelper.IsSearchMatch(selection.Search, script.GetClass().FullName) || Contains(list.serializedProperty, script))
                        continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(script, typeof(MonoScript), false);
                        if (GUILayout.Button("+", GUILayout.Width(24)))
                            AddEntry(list.serializedProperty, script);
                    }
                }
            }
        }

        private static void DrawEntry(Rect rect, SerializedProperty entry, bool adding)
        {
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            if (adding)
            {
                var options = new Rect(rect.xMax - 100, rect.y, 50, rect.height);
                DrawToggle(options, entry.FindPropertyRelative(nameof(SystemEntry.Active)), "A:", "Active");
                options.x += 50;
                DrawToggle(options, entry.FindPropertyRelative(nameof(SystemEntry.NonPausable)), "N:", "Non pausable");
                rect.width -= 104;
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(rect, entry.FindPropertyRelative(nameof(SystemEntry.Script)), GUIContent.none);
        }

        private static void DrawToggle(Rect rect, SerializedProperty property, string label, string tooltip)
        {
            EditorGUI.BeginProperty(rect, new GUIContent(label, tooltip), property);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 20, rect.height), new GUIContent(label, tooltip));
            property.boolValue = EditorGUI.Toggle(new Rect(rect.x + 20, rect.y, 20, rect.height), property.boolValue);
            EditorGUI.EndProperty();
        }

        private void ShowSystemMenu(Rect rect, SerializedProperty entries, ESystemCategory category)
        {
            var menu = new GenericMenu();
            var available = _scripts[category].Where(script => !Contains(entries, script)).ToArray();
            foreach (var script in available)
                menu.AddItem(new GUIContent(script.GetClass().FullName), false, () =>
                {
                    serializedObject.Update();
                    AddEntry(entries, script);
                    serializedObject.ApplyModifiedProperties();
                });
            if (available.Length == 0)
                menu.AddDisabledItem(new GUIContent("No more systems in this category"));
            menu.DropDown(rect);
        }

        private static bool Contains(SerializedProperty entries, MonoScript script)
        {
            for (var i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(SystemEntry.Script)).objectReferenceValue == script)
                    return true;
            return false;
        }

        private static void AddEntry(SerializedProperty entries, MonoScript script)
        {
            var index = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(nameof(SystemEntry.Script)).objectReferenceValue = script;
            entry.FindPropertyRelative(nameof(SystemEntry.Name)).stringValue = script.GetClass().FullName;
            entry.FindPropertyRelative(nameof(SystemEntry.Active)).boolValue = true;
            entry.FindPropertyRelative(nameof(SystemEntry.NonPausable)).boolValue = false;
        }

        private static string GetFieldName(ESystemCategory category) => category switch
        {
            ESystemCategory.Init => nameof(SystemsOverride.SystemSelection._initSystemScripts),
            ESystemCategory.Update => nameof(SystemsOverride.SystemSelection._updateSystemScripts),
            ESystemCategory.LateUpdate => nameof(SystemsOverride.SystemSelection._lateUpdateSystemScripts),
            ESystemCategory.FixedUpdate => nameof(SystemsOverride.SystemSelection._fixedUpdateSystemScripts),
            ESystemCategory.LateFixedUpdate => nameof(SystemsOverride.SystemSelection._lateFixedUpdateSystemScripts),
            ESystemCategory.OnEnable => nameof(SystemsOverride.SystemSelection._enableSystemScripts),
            ESystemCategory.OnDisable => nameof(SystemsOverride.SystemSelection._disableSystemScripts),
            ESystemCategory.Reactive => nameof(SystemsOverride.SystemSelection._reactiveSystemScripts),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };
    }
}
