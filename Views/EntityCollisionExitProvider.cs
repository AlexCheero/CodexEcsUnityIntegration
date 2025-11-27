using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityCollisionExitProvider : EntityUnityCallbackProvider
    {
        void OnCollisionExit(Collision collision)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionExit invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new CollisionExitComponent
            {
                collider = collider,
                otherCollider = collision.collider,
                contactPoint = collision.contacts[0].point,
                normal = collision.contacts[0].normal,
                rb = collision.rigidbody
            };
            if (view.Have<CollisionExitComponent>())
            {
                if (view.Have<OverrideCollision>())
                    view.GetEcsComponent<CollisionExitComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}