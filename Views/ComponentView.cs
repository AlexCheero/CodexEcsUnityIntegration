using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public abstract class BaseComponentView : MonoBehaviour
    {
        private EntityView _owner;
        public EntityView Owner
        {
            get
            {
                if (_owner == null)
                    _owner = GetComponent<EntityView>();
                return _owner;
            }
        }

        public abstract void AddToWorld(EcsWorld world, int id);
        
#if UNITY_EDITOR
        public abstract ComponentWrapper CreateWrapper();
#endif
    }

    public class ComponentView<T> : BaseComponentView
    {
#if UNITY_EDITOR
        public override ComponentWrapper CreateWrapper() => new ComponentWrapper<T>(Component);

        //this field should go before Component because for some reason if it goes after
        //OnValidate() gets the previous value of Component and thus Component's value can't be changed
        private T _initialComponent;
#endif

        public T Component = ComponentMeta<T>.GetDefault();

        public override void AddToWorld(EcsWorld world, int id)
        {
            ComponentMeta<T>.Init(ref Component);
            world.Add(id, Component);
        }
    }
}