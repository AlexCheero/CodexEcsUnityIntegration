using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
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
            
            if (view.Have<TriggerExitComponent>())
            {
                if (view.Have<OverrideTriggerExit>())
                    view.GetEcsComponent<TriggerExitComponent>().collider = other;
            }
            else
            {
                view.Add(new TriggerExitComponent { collider = other });
            }
        }
    }
}