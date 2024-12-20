using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using CodexFramework.CodexEcsUnityIntegration.Views;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class SystemAttribute : Attribute
    {
        //TODO: turn into flag
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
        OnDisable,
        Reactive
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
            if (string.IsNullOrEmpty(searchString))
                return true;
            return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif
    }
}