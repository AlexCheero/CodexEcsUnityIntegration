using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
    OnEnable,
    OnDisable
}

public enum EGatheredTypeCategory
{
    EcsComponent,
    UnityObject,
    System,
    Enum
}

public struct EntityInspectorCommonData
{
    public bool AddListExpanded;
    public string AddSearch;
    public string AddedSearch;
}

public struct EntityViewComponentsData
{
    public List<List<string>> ViewComponentTypeNames;
    public List<bool> ViewComponentFoldouts;

    public EntityViewComponentsData(int length)
    {
        ViewComponentTypeNames = new List<List<string>>(length);
        ViewComponentFoldouts = new List<bool>(length);
    }
}

public static class IntegrationHelper
{
    private const string Components = "Components";
    private const string Tags = "Tags";
    private const string UnityComponents = "UnityComponents";

    //TODO: unify with typenames from inspectors
    //TODO: why not use list?
    private static Type[] EcsComponentTypes;
    private static Type[] UnityObjectTypes;
    private static Type[] SystemTypes;
    private static Type[] EnumTypes;

    public static string[] ComponentTypeNames;
    public static string[] TagTypeNames;

    static IntegrationHelper()
    {
        EcsComponentTypes = typeof(EntityView).Assembly.GetTypes()
            .Where((t) => t.Namespace == Components || t.Namespace == Tags).ToArray();

        //TODO: could cause troubles with nested assemlies
        var types = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            types.AddRange(assembly.GetTypes().Where((t) => typeof(Object).IsAssignableFrom(t)));
        UnityObjectTypes = types.ToArray();
        
        var enumTypes = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            enumTypes.AddRange(assembly.GetTypes().Where((t) => t.IsEnum));
        EnumTypes = enumTypes.ToArray();

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
            case EGatheredTypeCategory.UnityObject:
                types = UnityObjectTypes;
                break;
            case EGatheredTypeCategory.System:
                types = SystemTypes;
                break;
            case EGatheredTypeCategory.Enum:
                types = EnumTypes;
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

    public static bool IsUnityObject(Type type) => typeof(Object).IsAssignableFrom(type);

    private static readonly object[] AddParams = { null, null };
#if DEBUG
    public static int InitAsEntity(EcsWorld world, in EntityMeta data, Object obj)
#else
    public static int InitAsEntity(EcsWorld world, in EntityMeta data)
#endif
    {
        var entityId = world.Create();

        MethodInfo addMethodInfo = typeof(EcsWorld).GetMethod("Add");

        foreach (var meta in data.Metas)
        {
            Type compType;
            object componentObj;
            if (meta.UnityObject != null)
            {
                compType = GetTypeByName(meta.ComponentName, EGatheredTypeCategory.UnityObject);
                componentObj = meta.UnityObject;
            }
            else
            {
                compType = GetTypeByName(meta.ComponentName, EGatheredTypeCategory.EcsComponent);
#if DEBUG
                if (compType == null)
                    throw new Exception(obj.name + ". can't find component type " + meta.ComponentName);
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

#if DEBUG
                    try
                    {
                        fieldInfo.SetValue(componentObj, value);
                    }
                    catch (Exception e)
                    {
                        var newEx = new Exception(e.Message + ". compType: " + compType.FullName + ". fieldInfo: " +
                                                  fieldInfo.Name + ". view: " + obj.name);
                        throw newEx;
                    }
#else
                    fieldInfo.SetValue(componentObj, value);
#endif
                    
                }
            }
            AddParams[0] = entityId;
            AddParams[1] = componentObj;

            MethodInfo genAddMethodInfo = addMethodInfo.MakeGenericMethod(compType);
            genAddMethodInfo.Invoke(world, AddParams);
        }

        return entityId;
    }

#region EntityInspectorHelper
    private static string[] GetTypeNames<SameAssemblyType>(Func<Type, bool> predicate)
    {
        var types = Assembly.GetAssembly(typeof(SameAssemblyType)).GetTypes().Where(predicate).ToArray();
        return Array.ConvertAll(types, (t) => t.FullName);
    }

#if UNITY_EDITOR
    public static bool IsSearchMatch(string searchString, string name)
    {
        if (searchString == null || searchString.Length == 0)
            return true;
        return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string GetTypeUIName(string fullName) => fullName.Substring(fullName.LastIndexOf('.') + 1);

    public static void OnEntityInspectorGUI(SerializedObject serializedObject, UnityEngine.Object target,
        ref EntityInspectorCommonData commonInspectorData, ref EntityMeta data, EntityViewComponentsData componentsData = default)
    {
        serializedObject.Update();
        DrawAddComponents(ref commonInspectorData.AddListExpanded, ref commonInspectorData.AddSearch, ref data, target, componentsData);
        commonInspectorData.AddedSearch = EditorGUILayout.TextField(commonInspectorData.AddedSearch);
        DrawComponents(ref data, ref commonInspectorData.AddedSearch, target);
    }

    private static void DrawAddComponents(ref bool addListExpanded, ref string addSearch, ref EntityMeta data,
        UnityEngine.Object target, EntityViewComponentsData componentsData = default)
    {
        var listText = addListExpanded ? "Shrink components list" : "Expand components list";
        if (GUILayout.Button(new GUIContent(listText), GUILayout.ExpandWidth(false)))
            addListExpanded = !addListExpanded;
        if (addListExpanded)
        {
            addSearch = EditorGUILayout.TextField(addSearch);
            EditorGUILayout.BeginVertical();
            DrawAddList(Components, ComponentTypeNames, addSearch, ref data, target);
            GUILayout.Space(10);
            DrawAddList(Tags, TagTypeNames, addSearch, ref data, target);
            GUILayout.Space(10);
            if (componentsData.ViewComponentTypeNames != null && componentsData.ViewComponentTypeNames.Count > 0)
            {
                DrawAddUnityComponentList(UnityComponents, componentsData, addSearch, ref data, target);
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private static void OnAddComponent(string componentName, ref EntityMeta data, UnityEngine.Object target)
    {
        var type = GetTypeByName(componentName, EGatheredTypeCategory.UnityObject);
        if (IsUnityObject(type))
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

    private static void DrawAddUnityComponentList(string label, EntityViewComponentsData componentsData, string search,
        ref EntityMeta data, UnityEngine.Object target)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        for (int i = 0; i < componentsData.ViewComponentTypeNames.Count; i++)
        {
            var compSubTypes = componentsData.ViewComponentTypeNames[i];
            if (compSubTypes.Count == 0)
                continue;

            EditorGUILayout.BeginHorizontal();

            componentsData.ViewComponentFoldouts[i] =
                EditorGUILayout.BeginFoldoutHeaderGroup(componentsData.ViewComponentFoldouts[i], GetTypeUIName(compSubTypes[0]));
            bool canAddFirstComponent = IsSearchMatch(search, compSubTypes[0]) &&
                !IsComponentAlreadyAdded(compSubTypes[0], data);
            if (canAddFirstComponent && GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                OnAddComponent(compSubTypes[0], ref data, target);

            EditorGUILayout.EndHorizontal();

            if (componentsData.ViewComponentFoldouts[i])
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
                        OnAddComponent(componentName, ref data, target);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

    private static void DrawAddList(string label, string[] components, string search, ref EntityMeta data, UnityEngine.Object target)
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
                OnAddComponent(componentName, ref data, target);

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

    private static void DrawComponents(ref EntityMeta data, ref string search, UnityEngine.Object target)
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
            var componentUIName = GetTypeUIName(meta.ComponentName);
            if (meta.Fields == null || meta.Fields.Length == 0)
            {
                EditorGUILayout.LabelField(componentUIName);
            }
            else
            {
                meta.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(meta.IsExpanded, componentUIName);
                if (meta.IsExpanded)
                {
                    for (int i = 0; i < meta.Fields.Length; i++)
                    {
                        if (!meta.Fields[i].IsHiddenInEditor)
                            DrawField(ref meta.Fields[i], target);
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private static void DrawField(ref ComponentFieldMeta fieldMeta, UnityEngine.Object target)
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField(fieldMeta.Name);
            var valueObject = fieldMeta.GetValue();

            bool setDirty = false;
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
            else if (fieldMeta.TypeName == typeof(string).FullName)
            {
                var str = valueObject as string;
                setDirty = fieldMeta.SetValue(EditorGUILayout.TextField(str));
            }
            else if (typeof(Object).IsAssignableFrom(GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.UnityObject)))
            {
                var type = GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.UnityObject);
                var obj = valueObject != null ? (Object)valueObject : null;
                setDirty = fieldMeta.SetValue(EditorGUILayout.ObjectField("", obj, type, true));
            }
            else if (GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.Enum) != null)
            {
                var enumType = GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.Enum);
                Enum value;
                if (string.IsNullOrEmpty(fieldMeta.ValueRepresentation))
                    value = (Enum)Enum.GetValues(enumType).GetValue(0);
                else
                    value = (Enum)Enum.Parse(enumType, fieldMeta.ValueRepresentation);
                setDirty = fieldMeta.SetValue(EditorGUILayout.EnumPopup(value));
            }

            if (setDirty)
                EditorUtility.SetDirty(target);
        }
        EditorGUILayout.EndHorizontal();
    }
#endif

#endregion
}
