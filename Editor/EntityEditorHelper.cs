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

        private static readonly List<(string TypeName, int Index, Type ComponentType, SerializedProperty ComponentProp)> _offlineBuffer = new();
        private static readonly Dictionary<string, bool> _offlineFoldouts = new();
        private static readonly Dictionary<Type, bool> _hasSerializableFieldsCache = new();

        public static void DrawComponentsInspector(SerializedProperty componentsProp, IReadOnlyList<ComponentWrapper> addedComponents)
        {
            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _offlineBuffer.Clear();
                _componentFilter = EditorGUILayout.TextField("Search", _componentFilter);
                EditorGUILayout.Space();

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
                    _offlineBuffer.Add((typeName, i, componentType, componentProp));
                }

                _offlineBuffer.Sort((p1, p2) =>
                    string.Compare(p1.TypeName, p2.TypeName, StringComparison.Ordinal));

                DrawFoldExpandButtons(
                    () => SetAllOfflineFoldouts(true),
                    () => SetAllOfflineFoldouts(false));
                
                EditorGUI.indentLevel++;
                for (int i = 0; i < _offlineBuffer.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    var (typeName, j, componentType, componentProp) = _offlineBuffer[i];
                    // Never PropertyField the whole _component: non-serializable fields (HashSet,
                    // Dictionary, …) under SerializeReference spam managed-reference errors.
                    // Draw only Unity-serializable children, or a label for tag-like components.
                    if (componentProp != null && HasUnitySerializableFields(componentType))
                    {
                        EditorGUILayout.BeginVertical();

                        if (!_offlineFoldouts.TryGetValue(typeName, out var expanded))
                            expanded = true;
                        expanded = EditorGUILayout.Foldout(expanded, typeName, true);
                        _offlineFoldouts[typeName] = expanded;

                        if (expanded)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUI.BeginChangeCheck();
                            DrawChildren(componentProp);
                            if (EditorGUI.EndChangeCheck())
                            {
                                var initMethod = componentType.GetMethod(
                                    "Init",
                                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                                );
                                if (initMethod != null)
                                {
                                    object[] args = { componentProp.boxedValue };
                                    initMethod.Invoke(null, args);
                                    componentProp.boxedValue = args[0];
                                }
                            }
                            EditorGUI.indentLevel--;
                        }

                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.LabelField(typeName);
                    }
                    
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        componentsProp.DeleteArrayElementAtIndex(j);
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
                    if (addedComponents != null && addedComponents.Any(c => c.GetComponentType() == addableComponentType))
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
                    if (_onlineBuffer.Any(c => c == addableComponentType))
                        continue;
                    
                    EditorGUILayout.BeginHorizontal("box");

                    EditorGUILayout.LabelField(addableComponentType.Name);

                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        var methodDefinition = typeof(EntityView)
                            .GetMethod(nameof(EntityView.AddInspector), BindingFlags.Public | BindingFlags.Instance)
                            ?.MakeGenericMethod(componentTypes[i]);

                        if (methodDefinition != null)
                            methodDefinition.Invoke(view, null);
                    }

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

            DrawFoldExpandButtons(
                () => SetAllRuntimeFoldouts(true),
                () => SetAllRuntimeFoldouts(false));

            EditorGUI.indentLevel++;
            for (int i = 0; i < _onlineBuffer.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.BeginVertical();
                
                DrawRuntimeComponent(world, entityId, _onlineBuffer[i]);
                
                EditorGUILayout.EndVertical();
                
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    var methodDefinition = typeof(EntityView)
                        .GetMethod(nameof(EntityView.Remove), BindingFlags.Public | BindingFlags.Instance)
                        ?.MakeGenericMethod(_onlineBuffer[i]);

                    if (methodDefinition != null)
                        methodDefinition.Invoke(view, null);

                    _onlineBuffer.RemoveAt(i);
                    
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
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
                    EditorGUI.indentLevel++;
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
                    
                    EditorGUI.indentLevel--;
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

        private static void DrawFoldExpandButtons(Action expandAll, Action foldAll)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Expand All"))
                expandAll();
            if (GUILayout.Button("Fold All"))
                foldAll();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private static void SetAllOfflineFoldouts(bool expanded)
        {
            for (int i = 0; i < _offlineBuffer.Count; i++)
            {
                var (typeName, _, componentType, componentProp) = _offlineBuffer[i];
                if (componentProp != null && HasUnitySerializableFields(componentType))
                    _offlineFoldouts[typeName] = expanded;
            }
        }

        private static void SetAllRuntimeFoldouts(bool expanded)
        {
            for (int i = 0; i < _onlineBuffer.Count; i++)
            {
                var componentType = _onlineBuffer[i];
                if (!_proxies.TryGetValue(componentType, out var proxy))
                {
                    proxy = ScriptableObject.CreateInstance<RuntimeComponentProxy>();
                    proxy.hideFlags = HideFlags.DontSave;
                    _proxies[componentType] = proxy;
                    _serializedProxies[componentType] = new SerializedObject(proxy);
                }

                var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
                if (proxy.Value == null || proxy.Value.GetType() != wrapperType)
                    proxy.Value = (ComponentWrapper)Activator.CreateInstance(wrapperType);

                proxy.Value.IsExpanded = expanded;
            }
        }

        private static bool HasUnitySerializableFields(Type type)
        {
            if (type == null)
                return false;

            if (_hasSerializableFieldsCache.TryGetValue(type, out var cached))
                return cached;

            var hasSerializable = false;
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (IsUnitySerializableField(fields[i]))
                {
                    hasSerializable = true;
                    break;
                }
            }

            _hasSerializableFieldsCache[type] = hasSerializable;
            return hasSerializable;
        }

        private static bool IsUnitySerializableField(FieldInfo field)
        {
            if (field.IsNotSerialized || field.GetCustomAttribute<NonSerializedAttribute>() != null)
                return false;
            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null
                && field.GetCustomAttribute<SerializeReference>() == null)
                return false;
            if (field.GetCustomAttribute<SerializeReference>() != null)
                return true;
            return IsUnitySerializableType(field.FieldType);
        }

        private static bool IsUnitySerializableType(Type type)
        {
            if (type == null || type == typeof(object))
                return false;
            if (typeof(Object).IsAssignableFrom(type))
                return true;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
                return true;
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
                || type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Color32)
                || type == typeof(Rect) || type == typeof(Bounds) || type == typeof(Matrix4x4)
                || type == typeof(AnimationCurve) || type == typeof(Gradient)
                || type == typeof(LayerMask) || type == typeof(Vector2Int) || type == typeof(Vector3Int)
                || type == typeof(RectInt) || type == typeof(BoundsInt))
                return true;

            if (type.IsArray)
                return type.GetArrayRank() == 1 && IsUnitySerializableType(type.GetElementType());

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>))
                    return IsUnitySerializableType(type.GetGenericArguments()[0]);
                if (definition == typeof(HashSet<>) || definition == typeof(Dictionary<,>)
                    || definition == typeof(Queue<>) || definition == typeof(Stack<>))
                    return false;
                return type.IsDefined(typeof(SerializableAttribute), false);
            }

            if (type.IsInterface)
                return false;

            return type.IsValueType || type.IsDefined(typeof(SerializableAttribute), false);
        }
    }
}