using ECS;
using UnityEngine;

namespace Components
{
    public interface IComponent { }

    public struct CollisionComponent : IComponent
    {
        public Vector3 contactPoint;
        public Collider collider;
    }

    public struct TriggerFireComponent : IComponent
    {
        public Collider coliider;
    }
}

namespace Tags
{
    public interface ITag { }

    public struct OverrideCollision : ITag { }
    public struct OverrideTriggerFire : ITag { }
}