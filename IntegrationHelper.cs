using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public const string Components = "Components";
    public const string Tags = "Tags";

    //TODO: unify with typenames from inspectors
    public static Type[] EcsComponentTypes;
    public static Type[] UnityComponentTypes;
    public static Type[] SystemTypes;

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
    }

    public static string GetTypeUIName(string fullName) => fullName.Substring(fullName.LastIndexOf('.') + 1);

    public static string[] GetTypeNames<SameAssemblyType>(Func<Type, bool> predicate)
    {
        var types = Assembly.GetAssembly(typeof(SameAssemblyType)).GetTypes().Where(predicate).ToArray();
        return Array.ConvertAll(types, (t) => t.FullName);
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

    public static bool IsSearchMatch(string searchString, string name)
    {
        if (searchString == null || searchString.Length == 0)
            return true;
        return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
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
}
