using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class EntityViewHelper
    {
        public static EntityView GetOwnerEntityView(GameObject go)
        {
            if (go.TryGetComponent<EntityView>(out var view))
                return view;
            if (go.TryGetComponent<EntityViewChild>(out var viewChild))
                view = viewChild.OwnerView;

            return view;
        }

        public static EntityView GetOwnerEntityView(Component component)
        {
            if (component.TryGetComponent<EntityView>(out var view))
                return view;
            if (component.TryGetComponent<EntityViewChild>(out var viewChild))
                view = viewChild.OwnerView;

            return view;
        }
    }
}