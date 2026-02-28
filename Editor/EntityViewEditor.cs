using System;
using System.Linq;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(EntityView))]
    public class EntityViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        private SerializedProperty _forceInitProp;
        private SerializedProperty _updateInspectorProp;

        private bool _addListExpanded;
        private bool _showComponents;
        private string _addFilter;
        private string _componentFilter;
        
        private void OnEnable()
        {
            _componentsProp = serializedObject.FindProperty(EntityView.ComponentsPropertyName);
            _forceInitProp = serializedObject.FindProperty(EntityView.ForceInitPropertyName);
            _updateInspectorProp = serializedObject.FindProperty(EntityView.UpdateInspectorPropertyName);
        }

        public override void OnInspectorGUI()
        {
            var view = (EntityView)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(_forceInitProp);
            EditorGUILayout.PropertyField(_updateInspectorProp);
            
            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _componentFilter = EditorGUILayout.TextField("Search", _componentFilter);
                EditorGUILayout.Space();
                
                EditorGUI.indentLevel++;

                for (int i = 0; i < _componentsProp.arraySize; i++)
                {
                    var element = _componentsProp.GetArrayElementAtIndex(i);
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

                    EditorGUILayout.PropertyField(
                        componentProp,
                        new GUIContent(typeName),
                        true
                    );

                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        _componentsProp.DeleteArrayElementAtIndex(i);
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
                    if (view.Components.Any(c => c.GetType() == addableComponentType))
                        continue;
                    
                    EditorGUILayout.BeginHorizontal("box");

                    
                    EditorGUILayout.LabelField(addableComponentType.Name);

                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        var index = _componentsProp.arraySize;
                        _componentsProp.InsertArrayElementAtIndex(index);

                        var element = _componentsProp.GetArrayElementAtIndex(index);
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}
