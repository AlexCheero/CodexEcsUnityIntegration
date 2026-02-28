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
        public abstract Type GetComponentType();
        
#if UNITY_EDITOR
        public const string ComponentPropertyName = "_component";
        public abstract void InitFromComponent(IComponent component);
#endif
    }

    [Serializable]
    public class ComponentWrapper<T> : ComponentWrapper where T : IComponent
    {
        [SerializeField]
        private T _component;
        public T Component
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _component;
        }

        public override void AddToWorld(EcsWorld world, int id)
        {
            ComponentMeta<T>.Init(ref _component);
            world.Add(id, _component);
        }
        
        public override Type GetComponentType() => typeof(T);

#if UNITY_EDITOR
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
        public const string ComponentsPropertyName = nameof(_components);
        public const string ForceInitPropertyName = nameof(_forceInit);

        [ContextMenu(nameof(RebuildUnityComponentsCache))]
        public void RebuildUnityComponentsCache()
        {
            var allComponents = GetComponents<Component>();
            _unityComponentsBuffer = new List<Component>(allComponents.Length);
            for (var i = 0; i < allComponents.Length; i++)
            {
                var component = allComponents[i];
                var compType = component.GetType();
                //TODO: uncomment when remove all the EntityView references
                //if (compType == typeof(EntityView))
                //    continue;
                
                while (compType != null && compType != typeof(MonoBehaviour) && compType != typeof(Behaviour) &&
                       compType != typeof(Component))
                {
                    _unityComponentsBuffer.Add(component);
                    compType = compType.BaseType;
                }
            }
        }
#endif
        
        [SerializeReference]
        private List<ComponentWrapper> _components;
        public IReadOnlyList<ComponentWrapper> Components => _components;
        
        private Dictionary<Type, ComponentWrapper> _componentsMap;

        public bool TryGetComponentDefaultValue<T>(out T result) where T : IComponent
        {
            result = default;
            var targetType = typeof(T);
            _componentsMap ??= new();
            if (_componentsMap.TryGetValue(targetType, out var componentWrapper))
            {
                result = ((ComponentWrapper<T>)componentWrapper).Component;
                return true;
            }

            
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].GetComponentType() != targetType)
                    continue;
                _componentsMap[targetType] = _components[i];
                result = ((ComponentWrapper<T>)_componentsMap[targetType]).Component;
                return true;
            }

            return false;
        }

        [SerializeField]
        private bool _forceInit;
        public bool ForceInit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _forceInit;
        }

        [SerializeField]
        private List<Component> _unityComponentsBuffer;

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
        
        private void RegisterUnityComponents(EcsWorld world)
        {
            //it should have at least Transform
            // if (_unityComponentsBuffer.Count == 0)
            //     return;

            var component = _unityComponentsBuffer[0];
            var type = component.GetType();
            if (!ComponentMapping.HaveType(type))
                CallStaticCtorForComponentMeta(type);
            world.AddMultiple_Dynamic(type, _id, component);
            
            for (int i = 1; i < _unityComponentsBuffer.Count; i++)
            {
                component = _unityComponentsBuffer[i];
                type = _unityComponentsBuffer[i - 1] == component
                    ? type.BaseType
                    : _unityComponentsBuffer[i].GetType();
                if (!ComponentMapping.HaveType(type))
                    CallStaticCtorForComponentMeta(type);
                world.AddMultiple_Dynamic(type, _id, component);
            }
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