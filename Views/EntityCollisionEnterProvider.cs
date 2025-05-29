using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityCollisionEnterProvider : EntityUnityCallbackProvider
    {
        void OnCollisionEnter(Collision collision)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionEnter invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new CollisionEnterComponent
            {
                collider = collision.collider,
                contactPoint = collision.contacts[0].point,
                normal = collision.contacts[0].normal,
                rb = collision.rigidbody
            };
            if (view.Have<CollisionEnterComponent>())
            {
                if (view.Have<OverrideCollision>())
                    view.GetEcsComponent<CollisionEnterComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}