using System;
using System.Collections.Generic;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomPropertyDrawer(typeof(EntityGroupMask))]
    public sealed class EntityGroupMaskDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var field = EditorGUI.PrefixLabel(position, label);
            var specified = property.FindPropertyRelative("_specified").boolValue;
            var selected = property.FindPropertyRelative("_componentTypes");
            var summary = "None (use initial components)";
            if (specified)
            {
                summary = "Empty (default group)";
                if (selected.arraySize == 1)
                {
                    var type = Type.GetType(selected.GetArrayElementAtIndex(0).stringValue);
                    summary = type != null ? DisplayName(type) : "Missing component type";
                }
                else if (selected.arraySize > 1)
                    summary = $"Mixed ({selected.arraySize} components)";
            }
            if (EditorGUI.DropdownButton(field, new GUIContent(summary), FocusType.Keyboard))
            {
                property.serializedObject.ApplyModifiedProperties();
                var types = new List<Type>();
                var owner = property.serializedObject.targetObject;
                if (owner is EntityView view)
                    view.CollectGroupMaskTypes(types);
                else if (owner is EntityPreset preset)
                    preset.CollectGroupMaskTypes(types);
                types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
                PopupWindow.Show(field, new MaskPopup(owner, property.propertyPath, types, field.width));
            }
            EditorGUI.EndProperty();
        }

        private static string DisplayName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;
            var name = type.Name.Split('`')[0] + "<";
            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
                name += (i == 0 ? "" : ", ") + DisplayName(arguments[i]);
            return name + ">";
        }

        private sealed class MaskPopup : PopupWindowContent
        {
            private readonly UnityEngine.Object _owner;
            private readonly string _path;
            private readonly List<Type> _types;
            private readonly float _width;
            private string _search = "";
            private Vector2 _scroll;

            public MaskPopup(UnityEngine.Object owner, string path, List<Type> types, float width)
            {
                _owner = owner;
                _path = path;
                _types = types;
                _width = Mathf.Clamp(width, 280, 600);
            }

            public override Vector2 GetWindowSize() =>
                new(_width, Mathf.Min(360, 110 + _types.Count * 20));

            public override void OnGUI(Rect rect)
            {
                if (_owner == null)
                {
                    editorWindow.Close();
                    return;
                }
                using var so = new SerializedObject(_owner);
                so.Update();
                var property = so.FindProperty(_path);
                var specified = property.FindPropertyRelative("_specified");
                var selected = property.FindPropertyRelative("_componentTypes");
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (GUILayout.Toggle(!specified.boolValue, "None (use initial components)", EditorStyles.toolbarButton)
                    && specified.boolValue)
                {
                    specified.boolValue = false;
                    selected.ClearArray();
                }
                if (GUILayout.Toggle(specified.boolValue && selected.arraySize == 0,
                    "Empty (default group)", EditorStyles.toolbarButton)
                    && (!specified.boolValue || selected.arraySize != 0))
                {
                    specified.boolValue = true;
                    selected.ClearArray();
                }
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                var matches = 0;
                for (var i = 0; i < _types.Count; i++)
                {
                    var type = _types[i];
                    var name = type.FullName;
                    if (name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    matches++;
                    var key = type.AssemblyQualifiedName;
                    var index = Find(selected, key);
                    var enabled = specified.boolValue && index >= 0;
                    var toggled = EditorGUILayout.ToggleLeft(new GUIContent(DisplayName(type), name), enabled);
                    if (toggled == enabled)
                        continue;
                    specified.boolValue = true;
                    if (toggled)
                    {
                        var next = selected.arraySize++;
                        selected.GetArrayElementAtIndex(next).stringValue = key;
                    }
                    else
                        selected.DeleteArrayElementAtIndex(index);
                }
                if (matches == 0)
                    EditorGUILayout.LabelField("No matching components.", EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
                so.ApplyModifiedProperties();
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    editorWindow.Close();
            }

            private static int Find(SerializedProperty selected, string key)
            {
                for (var i = 0; i < selected.arraySize; i++)
                    if (selected.GetArrayElementAtIndex(i).stringValue == key)
                        return i;
                return -1;
            }
        }
    }
}
