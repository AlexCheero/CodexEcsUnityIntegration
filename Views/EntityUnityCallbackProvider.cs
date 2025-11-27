using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public abstract class EntityUnityCallbackProvider : MonoBehaviour
    {
        [SerializeField]
        protected Collider collider;
        [SerializeField]
        public EntityView view;
        
        private void OnValidate()
        {
            view = EntityViewHelper.GetOwnerEntityView(gameObject);
            if (view == null)
                Debug.LogError("EntityUnityCallbackProvider have no view");
        }
    }
}