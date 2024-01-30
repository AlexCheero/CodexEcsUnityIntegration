using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class EntityViewChild : MonoBehaviour
    {
        [SerializeField]
        private EntityView _ownerView;

        public EntityView OwnerView => _ownerView;
    }
}