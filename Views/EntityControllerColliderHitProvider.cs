using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public class EntityControllerColliderHitProvider : EntityUnityCallbackProvider
    {
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            RecordHit(hit.collider, hit.point, hit.normal, hit.rigidbody);
        }

        internal void RecordHit(Collider otherCollider, Vector3 point, Vector3 normal, Rigidbody rb)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionEnter invalid entity");
#endif
                return;
            }
            
            // Preserve the flattest supporting contact independently of wall/edge collisions.
            if (normal.y >= 0.5f)
            {
                ref var ground = ref view.GetOrAdd<ControllerGroundHitComponent>();
                if (normal.y >= ground.normal.y)
                {
                    ground.otherCollider = otherCollider;
                    ground.contactPoint = point;
                    ground.normal = normal;
                }
            }

            var collisionComponent = new ControllerColliderHitComponent
            {
                collider = thisCollider,
                otherCollider = otherCollider,
                contactPoint = point,
                normal = normal,
                rb = rb
            };
            if (view.Have<ControllerColliderHitComponent>())
            {
                if (view.Have<OverrideCollision>())
                    view.Get<ControllerColliderHitComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}
