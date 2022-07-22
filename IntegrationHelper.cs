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

public class MutualyExclusiveAttribute : Attribute
{
    public Type[] Exclusives;
    public MutualyExclusiveAttribute(params Type[] exclusives) => Exclusives = exclusives;
}

public enum EReactionType
{
    OnAdd,
    OnRemove,
    OnChange
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
    public const string Components = "Components";
    public const string Tags = "Tags";

    //TODO: unify with typenames from inspectors
    public static Type[] EcsComponentTypes;
    public static Type[] UnityComponentTypes;
    public static Type[] SystemTypes;

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
    }

    public static string GetTypeUIName(string fullName) => fullName.Substring(fullName.LastIndexOf('.') + 1);

    public static void DrawAddList(string label, string[] components, string[] except, Action<string> onAdd, string search)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var componentName in components)
        {
            if (!IsSearchMatch(search, componentName) || ShouldSkipItem(componentName, except))
                continue;

            EditorGUILayout.BeginHorizontal();

            //TODO: add lines between components for readability
            //      or remove "+" button and make buttons with component names on it
            EditorGUILayout.LabelField(GetTypeUIName(componentName));
            bool tryAdd = GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false));
            if (tryAdd)
                onAdd(componentName);

            EditorGUILayout.EndHorizontal();
        }
    }

    public static string[] GetTypeNames<SameAssemblyType>(Func<Type, bool> predicate)
    {
        var types = Assembly.GetAssembly(typeof(SameAssemblyType)).GetTypes().Where(predicate).ToArray();
        return Array.ConvertAll(types, (t) => t.FullName);
    }

    public static bool HaveAttribute<AttribType>(Type type) where AttribType : Attribute
        => type.GetCustomAttributes(typeof(AttribType), true).Any();

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

    public static bool IsSearchMatch(string searchString, string name)
    {
        if (searchString == null || searchString.Length == 0)
            return true;
        return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool ShouldSkipItem(string item, string[] skippedItems)
    {
        foreach (var skippedItem in skippedItems)
        {
            if (item == skippedItem)
                return true;
        }
        return false;
    }
}
