#if UNITY_EDITOR && CODEX_ECS_EDITOR
using System;
using System.Collections.Generic;
using CodexECS;
using System.Runtime.CompilerServices;
using UnityEngine;
using CodexFramework.CodexEcsUnityIntegration.Views;
using System.Linq;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class ViewRegistrator
    {
        private static Dictionary<Type, Type> _viewsByCompTypes = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type GetViewTypeByCompType(Type compType) => _viewsByCompTypes[compType];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTypeHaveView(Type compType) => _viewsByCompTypes.ContainsKey(compType);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void Register()
        {
            var viewsTypes = typeof(EntityView).Assembly.GetTypes()
                    .Where(t => typeof(BaseComponentView).IsAssignableFrom(t) && t != typeof(BaseComponentView));
            foreach (var viewType in viewsTypes)
            {
                var baseType = viewType.BaseType;

                // Walk up the inheritance hierarchy if necessary
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ComponentView<>))
                    {
                        var componentType = baseType.GetGenericArguments()[0];
                        if (typeof(IComponent).IsAssignableFrom(componentType))
                            _viewsByCompTypes[componentType] = viewType;
                        break;
                    }

                    baseType = baseType.BaseType;
                }
            }
        }
    }
}
#endif