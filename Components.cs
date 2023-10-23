using System;
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

    [Serializable]
    public struct EcsTransform : IComponent
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        private Vector3 _localPosition;
        public Vector3 LocalPosition
        {
            readonly get => _localPosition;
            set
            {
                var delta = value - _localPosition;
                _localPosition = value;
                position += delta;
            }
        }

        private Quaternion _localRotation;
        public Quaternion LocalRotation
        {
            readonly get => _localRotation;
            set
            {
                var delta = value * Quaternion.Inverse(_localRotation);
                _localRotation = value;
                rotation = delta * rotation;
            }
        }
    }
}

namespace Tags
{
    public interface ITag { }

    public struct OverrideCollision : ITag { }
    public struct OverrideTriggerFire : ITag { }

    public struct ApplyEcsTransformTag : ITag { }
    public struct ApplyEcsLocalTransformTag : ITag { }
    public struct ApplyEcsTransformScaleTag : ITag { }
}