using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Components
{
    public struct CollisionComponent : IComponent
    {
        public Vector3 contactPoint;
        public Collider collider;
    }

    public struct TriggerEnterComponent : IComponent
    {
        public Collider collider;
    }

    public struct TriggerExitComponent : IComponent
    {
        public Collider collider;
    }
}

namespace CodexFramework.CodexEcsUnityIntegration.Tags
{
    public struct OverrideCollision : ITag { }
    public struct OverrideTriggerEnter : ITag { }
    public struct OverrideTriggerExit : ITag { }
}