using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityTriggerExitProvider : MonoBehaviour
    {
        private EntityView _view;
        
        void Awake() => _view = GetComponent<EntityView>();

        void OnTriggerExit(Collider other)
        {
            if (!_view.IsValid)
            {
#if DEBUG
                Debug.Log("OnTriggerExit invalid entity");
#endif
                return;
            }
            
            if (_view.Have<TriggerExitComponent>())
            {
                if (_view.Have<OverrideTriggerExit>())
                    _view.GetEcsComponent<TriggerExitComponent>().collider = other;
            }
            else
            {
                _view.Add(new TriggerExitComponent { collider = other });
            }
        }
    }
}