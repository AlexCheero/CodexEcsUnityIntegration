using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class SystemAttribute : Attribute
{
    public ESystemCategory[] Categories;
    public SystemAttribute(params ESystemCategory[] categories) => Categories = categories;
}

public class HiddenInspector : Attribute { }
public class DefaultValue : Attribute
{
    public object Value;
    public DefaultValue(object value) => Value = value;
}

public enum ESystemCategory
{
    Init,
    Update,
    LateUpdate,
    FixedUpdate,
    LateFixedUpdate,
}

public enum EGatheredTypeCategory
{
    EcsComponent,
    UnityComponent,
    System
}

public static class IntegrationHelper
{
    private const string Components = "Components";
    private const string Tags = "Tags";
    private const string UnityComponents = "UnityComponents";

    //TODO: unify with typenames from inspectors
    private static Type[] EcsComponentTypes;
    private static Type[] UnityComponentTypes;
    private static Type[] SystemTypes;

    public static string[] ComponentTypeNames;
    public static string[] TagTypeNames;

    public static readonly Type[] AllowedFiledRefTypes = { typeof(EntityPreset), typeof(string) };

    static IntegrationHelper()
    {
        EcsComponentTypes = typeof(EntityView).Assembly.GetTypes()
            .Where((t) => t.Namespace == Components || t.Namespace == Tags).ToArray();

        //TODO: could cause troubles with nested assemlies
        var types = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            types.AddRange(assembly.GetTypes().Where((t) => typeof(Component).IsAssignableFrom(t)));
        UnityComponentTypes = types.ToArray();

        SystemTypes = typeof(ECSPipeline).Assembly.GetTypes()
            //TODO: duplicated from Pipeline_Inspector move to helper class
            .Where((type) => type != typeof(EcsSystem) && typeof(EcsSystem).IsAssignableFrom(type)).ToArray();

        ComponentTypeNames = GetTypeNames<EntityView>((t) => t.Namespace == Components);
        TagTypeNames = GetTypeNames<EntityView>((t) => t.Namespace == Tags);
    }

    public static Type GetTypeByName(string systemName, EGatheredTypeCategory category)
    {
        Type[] types;
        switch (category)
        {
            case EGatheredTypeCategory.EcsComponent:
                types = EcsComponentTypes;
                break;
            case EGatheredTypeCategory.UnityComponent:
                types = UnityComponentTypes;
                break;
            case EGatheredTypeCategory.System:
                types = SystemTypes;
                break;
            default:
                return null;
        }

        foreach (var compType in types)
        {
            if (compType.FullName == systemName)
                return compType;
        }

        return null;
    }

    public static bool IsUnityComponent(Type type) => typeof(Component).IsAssignableFrom(type);

    private static readonly object[] AddParams = { null, null };
    public static int InitAsEntity(EcsWorld world, in EntityMeta data)
    {
        var entityId = world.Create();

        MethodInfo addMethodInfo = typeof(EcsWorld).GetMethod("Add");

        foreach (var meta in data.Metas)
        {
            Type compType;
            object componentObj;
            if (meta.UnityComponent != null)
            {
                compType = meta.UnityComponent.GetType();
                componentObj = meta.UnityComponent;
            }
            else
            {
                compType = GetTypeByName(meta.ComponentName, EGatheredTypeCategory.EcsComponent);
#if DEBUG
                if (compType == null)
                    throw new Exception("can't find component type " + meta.ComponentName);
#endif
                componentObj = Activator.CreateInstance(compType);

                foreach (var field in meta.Fields)
                {
                    var fieldInfo = compType.GetField(field.Name);
                    var defaultValueAttribute = fieldInfo.GetCustomAttribute<DefaultValue>();
                    object defaultValue = defaultValueAttribute?.Value;
                    var value = field.IsHiddenInEditor ? defaultValue : field.GetValue();
                    if (value == null)
                        continue;

                    fieldInfo.SetValue(componentObj, value);
                }
            }
            AddParams[0] = entityId;
            AddParams[1] = componentObj;

            MethodInfo genAddMethodInfo = addMethodInfo.MakeGenericMethod(compType);
            genAddMethodInfo.Invoke(world, AddParams);
        }

        return entityId;
    }

#if UNITY_EDITOR
    private static string[] GetTypeNames<SameAssemblyType>(Func<Type, bool> predicate)
    {
        var types = Assembly.GetAssembly(typeof(SameAssemblyType)).GetTypes().Where(predicate).ToArray();
        return Array.ConvertAll(types, (t) => t.FullName);
    }

