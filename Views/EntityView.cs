using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
#if UNITY_EDITOR
    public static class EntityValidator
    {
        public static void ValidateComponents(UnityEngine.Object owner, List<ComponentWrapper> components)
        {
            // PasteComponentAsNew can invoke OnValidate before _components is assigned.
            if (components == null)
                return;
            // Asset import can precede compilation of a newly authored component. Unity
            // retains its serialized data, but removing the null slot loses that reference.
            if (UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(owner))
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
            InvalidateCachedMasks();
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

        private void OnValidate()
        {
            EntityValidator.ValidateComponents(this, _components);
            _groupMask.Invalidate();
            InvalidateCachedMasks();
            _componentsMap = null;
        }

        public void CollectGroupMaskTypes(List<Type> types)
        {
            var mask = BuildDestinationMask(true);
            foreach (var id in mask)
                types.Add(ComponentMapping.GetTypeForId(id));
        }
#endif
        
        [SerializeReference]
        private List<ComponentWrapper> _components = new();
        public IReadOnlyList<ComponentWrapper> Components => _components;

        [SerializeField] private EntityGroupMask _groupMask = new();

        private Dictionary<Type, ComponentWrapper> _componentsMap;

        // Authored components and the prefab's Unity component buffer do not change at runtime,
        // so the destination masks and the per-Unity-component ids are built once per view
        // instance (pooled views are initialized many times). Not serialized: a domain reload,
        // OnValidate or RebuildUnityComponentsCache rebuilds them.
        private BitMask _viewMask, _viewGroupMask, _pureMask, _pureGroupMask;
        private bool _viewMasksCached, _pureMasksCached;
        private int[] _unityComponentIds;
        private bool[] _unityComponentIsMultiple;

        private void InvalidateCachedMasks()
        {
            _viewMasksCached = false;
            _pureMasksCached = false;
            _unityComponentIds = null;
            _unityComponentIsMultiple = null;
        }

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
        private List<Component> _unityComponentsBuffer = new();

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
        public uint Version
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
            EnsureUnityComponentIds();
            for (int i = 0; i < _unityComponentsBuffer.Count; i++)
            {
                var dispatcher = ComponentMapping.GetDispatcher(_unityComponentIds[i]);
                // The first component of a type fills the default slot; repeats join its multiple storage.
                if (_unityComponentIsMultiple[i])
                    dispatcher.AddMultiple(world, _id, _unityComponentsBuffer[i]);
                else
                    dispatcher.Set(world, _id, _unityComponentsBuffer[i]);
            }
        }

        private void EnsureUnityComponentIds()
        {
            var count = _unityComponentsBuffer.Count;
            if (_unityComponentIds != null && _unityComponentIds.Length == count)
                return;
            _unityComponentIds = new int[count];
            _unityComponentIsMultiple = new bool[count];
            var seenTypes = new BitMask();
            for (var i = 0; i < count; i++)
            {
                var componentId = EnsureComponentTypeRegistered(GetUnityComponentType(i));
                _unityComponentIds[i] = componentId;
                _unityComponentIsMultiple[i] = seenTypes.Check(componentId);
                seenTypes.Set(componentId);
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

        public void PrewarmComponentTypes() => BuildDestinationMask(true);

        internal BitMask BuildDestinationMask(bool includeUnityComponents)
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
            if (!_viewMasksCached)
            {
                _viewMask = BuildDestinationMask(true);
                _viewGroupMask = _groupMask.Resolve(in _viewMask);
                _viewMasksCached = true;
            }
            return InitAsEntity(world, in _viewMask, in _viewGroupMask);
        }

        private int InitAsEntity(EcsWorld world, in BitMask destinationMask, in BitMask groupMask)
        {
#if UNITY_EDITOR
            if (IsValid)
                Debug.LogError($"EntityView {name} is already valid");
#endif

            _world = world;
            _id = world.CreateWithComponents(in destinationMask, in groupMask);
            _entity = _world.GetById(_id);

            for (var i = 0; i < _components.Count; i++)
                _components[i].AddToWorld(world, _id);

            RegisterUnityComponents(_world);

            return _id;
        }

        public int CreatePureEntity(EcsWorld world)
        {
            if (!_pureMasksCached)
            {
                _pureMask = BuildDestinationMask(false).SetTypeId<PureEntity>();
                _pureGroupMask = _groupMask.Resolve(in _pureMask);
                _pureMasksCached = true;
            }
            return CreatePureEntity(world, in _pureMask, in _pureGroupMask);
        }

        private int CreatePureEntity(EcsWorld world, in BitMask destinationMask, in BitMask groupMask)
        {
            var eid = world.CreateWithComponents(in destinationMask, in groupMask);
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
        
        // Every component of the entity (tests each registered type): for inspectors and debugging.
        public void CollectMask(ref BitMask mask) => _world.CollectMask(_id, ref mask);

        public void DeleteFromWorld()
        {
            // A checkout can be canceled before initialization, or its entity can
            // already have been deleted and its id reused by another entity.
            var world = _world;
            var id = _id;
            var isValid = IsValid;
            _world = null;
            _id = -1;
            _entity = EntityExtension.NullEntity;
            if (isValid)
                world.Delete(id);
        }

        void OnDestroy() => DeleteFromWorld();

        public override string ToString() => World.DebugEntity(Id, true);
    }
}
