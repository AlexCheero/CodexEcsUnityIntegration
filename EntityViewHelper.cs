using System.Runtime.CompilerServices;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class EntityViewHelper
    {
        public static EntityView Instantiate(EcsWorld world, EntityView original, Transform parent)
            => Instantiate(world, original, Vector3.zero, Quaternion.identity, parent);
        public static EntityView Instantiate(EcsWorld world, EntityView original, Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = Object.Instantiate(original, position, rotation, parent);
            instance.InitAsEntity(world);
            return instance;
        }

        public static bool GetOwnerEntityView(this GameObject go, out EntityView view)
        {
            view = null;
            if (go == null)
                return false;

            if (go.TryGetComponent<EntityView>(out var entityView))
            {
                view = entityView;
                return true;
            }
            
            if (!go.TryGetComponent<EntityViewChild>(out var viewChild))
                return false;
            
            view = viewChild.OwnerView;
            return view != null;
        }

        public static bool GetOwnerEntityView(this Component component, out EntityView view)
        {
            view = null;
            if (component == null)
                return false;

            if (component.TryGetComponent<EntityView>(out var entityView))
            {
                view = entityView;
                return true;
            }

            if (!component.TryGetComponent<EntityViewChild>(out var viewChild))
                return false;
            
            view = viewChild.OwnerView;
            return view != null;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetValidOwnerEntityView(this GameObject go, out EntityView view) => go.GetOwnerEntityView(out view) && view.IsValid;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetValidOwnerEntityView(this Component component, out EntityView view) => component.GetOwnerEntityView(out view) && view.IsValid;
    }
}