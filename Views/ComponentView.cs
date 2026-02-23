using CodexECS;
using System;
using System.Runtime.CompilerServices;
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
        //this field should go before Component because for some reason if it goes after
        //OnValidate() gets the previous value of Component and thus Component's value can't be changed
        private T _initialComponent;
#endif

        public T Component = ComponentMeta<T>.GetDefault();

        public override void AddToWorld(EcsWorld world, int id)
        {
#if UNITY_EDITOR && CODEX_ECS_EDITOR
            if (_isInitialized)
                Component = _initialComponent;
            else
#endif
            ComponentMeta<T>.Init(ref Component);
            world.Add(id, Component);
        }
        
#if UNITY_EDITOR && CODEX_ECS_EDITOR
        private bool _isInitialized;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitComponent()
        {
            ComponentMeta<T>.Init(ref Component);
            _initialComponent = Component;
            _isInitialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Awake() => InitComponent(); //object spawned from script won't call OnValidate
        
        public override void UpdateFromWorld(EcsWorld world, int id)
        {
            if (ComponentMeta<T>.IsTag)
                return;

            var comp = world.Get<T>(id);
            Component = comp;
        }

        private bool _canValidate;//hack to validate only after game started and initialized
        void Start() => _canValidate = true;

        void OnValidate()
        {
            InitComponent();
            if (_canValidate)
                Owner.OnComponentValidate(this, Component);
        }

        public override Type GetEcsComponentType() => typeof(T);
#endif
    }
}