    public static bool IsSearchMatch(string searchString, string name)
    {
        if (searchString == null || searchString.Length == 0)
            return true;
        return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string GetTypeUIName(string fullName) => fullName.Substring(fullName.LastIndexOf('.') + 1);

    public static void DrawAddComponents(ref bool addListExpanded, string addSearch, in EntityMeta data,
        UnityEngine.Object target, List<List<string>> viewComponentTypeNames = null, List<bool> viewComponentFoldouts = null)
    {
        var listText = addListExpanded ? "Shrink components list" : "Expand components list";
        if (GUILayout.Button(new GUIContent(listText), GUILayout.ExpandWidth(false)))
            addListExpanded = !addListExpanded;
        if (addListExpanded)
        {
            addSearch = EditorGUILayout.TextField(addSearch);
            EditorGUILayout.BeginVertical();
            DrawAddList(Components, ComponentTypeNames, OnAddComponent, addSearch, data, target);
            GUILayout.Space(10);
            DrawAddList(Tags, TagTypeNames, OnAddComponent, addSearch, data, target);
            GUILayout.Space(10);
            if (viewComponentTypeNames != null && viewComponentTypeNames.Count > 0)
            {
                DrawAddUnityComponentList(UnityComponents, viewComponentTypeNames, viewComponentFoldouts, OnAddComponent, addSearch, data, target);
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private static void OnAddComponent(string componentName, EntityMeta data, UnityEngine.Object target)
    {
        var type = GetTypeByName(componentName, EGatheredTypeCategory.UnityComponent);
        if (IsUnityComponent(type))
        {
            MethodInfo getComponentInfo = typeof(EntityView).GetMethod("GetComponent", new Type[] { }).MakeGenericMethod(type);
            var component = (Component)getComponentInfo.Invoke(target, null);
            if (data.AddUnityComponent(component, type))
                EditorUtility.SetDirty(target);
        }
        else
        {
            if (data.AddComponent(componentName))
                EditorUtility.SetDirty(target);
        }
    }

    private static void DrawAddUnityComponentList(string label, List<List<string>> components, List<bool> foldouts,
        Action<string, EntityMeta, UnityEngine.Object> onAdd, string search, in EntityMeta data, UnityEngine.Object target)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        for (int i = 0; i < components.Count; i++)
        {
            var compSubTypes = components[i];
            if (compSubTypes.Count == 0)
                continue;

            EditorGUILayout.BeginHorizontal();

            foldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(foldouts[i], GetTypeUIName(compSubTypes[0]));
            bool canAddFirstComponent = IsSearchMatch(search, compSubTypes[0]) &&
                !IsComponentAlreadyAdded(compSubTypes[0], data);
            if (canAddFirstComponent && GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                onAdd(compSubTypes[0], data, target);

            EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                for (int j = 1; j < compSubTypes.Count; j++)
                {
                    var componentName = compSubTypes[j];
                    if (!IsSearchMatch(search, componentName) ||
                        IsComponentAlreadyAdded(componentName, data))
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();

                    //TODO: add lines between components for readability
                    //      or remove "+" button and make buttons with component names on it
                    EditorGUILayout.LabelField("add as " + GetTypeUIName(componentName));
                    if (GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                        onAdd(componentName, data, target);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

    private static void DrawAddList(string label, string[] components, Action<string, EntityMeta, UnityEngine.Object> onAdd, string search,
        in EntityMeta data, UnityEngine.Object target)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var componentName in components)
        {
            if (!IsSearchMatch(search, componentName) || IsComponentAlreadyAdded(componentName, data))
                continue;

            EditorGUILayout.BeginHorizontal();

            //TODO: add lines between components for readability
            //      or remove "+" button and make buttons with component names on it
            EditorGUILayout.LabelField(GetTypeUIName(componentName));
            if (GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                onAdd(componentName, data, target);

            EditorGUILayout.EndHorizontal();
        }
    }

    private static bool IsComponentAlreadyAdded(string component, in EntityMeta data)
    {
        var metas = data.Metas;
        for (int i = 0; i < metas.Length; i++)
        {
            if (component == metas[i].ComponentName)
                return true;
        }
        return false;
    }

    public static void DrawComponents(in EntityMeta data, string search, UnityEngine.Object target)
    {
        var metas = data.Metas;
        for (int i = 0; i < metas.Length; i++)
        {
            if (!IsSearchMatch(search, metas[i].ComponentName))
                continue;

            EditorGUILayout.BeginHorizontal();

            DrawComponent(ref metas[i], target);

            //TODO: delete button moves outside of the screen when foldout is expanded
            //component delete button
            if (GUILayout.Button(new GUIContent("-"), GUILayout.ExpandWidth(false)))
            {
                data.RemoveMetaAt(i);
                i--;
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private static void DrawComponent(ref ComponentMeta meta, UnityEngine.Object target)
    {
        EditorGUILayout.BeginVertical();
        {
            //TODO: draw tags without arrow
            meta.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(meta.IsExpanded, GetTypeUIName(meta.ComponentName));
            if (meta.IsExpanded && meta.Fields != null)
            {
                for (int i = 0; i < meta.Fields.Length; i++)
                {
                    if (!meta.Fields[i].IsHiddenInEditor)
                        DrawField(ref meta.Fields[i], target);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        EditorGUILayout.EndVertical();
    }

    private static void DrawField(ref ComponentFieldMeta fieldMeta, UnityEngine.Object target)
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField(fieldMeta.Name);
            var valueObject = fieldMeta.GetValue();

            bool setDirty;
            if (fieldMeta.TypeName == typeof(int).FullName)
            {
                var intValue = valueObject != null ? (int)valueObject : default(int);
                setDirty = fieldMeta.SetValue(EditorGUILayout.IntField(intValue));
            }
            else if (fieldMeta.TypeName == typeof(float).FullName)
            {
                var floatValue = valueObject != null ? (float)valueObject : default(float);
                setDirty = fieldMeta.SetValue(EditorGUILayout.FloatField(floatValue));
            }
            else if (fieldMeta.TypeName == typeof(Vector3).FullName)
            {
                var vec3Value = valueObject != null ? (Vector3)valueObject : default(Vector3);
                setDirty = fieldMeta.SetValue(EditorGUILayout.Vector3Field("", vec3Value));
            }
            else if (nameof(EntityPreset) == fieldMeta.TypeName)
            {
                var obj = valueObject != null ? (EntityPreset)valueObject : null;
                setDirty = fieldMeta.SetValue(EditorGUILayout.ObjectField("", obj, typeof(EntityPreset), true));
            }
            else if (fieldMeta.TypeName == typeof(string).FullName)
            {
                var str = valueObject as string;
                setDirty = fieldMeta.SetValue(EditorGUILayout.TextField(str));
            }
            else
            {
                var type = GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.UnityComponent);
                var obj = valueObject != null ? (Component)valueObject : null;
                setDirty = fieldMeta.SetValue(EditorGUILayout.ObjectField("", obj, type, true));
            }

            if (setDirty)
                EditorUtility.SetDirty(target);
        }
        EditorGUILayout.EndHorizontal();
    }
#endif
}
