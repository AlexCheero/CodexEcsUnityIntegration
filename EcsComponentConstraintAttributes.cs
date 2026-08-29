using System;

namespace CodexFramework.CodexEcsUnityIntegration
{
    /// <summary>
    /// Declares ECS components that the EntityView/EntityPreset inspector must add before
    /// adding the annotated component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EcsRequireComponentAttribute : Attribute
    {
        public Type[] ComponentTypes { get; }

        public EcsRequireComponentAttribute(params Type[] componentTypes) =>
            ComponentTypes = componentTypes ?? Array.Empty<Type>();
    }

    /// <summary>
    /// Declares ECS components that cannot coexist with the annotated component when
    /// components are added through the EntityView/EntityPreset inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EcsExcludeComponentAttribute : Attribute
    {
        public Type[] ComponentTypes { get; }

        public EcsExcludeComponentAttribute(params Type[] componentTypes) =>
            ComponentTypes = componentTypes ?? Array.Empty<Type>();
    }
}
