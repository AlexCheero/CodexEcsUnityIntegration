using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CodexFramework.CodexEcsUnityIntegration.Views;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class SkipViewGenerationAttribute : Attribute {}
    
    public class SystemAttribute : Attribute
    {
        public ESystemCategory Categories;
        public SystemAttribute(ESystemCategory categories) => Categories = categories;
    }

    [Flags]
    public enum ESystemCategory
    {
        Init            = 1 << 0,
        Update          = 1 << 1,
        LateUpdate      = 1 << 2,
        FixedUpdate     = 1 << 3,
        LateFixedUpdate = 1 << 4,
        OnEnable        = 1 << 5,
        OnDisable       = 1 << 6,
        Reactive        = 1 << 7,
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
                .Where(t => typeof(IComponent).IsAssignableFrom(t) && t != typeof(IComponent));
            
            SystemTypes = typeof(ECSPipeline).Assembly.GetTypes()
                .Where((type) => type != typeof(EcsSystem) && typeof(EcsSystem).IsAssignableFrom(type)).ToDictionary(t => t.FullName, t => t);

            SystemCategories = (ESystemCategory[])Enum.GetValues(typeof(ESystemCategory));
        }

#if UNITY_EDITOR
        public static bool IsSearchMatch(string searchString, string name)
        {
            if (string.IsNullOrEmpty(searchString))
                return true;
            return name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ESystemCategory mask, ESystemCategory flag) => (mask & flag) == flag;

        public static ESystemCategory[] SystemCategories
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }
    }
}