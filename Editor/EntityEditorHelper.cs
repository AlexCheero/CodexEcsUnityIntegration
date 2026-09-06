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
        private static readonly GUIContent ComponentMenuContent = new("\u22ee", "Component actions");
        
        private static readonly List<(string TypeName, int Index, Type ComponentType, SerializedProperty ComponentProp)> _offlineBuffer = new();
        private static readonly Dictionary<string, bool> _offlineFoldouts = new();
        private static readonly Dictionary<Type, bool> _hasSerializableFieldsCache = new();

    public static void DrawComponentsInspector(
        SerializedProperty componentsProp,
        IReadOnlyList<ComponentWrapper> addedComponents,
        Object owner)
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
                        DrawComponentFields(componentProp, componentType);
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

                if (GUILayout.Button(ComponentMenuContent, GUILayout.Width(22)))
                {
                    var canPasteAsNew = EcsComponentClipboard.HasComponent &&
                                        !EcsComponentClipboard.ContainsComponentType(
                                            componentsProp,
                                            addedComponents,
                                            EcsComponentClipboard.ComponentType);
                    ShowOfflineComponentMenu(owner, componentType, canPasteAsNew);
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

        if (EcsComponentClipboard.HasComponent)
        {
            var copiedType = EcsComponentClipboard.ComponentType;
            var alreadyPresent = EcsComponentClipboard.ContainsComponentType(
                componentsProp,
                addedComponents,
                copiedType);
            using (new EditorGUI.DisabledScope(alreadyPresent))
            {
                if (GUILayout.Button($"Paste {copiedType.Name} As New"))
                {
                    EcsComponentClipboard.TryPasteComponentAsNew(
                        componentsProp,
                        addedComponents,
                        owner);
                }
            }
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
                if (addedComponents != null &&
                    addedComponents.Any(c => c != null && c.GetComponentType() == addableComponentType))
                    continue;
                
                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.LabelField(addableComponentType.Name);

                if (GUILayout.Button("+", GUILayout.Width(25)))
                    EcsComponentInspectorUtility.TryAddSerializedComponents(
                        componentsProp,
                        addedComponents,
                        owner,
                        addableComponentType);

                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
        }
    }

        private static void ShowOfflineComponentMenu(Object owner, Type componentType, bool canPasteAsNew)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Copy Component"),
                false,
                () => EcsComponentClipboard.CopyComponent(owner, componentType));

            if (EcsComponentClipboard.HasComponent &&
                EcsComponentClipboard.ComponentType == componentType)
            {
                menu.AddItem(
                    new GUIContent("Paste Component Values"),
                    false,
                    () => EcsComponentClipboard.PasteComponentValues(owner, componentType));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste Component Values"));
            }

            if (canPasteAsNew)
            {
                menu.AddItem(
                    new GUIContent("Paste Component As New"),
                    false,
                    () => EcsComponentClipboard.PasteComponentAsNew(owner));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste Component As New"));
            }

            menu.ShowAsContext();
        }

        private static readonly Dictionary<Type, bool> _hasCustomDrawerCache = new();

        /// <summary>
        /// Uses a component [CustomPropertyDrawer] when one exists. Otherwise draws
        /// visible children only — PropertyField on the whole _component can error on
        /// non-serializable fields (HashSet, Dictionary, …) under SerializeReference.
        /// </summary>
        internal static void DrawComponentFields(SerializedProperty componentProp, Type componentType)
        {
            if (HasCustomPropertyDrawer(componentType))
                EditorGUILayout.PropertyField(componentProp, GUIContent.none, true);
            else
                DrawChildren(componentProp);
        }

        private static bool HasCustomPropertyDrawer(Type componentType)
        {
            if (componentType == null)
                return false;

            if (_hasCustomDrawerCache.TryGetValue(componentType, out var cached))
                return cached;

            var hasDrawer = false;
            var typeField = typeof(CustomPropertyDrawer).GetField(
                "m_Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var useForChildrenField = typeof(CustomPropertyDrawer).GetField(
                "m_UseForChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (typeField != null)
            {
                foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
                {
                    var attrs = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), false);
                    for (int i = 0; i < attrs.Length; i++)
                    {
                        var drawnType = typeField.GetValue(attrs[i]) as Type;
                        if (drawnType == null)
                            continue;

                        if (drawnType == componentType)
                        {
                            hasDrawer = true;
                            break;
                        }

                        var useForChildren = useForChildrenField != null &&
                                             (bool)useForChildrenField.GetValue(attrs[i]);
                        if (useForChildren && componentType.IsSubclassOf(drawnType))
                        {
                            hasDrawer = true;
                            break;
                        }
                    }

                    if (hasDrawer)
                        break;
                }
            }

            _hasCustomDrawerCache[componentType] = hasDrawer;
            return hasDrawer;
        }

        private static void DrawChildren(SerializedProperty property)
        {
            using var copy = property.Copy();
            using var end = copy.GetEndProperty();
            var hasNext = copy.NextVisible(true);
            while (hasNext && !SerializedProperty.EqualContents(copy, end))
            {
                EditorGUILayout.PropertyField(copy, true);
                hasNext = copy.NextVisible(false);
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

        internal static bool HasUnitySerializableFields(Type type)
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

        internal static bool IsUnitySerializableField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized ||
                field.GetCustomAttribute<NonSerializedAttribute>() != null)
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
