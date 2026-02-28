using UnityEngine;
using UnityEngine.Serialization;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public abstract class EntityUnityCallbackProvider : MonoBehaviour
    {
        [FormerlySerializedAs("collider")]
        [SerializeField]
        protected Collider thisCollider;
        [SerializeField]
        protected EntityView view;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (view == null)
                view = EntityViewHelper.GetOwnerEntityView(gameObject);
            if (view == null)
                Debug.LogError($"{name} EntityUnityCallbackProvider have no view");
            
            if (thisCollider == null)
                thisCollider = GetComponent<Collider>();
            if (thisCollider == null)
                Debug.LogError($"{name} EntityUnityCallbackProvider has no Collider");
        }
#endif
    }
}