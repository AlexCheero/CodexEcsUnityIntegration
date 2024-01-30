using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class EntityViewHelper
    {
        public static EntityView GetOwnerEntityView(GameObject go)
        {
            var view = go.GetComponent<EntityView>();
            if (view != null)
                return view;
            var viewChild = go.GetComponent<EntityViewChild>();
            if (viewChild != null)
                view = viewChild.OwnerView;

            return view;
        }

        public static EntityView GetOwnerEntityView(Component component)
        {
            var view = component.GetComponent<EntityView>();
            if (view != null)
                return view;
            var viewChild = component.GetComponent<EntityViewChild>();
            if (viewChild != null)
                view = viewChild.OwnerView;

            return view;
        }
    }
}