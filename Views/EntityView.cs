using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
#if UNITY_EDITOR
    public static class EntityValidator
    {
        public static void ValidateComponents(List<ComponentWrapper> components)
        {
            // PasteComponentAsNew can invoke OnValidate before _components is assigned.
            if (components == null)
                return;
            for (int i = components.Count - 1; i >= 0; i--)
            {
                if (components[i] == null)
                    components.RemoveAt(i);
            }
        }
    }
#endif
    
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInspector<T>() => Add<T>();

        private void OnValidate() => EntityValidator.ValidateComponents(_components);
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
            var populatedTypes = new BitMask();
            for (int i = 0; i < _unityComponentsBuffer.Count; i++)
            {
                var component = _unityComponentsBuffer[i];
                var type = GetUnityComponentType(i);
                var componentId = EnsureComponentTypeRegistered(type);
                if (populatedTypes.Check(componentId))
                    world.AddMultiple_Dynamic(type, _id, component);
                else
                {
                    world.Set_Dynamic(type, _id, component);
                    populatedTypes.Set(componentId);
                }
            }
        }

        private Type GetUnityComponentType(int index)
        {
            var component = _unityComponentsBuffer[index];
            if (index == 0 || _unityComponentsBuffer[index - 1] != component)
                return component.GetType();
            return GetUnityComponentType(index - 1).BaseType;
        }

        private static int EnsureComponentTypeRegistered(Type type)
        {
            if (!ComponentMapping.HaveType(type))
            {
                var specificType = typeof(ComponentMeta<>).MakeGenericType(type);
                RuntimeHelpers.RunClassConstructor(specificType.TypeHandle);
            }
            return ComponentMapping.GetIdForType(type);
        }

        private BitMask BuildDestinationMask(bool includeUnityComponents)
        {
            var mask = new BitMask();
            for (var i = 0; i < _components.Count; i++)
                mask.Set(_components[i].GetComponentId());

            if (!includeUnityComponents)
                return mask;

            var seenUnityTypes = new BitMask();
            for (var i = 0; i < _unityComponentsBuffer.Count; i++)
            {
                var type = GetUnityComponentType(i);
                var componentId = EnsureComponentTypeRegistered(type);
                if (seenUnityTypes.Check(componentId))
                {
                    var multipleType = typeof(MultipleComponents<>).MakeGenericType(type);
                    mask.Set(EnsureComponentTypeRegistered(multipleType));
                }
                else
                {
                    seenUnityTypes.Set(componentId);
                }
                mask.Set(componentId);
            }

            return mask;
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
            var destinationMask = BuildDestinationMask(true);
            _id = world.CreateWithComponents(destinationMask);
            _entity = _world.GetById(_id);

            for (var i = 0; i < _components.Count; i++)
                _components[i].AddToWorld(world, _id);

            RegisterUnityComponents(_world);

            return _id;
        }

        public int CreatePureEntity(EcsWorld world)
        {
            var destinationMask = BuildDestinationMask(false);
            var eid = world.CreateWithComponents(destinationMask);
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
        public ref T Get<T>() => ref _world.Get<T>(_id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() => _world.Remove<T>(_id);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove<T>() => _world.TryRemove<T>(_id);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly BitMask GetMask() => ref _world.GetMask(_id);

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