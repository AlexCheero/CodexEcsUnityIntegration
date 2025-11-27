using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Components
{
    public struct ControllerColliderHitComponent : IComponent
    {
        public Vector3 contactPoint;
        public Vector3 normal;
        public Collider collider;
        public Collider otherCollider;
        public Rigidbody rb;
    }
    
    public struct CollisionEnterComponent : IComponent
    {
        public Vector3 contactPoint;
        public Vector3 normal;
        public Collider collider;
        public Collider otherCollider;
        public Rigidbody rb;
    }
    
    public struct CollisionExitComponent : IComponent
    {
        public Vector3 contactPoint;
        public Vector3 normal;
        public Collider collider;
        public Collider otherCollider;
        public Rigidbody rb;
    }
    
    //TODO: probably stay components should have array of entries
    // public struct CollisionStayComponent : IComponent
    // {
    //     public Vector3 contactPoint;
    //     public Collider collider;
    //     public Collider otherCollider;
    //     public Rigidbody rb;
    // }

    public struct TriggerEnterComponent : IComponent
    {
        public Collider trigger;
        public Collider otherCollider;
    }

    public struct TriggerExitComponent : IComponent
    {
        public Collider trigger;
        public Collider otherCollider;
    }
    
    // public struct TriggerStayComponent : IComponent
    // {
    //     public Collider trigger;
    //     public Collider collider;
    // }
}

namespace CodexFramework.CodexEcsUnityIntegration.Tags
{
    public struct OverrideCollision : IComponent { }
    public struct OverrideTriggerEnter : IComponent { }
    public struct OverrideTriggerExit : IComponent { }
}