using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public class EntityTriggerExitProvider : EntityUnityCallbackProvider
    {
        void OnTriggerExit(Collider other)
        {
            if (!view.IsValid)
            {
#if DEBUG
                Debug.Log("OnTriggerExit invalid entity");
#endif
                return;
            }
            
            var collisionComponent = new TriggerExitComponent
            {
                trigger = thisCollider,
                otherCollider = other
            };
            if (view.Have<TriggerExitComponent>())
            {
                if (view.Have<OverrideTriggerExit>())
                    view.GetEcsComponent<TriggerExitComponent>() = collisionComponent;
            }
            else
            {
                view.Add(collisionComponent);
            }
        }
    }
}