using System;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ComponentFilterAttribute : PropertyAttribute
    {
        public Type[] RequiredComponents;
        public Type[] ExcludedComponents;
        public ComponentFilterAttribute(Type[] requiredComponents, Type[] excludedComponents = null)
        {
            RequiredComponents = requiredComponents;
            ExcludedComponents = excludedComponents;
        }
    }
}