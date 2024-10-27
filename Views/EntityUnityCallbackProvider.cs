using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [RequireComponent(typeof(EntityView))]
    public abstract class EntityUnityCallbackProvider : MonoBehaviour
    {
        protected EntityView view;
        
        protected virtual void Awake() => view = GetComponent<EntityView>();
    }
}