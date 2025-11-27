using System.Runtime.CompilerServices;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class EntityViewChild : MonoBehaviour
    {
        [SerializeField]
        private EntityView _ownerView;

        public EntityView OwnerView
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ownerView;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_ownerView == null)
                _ownerView = GetComponentInParent<EntityView>();
            if (_ownerView == null)
                Debug.LogError($"{name} EntityViewChild has no EntityView parent");
        }
#endif
    }
}