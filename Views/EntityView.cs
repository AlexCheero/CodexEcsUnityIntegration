using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
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
                _components.Add(componentView.CreateWrapper());
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

        private BaseComponentView[] _componentViews;
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
        void Awake() => Init();
        public void Init()
        {
            if (_componentViews == null || _componentViews.Length == 0)
                _componentViews = GetComponents<BaseComponentView>();
            if (_componentsBuffer == null || _componentsBuffer.Length == 0)
                _componentsBuffer = GatherUnityComponents();

#if UNITY_EDITOR && CODEX_ECS_EDITOR
            _viewsByComponentType ??= _componentViews.ToDictionary(view => view.GetEcsComponentType(), view => view);
            _typesToCheck ??= new HashSet<Type>(_viewsByComponentType.Keys);
            _typesBuffer ??= new HashSet<Type>(_viewsByComponentType.Keys);
#endif
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

            if (_componentViews == null)
                Init();

            for (var i = 0; i < _componentViews.Length; i++)
                _componentViews[i].AddToWorld(_world, _id);

            RegisterUnityComponents(_world);

            return _id;
        }

        public int CreatePureEntity(EcsWorld world)
        {
            if (_componentViews == null || _componentViews.Length == 0)
                _componentViews = GetComponents<BaseComponentView>();
            var eid = world.Create();
            for (var i = 0; i < _componentViews.Length; i++)
                _componentViews[i].AddToWorld(world, eid);
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

#if UNITY_EDITOR && CODEX_ECS_EDITOR
        private bool _validationGuard;//hack to not loose values on inspector update in late update
        public void OnComponentValidate<T>(BaseComponentView view, T component)
        {
            if (_validationGuard)
                return;

            if (_world == null || !_world.IsEntityValid(_entity))
                return;

            _viewsByComponentType[typeof(T)] = view;
            if (!ComponentMeta<T>.IsTag)
                GetOrAdd<T>() = component;
        }

        public void OnComponentEnable<T>(BaseComponentView view, T component)
        {
            if (_world == null || !_world.IsEntityValid(_entity))
                return;

            _viewsByComponentType[typeof(T)] = view;
            if (!ComponentMeta<T>.IsTag)
                GetOrAdd<T>() = component;
        }

        public void OnComponentDisable<T>()
        {
            if (_world == null || !_world.IsEntityValid(_entity))
                return;

            if (_viewsByComponentType.ContainsKey(typeof(T)))
                _viewsByComponentType.Remove(typeof(T));
            TryRemove<T>();
        }

        private Dictionary<Type, BaseComponentView> _viewsByComponentType;
        private HashSet<Type> _typesToCheck;
        private HashSet<Type> _typesBuffer;

        void LateUpdate()
        {
            if (!_updateInspector)
                return;
            
            if (!IsValid)
                return;

            _world.GetTypesForId(_id, _typesBuffer);
            foreach (var type in _typesBuffer)
            {
                if (!ViewRegistrator.IsTypeHaveView(type))
                    continue;

                var isComponent = typeof(IComponent).IsAssignableFrom(type);
                if (!isComponent)
                    continue;

                if (!_viewsByComponentType.ContainsKey(type))
                {
                    var viewType = ViewRegistrator.GetViewTypeByCompType(type);
                    _validationGuard = true;
                    _viewsByComponentType[type] = gameObject.GetComponent(viewType) as BaseComponentView;
                    if (_viewsByComponentType[type] == null)
                        _viewsByComponentType[type] = gameObject.AddComponent(viewType) as BaseComponentView;
                    _validationGuard = false;
                }

                _viewsByComponentType[type].UpdateFromWorld(_world, _id);
            }

            _typesToCheck.Clear();
            _typesToCheck.UnionWith(_viewsByComponentType.Keys);
            _typesToCheck.ExceptWith(_typesBuffer);

            foreach (var type in _typesToCheck)
            {
                var viewType = _viewsByComponentType[type].GetType();
                Destroy(GetComponent(viewType));
                _viewsByComponentType.Remove(type);
            }
        }
#endif
    }
}