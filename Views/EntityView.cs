using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [Serializable]
    public abstract class ComponentWrapper
    {
        public abstract void AddToWorld(EcsWorld world, int id);
        
#if UNITY_EDITOR
        public const string ComponentPropertyName = "_component";
        public abstract void InitFromComponent(IComponent component);
#endif
    }

    //TODO: uncomment where T : IComponent and remove ctors when ComponentViews will be deleted
    [Serializable]
    public class ComponentWrapper<T> : ComponentWrapper //where T : IComponent
    {
        [SerializeField]
        private T _component;

        public override void AddToWorld(EcsWorld world, int id)
        {
            ComponentMeta<T>.Init(ref _component);
            world.Add(id, _component);
        }

#if UNITY_EDITOR
        public ComponentWrapper() {}
        public ComponentWrapper(T component) => _component = component;
        
        public override void InitFromComponent(IComponent component) => _component = (T)component;
#endif
    }
    
    public static class EntityViewExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsViewValid(this EntityView view) => view != null && view.IsValid;
    }
    
    public class EntityView : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        private bool _updateInspector;
        
        public const string ComponentsPropertyName = nameof(_components);
        public const string ForceInitPropertyName = nameof(_forceInit);
        public const string UpdateInspectorPropertyName = nameof(_updateInspector);
        
        void OnValidate()
        {
            for (int i = _components.Count - 1; i > -1; i--)
            {
                if (_components[i] == null)
                    _components.RemoveAt(i);
            }
        }

        [ContextMenu(nameof(GatherComponentsFromViews))]
        public void GatherComponentsFromViews()
        {
            _components.Clear();
            foreach (var componentView in GetComponents<BaseComponentView>())
            {
                _components.Add(componentView.CreateWrapper());
                DestroyImmediate(componentView, true);
            }
        }
#endif
        
        [SerializeReference]
        private List<ComponentWrapper> _components;
        public IReadOnlyList<ComponentWrapper> Components => _components;

        [SerializeField]
        private bool _forceInit;
        public bool ForceInit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _forceInit;
        }

        private struct TypeSystemPair
        {
            public Type Type;
            public Component Component;
        }
        private SimpleList<TypeSystemPair> _componentsBuffer;

        private EcsWorld _world;
        private Entity _entity = EntityExtension.NullEntity;
        private int _id = -1;

        public Entity Entity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _entity;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => _entity = value;
        }
        public EcsWorld World
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _world;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => _world = value;
        }
        public int Id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _id;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => _id = value;
        }
        public int Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _entity.GetVersion();
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _world != null && _id == _entity.GetId() && _world.IsEntityValid(_entity);
        }

        //TODO: maybe move to void OnValidate()
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Awake() => Init();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Init()
        {
            if (_componentsBuffer == null || _componentsBuffer.Length == 0)
                _componentsBuffer = GatherUnityComponents();
        }

        private SimpleList<TypeSystemPair> GatherUnityComponents()
        {
            var allComponents = GetComponents<Component>();
            var list = new SimpleList<TypeSystemPair>();
            for (var i = 0; i < allComponents.Length; i++)
            {
                var component = allComponents[i];
                if (component is BaseComponentView)
                    continue;

                var compType = component.GetType();
                do
                {
                    if (!ComponentMapping.HaveType(compType))
                        CallStaticCtorForComponentMeta(compType);
                    list.Add(new TypeSystemPair
                    {
                        Type = compType,
                        Component = component
                    });
                    compType = compType.BaseType;
                } while (compType != typeof(MonoBehaviour) && compType != typeof(Behaviour) &&
                         compType != typeof(Component));
            }

            return list;
        }

        private void CallStaticCtorForComponentMeta(Type type)
        {
            var genericType = typeof(ComponentMeta<>);
            var specificType = genericType.MakeGenericType(type);
            
            // specificType.TypeInitializer?.Invoke(null, null);
            // fore some reason code above called static ctor twice so I used this code instead
            RuntimeHelpers.RunClassConstructor(specificType.TypeHandle);
        }

        public int InitAsEntityWithChildren(EcsWorld world)
        {
            foreach (var view in GetComponentsInChildren<EntityView>())
                view.InitAsEntity(world);
            return Id;
        }

        public int InitAsEntity(EcsWorld world)
        {
#if UNITY_EDITOR
            if (IsValid)
                Debug.LogError($"EntityView {name} is already valid");
#endif
            
            _world = world;
            _id = world.Create();
            _entity = _world.GetById(_id);

            if (_componentsBuffer == null)
                Init();

            for (var i = 0; i < _components.Count; i++)
                _components[i].AddToWorld(world, _id);

            RegisterUnityComponents(_world);

            return _id;
        }

        public int CreatePureEntity(EcsWorld world)
        {
            var eid = world.Create();
            for (var i = 0; i < _components.Count; i++)
                _components[i].AddToWorld(world, eid);
            return eid;
        }
        
        private void RegisterUnityComponents(EcsWorld world)
        {
            for (int i = 0; i < _componentsBuffer.Length; i++)
            {
                ref readonly var componentTuple = ref _componentsBuffer[i];
                world.AddMultiple_Dynamic(componentTuple.Type, _id, componentTuple.Component);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Have<T>() => _world.Have<T>(_id);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Have(in BitMask mask) => _world.Have(mask, _id);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() => _world.Add<T>(_id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(T component) => _world.Add(_id, component);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd<T>() => _world.TryAdd<T>(_id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetOrAdd<T>() => ref _world.GetOrAddComponent<T>(_id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetEcsComponent<T>() => ref _world.Get<T>(_id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() => _world.Remove<T>(_id);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove<T>() => _world.TryRemove<T>(_id);

        public void DeleteFromWorld()
        {
            _world.Delete(_id);
            _id = -1;
            _entity = EntityExtension.NullEntity;
        }

        void OnDestroy()
        {
            if (_world != null && _world.IsEntityValid(_entity))
                _world.Delete(_id);
        }

        public override string ToString() => World.DebugEntity(Id, true);
    }
}