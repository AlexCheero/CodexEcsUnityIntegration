using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityCollisionExitProvider : MonoBehaviour
    {
        private EntityView _view;
        
        void Awake() => _view = GetComponent<EntityView>();

        void OnCollisionExit(Collision collision)
        {
            if (!_view.IsValid)
            {
#if DEBUG
                Debug.Log("OnCollisionExit invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new CollisionExitComponent
            {
                collider = collision.collider,
                contactPoint = collision.GetContact(0).point,
                rb = collision.rigidbody
            };
            if (_view.Have<CollisionEnterComponent>())
            {
                if (_view.Have<OverrideCollision>())
                    _view.GetEcsComponent<CollisionExitComponent>() = collisionComponent;
            }
            else
            {
                _view.Add(collisionComponent);
            }
        }
    }
}