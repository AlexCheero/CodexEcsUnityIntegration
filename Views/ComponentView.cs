using CodexECS;
using System;
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

#if UNITY_EDITOR && CODEX_ECS_EDITOR
        public abstract Type GetEcsComponentType();
        public abstract void UpdateFromWorld(EcsWorld world, int id);
#endif
    }

    public class ComponentView<T> : BaseComponentView
    {
#if UNITY_EDITOR
        [SerializeField]
        [HideInInspector]
        //this field should go before Component because for some reason if it goes after
        //OnValidate() gets the previous value of Component and thus Component's value can't be changed
        private T _defaultComponent;
#endif

        public T Component = ComponentMeta<T>.GetDefault();

        public override void AddToWorld(EcsWorld world, int id)
        {
#if UNITY_EDITOR && CODEX_ECS_EDITOR
            Component = _defaultComponent;
#endif
            world.Add(id, Component);
        }

#if UNITY_EDITOR && CODEX_ECS_EDITOR
        public override void UpdateFromWorld(EcsWorld world, int id)
        {
            if (ComponentMeta<T>.IsTag)
                return;

            var comp = world.Get<T>(id);
            Component = comp;
        }

        private bool _canValidate;//hack to validate only after game started and initialized
        void Start()
        {
            _canValidate = true;
        }

        //TODO this breaks runtime instantiation
        //void OnEnable()
        //{
        //    if (Owner != null)
        //        Owner.OnComponentEnable(this, Component);
        //}

        //void OnDisable()
        //{
        //    _canValidate = false;
        //    if (Owner != null)
        //        Owner.OnComponentDisable<T>();
        //}

        void Awake()
        {
            //object spawned from script won't call OnValidate
            _defaultComponent = Component;
        }

        void OnValidate()
        {
            ComponentMeta<T>.Init(ref Component);
            _defaultComponent = Component;
            if (_canValidate)
                Owner.OnComponentValidate(this, Component);
        }

        public override Type GetEcsComponentType() => typeof(T);
#endif
    }
}