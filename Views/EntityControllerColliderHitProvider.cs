using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public class EntityControllerColliderHitProvider : EntityUnityCallbackProvider
    {
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionEnter invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new ControllerColliderHitComponent
            {
                collider = thisCollider,
                otherCollider = hit.collider,
                contactPoint = hit.point,
                rb = hit.rigidbody
            };
            if (view.Have<ControllerColliderHitComponent>())
            {
                if (view.Have<OverrideCollision>())
                    view.GetEcsComponent<ControllerColliderHitComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}