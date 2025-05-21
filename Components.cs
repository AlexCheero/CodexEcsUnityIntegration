using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Components
{
    public struct ControllerColliderHitComponent : IComponent
    {
        public ControllerColliderHit hit;
    }
    
    public struct CollisionEnterComponent : IComponent
    {
        public Collision collision;
    }
    
    public struct CollisionExitComponent : IComponent
    {
        public Collision collision;
    }
    
    //TODO: probably stay components should have array of entries
    // public struct CollisionStayComponent : IComponent
    // {
    //     public Vector3 contactPoint;
    //     public Collider collider;
    //     public Rigidbody rb;
    // }

    public struct TriggerEnterComponent : IComponent
    {
        public Collider collider;
    }

    public struct TriggerExitComponent : IComponent
    {
        public Collider collider;
    }
    
    // public struct TriggerStayComponent : IComponent
    // {
    //     public Collider collider;
    // }
}

namespace CodexFramework.CodexEcsUnityIntegration.Tags
{
    public struct OverrideCollision : IComponent { }
    public struct OverrideTriggerEnter : IComponent { }
    public struct OverrideTriggerExit : IComponent { }
}