using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class EntityViewHelper
    {
        public static EntityView Instantiate<T>(EcsWorld world, ComponentView<T> original, Transform parent)
            => Instantiate(world, original, Vector3.zero, Quaternion.identity, parent);
        public static EntityView Instantiate<T>(EcsWorld world, ComponentView<T> original, Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = Object.Instantiate(original.Owner, position, rotation, parent);
            instance.InitAsEntity(world);
            return instance;
        }

        public static EntityView Instantiate(EcsWorld world, EntityView original, Transform parent)
            => Instantiate(world, original, Vector3.zero, Quaternion.identity, parent);
        public static EntityView Instantiate(EcsWorld world, EntityView original, Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = Object.Instantiate(original, position, rotation, parent);
            instance.InitAsEntity(world);
            return instance;
        }

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