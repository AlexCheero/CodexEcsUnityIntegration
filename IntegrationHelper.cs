using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class SystemAttribute : Attribute
    {
        public readonly ESystemCategory Categories;
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
        public static readonly Dictionary<string, Type> SystemTypes;
        
#if UNITY_EDITOR
        public static readonly List<Type> ComponentTypes;
#endif
        
        static IntegrationHelper()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var allTypes = assemblies.SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null);
                }
            }).Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType);
            
            SystemTypes = allTypes
                .Where(t => typeof(EcsSystem).IsAssignableFrom(t) && t != typeof(EcsSystem))
                .ToDictionary(t => t.FullName, t => t);
            
#if UNITY_EDITOR
            ComponentTypes = allTypes
                .Where(t => typeof(IComponent).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();
#endif

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