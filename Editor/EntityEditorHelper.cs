using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    public static class EntityEditorHelper
    {
        private static bool _addListExpanded;
        private static bool _showComponents;
        private static string _addFilter;
        private static string _componentFilter;
        private static GUIContent _componentGUIContent = new();
        
        public static void DrawComponentsInspector(SerializedProperty componentsProp, IReadOnlyList<ComponentWrapper> addedComponents)
        {
            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _componentFilter = EditorGUILayout.TextField("Search", _componentFilter);
                EditorGUILayout.Space();
                
                EditorGUI.indentLevel++;

                for (int i = 0; i < componentsProp.arraySize; i++)
                {
                    var element = componentsProp.GetArrayElementAtIndex(i);
                    var obj = element.managedReferenceValue;

                    if (obj == null)
                        continue;

                    var type = obj.GetType();
                    while (type != null && !type.IsGenericType)
                        type = type.BaseType;
                    if (type == null)
                    {
                        Debug.LogError("Can't find generic component wrapper base type");
                        continue;
                    }
                    
                    var componentType = type.GetGenericArguments()[0];
                    var typeName = componentType.Name;

                    if (!string.IsNullOrEmpty(_componentFilter) &&
                        !typeName.Contains(_componentFilter, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    EditorGUILayout.BeginHorizontal();

                    var componentProp = element.FindPropertyRelative(ComponentWrapper.ComponentPropertyName);
                    if (componentProp != null)
                    {
                        _componentGUIContent.text = typeName;
                        EditorGUILayout.PropertyField(componentProp, _componentGUIContent, true);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(typeName);
                    }

                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        componentsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button(_addListExpanded ? "Fold" : "Add Component"))
                _addListExpanded = !_addListExpanded;
            if (_addListExpanded)
            {
                _addFilter = EditorGUILayout.TextField("Search", _addFilter);
                EditorGUILayout.Space();
                
                EditorGUI.indentLevel++;

                var componentTypes = IntegrationHelper.ComponentTypes;
                for (int i = 0; i < componentTypes.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_addFilter)
                        && !componentTypes[i].Name
                            .Contains(_addFilter, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                    
                    var addableComponentType = componentTypes[i];
                    if (addedComponents != null && addedComponents.Any(c => c.GetType() == addableComponentType))
                        continue;
                    
                    EditorGUILayout.BeginHorizontal("box");

                    
                    EditorGUILayout.LabelField(addableComponentType.Name);

                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        var index = componentsProp.arraySize;
                        componentsProp.InsertArrayElementAtIndex(index);

                        var element = componentsProp.GetArrayElementAtIndex(index);
                        var defaultValueGetter = addableComponentType.GetProperty("Default",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        var viewType = typeof(ComponentWrapper<>).MakeGenericType(addableComponentType);
                        var wrapper = (ComponentWrapper)Activator.CreateInstance(viewType);
                        if (defaultValueGetter != null)
                            wrapper.InitFromComponent((IComponent)defaultValueGetter.GetValue(null));
                        element.managedReferenceValue = wrapper;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }
        }
    }
}