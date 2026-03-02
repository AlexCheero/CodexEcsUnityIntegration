using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    public static class EntityEditorHelper
    {
        private static bool _addListExpanded;
        private static bool _showComponents;
        private static string _addFilter;
        private static string _componentFilter;
        private static GUIContent _componentGUIContent = new();
        
        private static readonly Dictionary<Type, RuntimeComponentProxy> _proxies = new();
        private static readonly Dictionary<Type, SerializedObject> _serializedProxies = new();

        public static void CleanProxiesCache()
        {
            foreach (var proxy in _proxies.Values)
            {
                if (proxy != null)
                    Object.DestroyImmediate(proxy);
            }

            _proxies.Clear();
            _serializedProxies.Clear();
        }

        private static readonly List<(string, SerializedProperty)> _offlineBuffer = new();
        public static void DrawComponentsInspector(SerializedProperty componentsProp, IReadOnlyList<ComponentWrapper> addedComponents)
        {
            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _offlineBuffer.Clear();
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
                    
                    var componentProp = element.FindPropertyRelative(ComponentWrapper.ComponentPropertyName);
                    _offlineBuffer.Add((typeName, componentProp));
                }

                _offlineBuffer.Sort((p1, p2) =>
                    string.Compare(p1.Item1, p2.Item1, StringComparison.Ordinal));
                for (int i = 0; i < _offlineBuffer.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    var (typeName, componentProp) = _offlineBuffer[i];
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

        public static void DrawRuntimeInspector(EntityView view)
        {
            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _componentFilter = EditorGUILayout.TextField("Search", _componentFilter);
                EditorGUILayout.Space();
                DrawRuntimeComponents(view);
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
                    // if (addedComponents != null && addedComponents.Any(c => c.GetType() == addableComponentType))
                    //     continue;
                    
                    EditorGUILayout.BeginHorizontal("box");

                    EditorGUILayout.LabelField(addableComponentType.Name);

                    // if (GUILayout.Button("+", GUILayout.Width(25)))
                    // {
                    //     var index = componentsProp.arraySize;
                    //     componentsProp.InsertArrayElementAtIndex(index);
                    //
                    //     var element = componentsProp.GetArrayElementAtIndex(index);
                    //     var defaultValueGetter = addableComponentType.GetProperty("Default",
                    //         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    //     var viewType = typeof(ComponentWrapper<>).MakeGenericType(addableComponentType);
                    //     var wrapper = (ComponentWrapper)Activator.CreateInstance(viewType);
                    //     if (defaultValueGetter != null)
                    //         wrapper.InitFromComponent((IComponent)defaultValueGetter.GetValue(null));
                    //     element.managedReferenceValue = wrapper;
                    // }

                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private static List<Type> _onlineBuffer = new();
        private static void DrawRuntimeComponents(EntityView view)
        {
            var world = view.World;
            var entityId = view.Id;

            _onlineBuffer.Clear();
            
            EditorGUI.indentLevel++;
            foreach (var componentId in view.GetMask())
            {
                var componentType = ComponentMapping.GetTypeForId(componentId);
                if (!string.IsNullOrEmpty(_componentFilter) &&
                    !componentType.Name.Contains(_componentFilter, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (typeof(IComponent).IsAssignableFrom(componentType))
                    _onlineBuffer.Add(ComponentMapping.GetTypeForId(componentId));
            }
            
            _onlineBuffer.Sort((t1, t2) => string.Compare(t1.Name, t2.Name, StringComparison.Ordinal));
            for (int i = 0; i < _onlineBuffer.Count; i++)
                DrawRuntimeComponent(world, entityId, _onlineBuffer[i]);
            
            EditorGUI.indentLevel--;
        }

        private static void DrawRuntimeComponent(EcsWorld world, int entityId, Type componentType)
        {
            if (!_proxies.TryGetValue(componentType, out var proxy))
            {
                proxy = ScriptableObject.CreateInstance<RuntimeComponentProxy>();
                proxy.hideFlags = HideFlags.DontSave;

                _proxies[componentType] = proxy;
                _serializedProxies[componentType] = new SerializedObject(proxy);
            }

            //TODO: cache
            var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
            if (proxy.Value == null || proxy.Value.GetType() != wrapperType)
            {
                proxy.Value = (ComponentWrapper)Activator.CreateInstance(wrapperType);
            }

            // ===== READ FROM WORLD =====
            proxy.Value.ReadFromWorld(world, entityId);

            var so = _serializedProxies[componentType];
            so.Update();

            EditorGUILayout.Space();
            
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var isTag = fields.Length == 0 && componentType.IsValueType && !componentType.IsEnum;
            if (isTag)
            {
                EditorGUILayout.LabelField(componentType.Name);
            }
            else
            {
                proxy.Value.IsExpanded = EditorGUILayout.Foldout(proxy.Value.IsExpanded, componentType.Name, true);
                if (proxy.Value.IsExpanded)
                {
                    EditorGUI.BeginChangeCheck();

                    // Находим поле Value (wrapper)
                    var wrapperProp = so.FindProperty("Value");

                    // А внутри него — реальное поле компонента
                    var componentProp = wrapperProp.FindPropertyRelative("_component");

                    // Рисуем ТОЛЬКО поля компонента
                    if (componentProp != null)
                        DrawChildren(componentProp);

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        proxy.Value.WriteToWorld(world, entityId);
                    }
                }
            }
        }

        private static void DrawChildren(SerializedProperty property)
        {
            var copy = property.Copy();
            var end = copy.GetEndProperty();

            copy.NextVisible(true);

            while (!SerializedProperty.EqualContents(copy, end))
            {
                EditorGUILayout.PropertyField(copy, true);
                copy.NextVisible(false);
            }
        }
    }
}