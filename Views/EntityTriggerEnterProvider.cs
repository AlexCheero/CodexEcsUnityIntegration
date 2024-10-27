using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public class EntityTriggerEnterProvider : MonoBehaviour
    {
        private EntityView _view;
        
        void Awake() => _view = GetComponent<EntityView>();

        void OnTriggerEnter(Collider other)
        {
            if (!_view.IsValid)
            {
#if DEBUG
                Debug.Log("OnTriggerEnter invalid entity");
#endif
                return;
            }
            
            if (_view.Have<TriggerEnterComponent>())
            {
                if (_view.Have<OverrideTriggerEnter>())
                    _view.GetEcsComponent<TriggerEnterComponent>().collider = other;
            }
            else
            {
                _view.Add(new TriggerEnterComponent { collider = other });
            }
        }
    }
}