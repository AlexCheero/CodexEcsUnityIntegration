using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace CodexFramework.CodexEcsUnityIntegration
{
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
        public static Dictionary<string, Type> SystemTypes;
        public static List<Type> ComponentTypes;

        static IntegrationHelper()
        {
            SystemTypes = typeof(ECSPipeline).Assembly.GetTypes()
                .Where((type) => type != typeof(EcsSystem) && !type.IsGenericType && typeof(EcsSystem)
                    .IsAssignableFrom(type)).ToDictionary(t => t.FullName, t => t);
            
            ComponentTypes = TypeCache.GetTypesDerivedFrom<IComponent>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name)
                .ToList();

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