using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class InitSystemAttribute : Attribute { }
public class UpdateSystemAttribute : Attribute { }
public class FixedUpdateSystemAttribute : Attribute { }

public enum ESystemCategory
{
    Init = 0,
    Update,
    FixedUpdate,
    Max
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

        SystemTypes = typeof(EcsSystem).Assembly.GetTypes()
            //TODO: duplicated from Pipeline_Inspector move to helper class
            .Where((type) => type != typeof(EcsSystem) && typeof(EcsSystem).IsAssignableFrom(type)).ToArray();
    }

    public static string GetTypeUIName(string fullName) => fullName.Substring(fullName.LastIndexOf('.') + 1);

    public static void DrawAddList(string label, string[] components, Action<string> onAdd)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var componentName in components)
        {
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
}
