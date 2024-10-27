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
            
            if (view.Have<TriggerEnterComponent>())
            {
                if (view.Have<OverrideTriggerEnter>())
                    view.GetEcsComponent<TriggerEnterComponent>().collider = other;
            }
            else
            {
                view.Add(new TriggerEnterComponent { collider = other });
            }
        }
    }
}