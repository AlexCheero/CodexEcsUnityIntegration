using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityCollisionEnterProvider : MonoBehaviour
    {
        private EntityView _view;
        
        void Awake() => _view = GetComponent<EntityView>();

        void OnCollisionEnter(Collision collision)
        {
            if (!_view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionEnter invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new CollisionEnterComponent
            {
                collider = collision.collider,
                contactPoint = collision.GetContact(0).point,
                rb = collision.rigidbody
            };
            if (_view.Have<CollisionEnterComponent>())
            {
                if (_view.Have<OverrideCollision>())
                    _view.GetEcsComponent<CollisionEnterComponent>() = collisionComponent;
            }
            else
            {
                _view.Add(collisionComponent);
            }
        }
    }
}