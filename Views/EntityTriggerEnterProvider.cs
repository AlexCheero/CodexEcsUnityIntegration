using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityTriggerEnterProvider : EntityUnityCallbackProvider
    {
        void OnTriggerEnter(Collider other)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnTriggerEnter invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new TriggerEnterComponent
            {
                trigger = collider,
                otherCollider = other
            };
            if (view.Have<TriggerEnterComponent>())
            {
                if (view.Have<OverrideTriggerEnter>())
                {
                    view.GetEcsComponent<TriggerEnterComponent>() = collisionComponent;
                }
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}