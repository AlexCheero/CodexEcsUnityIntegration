using Components;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using Tags;

public class SystemAttribute : Attribute
{
    public ESystemCategory[] Categories;
    public SystemAttribute(params ESystemCategory[] categories) => Categories = categories;
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

public static class IntegrationHelper
{
    //TODO: unify with typenames from inspectors
    //TODO: why not use list?
    public static IEnumerable<Type> EcsComponentTypes;
    public static Dictionary<string, Type> SystemTypes;

    static IntegrationHelper()
    {
        EcsComponentTypes = typeof(EntityView).Assembly.GetTypes()
            .Where((t) => typeof(IComponent).IsAssignableFrom(t) || typeof(ITag).IsAssignableFrom(t));

        SystemTypes = typeof(ECSPipeline).Assembly.GetTypes()
            .Where((type) => type != typeof(EcsSystem) && typeof(EcsSystem).IsAssignableFrom(type)).ToDictionary(t => t.FullName, t => t);
    }

#if UNITY_EDITOR
    public static bool IsSearchMatch(string searchString, string name)
    {
        if (searchString == null || searchString.Length == 0)
            return true;
        return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
    }
#endif
}
