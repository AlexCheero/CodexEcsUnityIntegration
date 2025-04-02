using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public static class EntityViewExtension
    {
        public static bool IsViewValid(this EntityView view) => view != null && view.IsValid;
    }
    
    public class EntityView : MonoBehaviour
    {
        [SerializeField]
        private bool _forceInit;
        public bool ForceInit => _forceInit;

        private List<Tuple<Type, Component>> _componentsBuffer;

        static EntityView()
        {
            ViewRegistrator.Register();
        }

        private BaseComponentView[] _componentViews;
        private EcsWorld _world;
        private Entity _entity = EntityExtension.NullEntity;
        private int _id = -1;

        public Entity Entity { get => _entity; private set => _entity = value; }
        public EcsWorld World { get => _world; private set => _world = value; }
        public int Id { get => _id; private set => _id = value; }
        public int Version => _entity.GetVersion();
        public bool IsValid => _world != null && _id == _entity.GetId() && _world.IsEntityValid(_entity);

        //TODO: maybe move to void OnValidate()
        void Awake() => Init();
        public void Init()
        {
            if (_componentViews == null || _componentViews.Length == 0)
                _componentViews = GetComponents<BaseComponentView>();
            if (_componentsBuffer == null || _componentsBuffer.Count == 0)
                _componentsBuffer = GatherUnityComponents();

#if UNITY_EDITOR && CODEX_ECS_EDITOR
            _viewsByComponentType ??= _componentViews.ToDictionary(view => view.GetEcsComponentType(), view => view);
            _typesToCheck ??= new HashSet<Type>(_viewsByComponentType.Keys);
            _typesBuffer ??= new HashSet<Type>(_viewsByComponentType.Keys);
#endif
        }

        private List<Tuple<Type, Component>> GatherUnityComponents()
        {
            var allComponents = GetComponents<Component>();
            var list = new List<Tuple<Type, Component>>();
            foreach (var component in allComponents)
            {
                if (component is BaseComponentView)
                    continue;

                var compType = component.GetType();
                do
                {
                    if (!ComponentMapping.HaveType(compType))
                        CallStaticCtorForComponentMeta(compType);
                    list.Add(Tuple.Create(compType, component));
                    compType = compType.BaseType;
                }
                while (compType != typeof(MonoBehaviour) && compType != typeof(Behaviour) && compType != typeof(Component));
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
            _world = world;
            _id = world.Create();
            _entity = _world.GetById(_id);

            if (_componentViews == null)
                Init();
            
            foreach (var view in _componentViews)
                view.AddToWorld(_world, _id);
            RegisterUnityComponents(_world);

            return _id;
        }

        private void RegisterUnityComponents(EcsWorld world)
        {
            foreach (var componentTuple in _componentsBuffer)
                world.AddReference(componentTuple.Item1, _id, componentTuple.Item2);
        }

        public bool Have<T>() => _world.Have<T>(_id);
        public void Add<T>() => _world.Add<T>(_id);
        public void Add<T>(T component) => _world.Add(_id, component);
        public void TryAdd<T>() => _world.TryAdd<T>(_id);
        public ref T GetOrAdd<T>() => ref _world.GetOrAddComponent<T>(_id);
        public ref T GetEcsComponent<T>() => ref _world.Get<T>(_id);
        public void Remove<T>() => _world.Remove<T>(_id);
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

#if UNITY_EDITOR && CODEX_ECS_EDITOR
        private bool _validationGuard;//hack to not loose values on inspector update in late update
        public void OnComponentValidate<T>(BaseComponentView view, T component)
        {
            if (_validationGuard)
                return;

            if (_world == null || !_world.IsEntityValid(_entity))
                return;

            _viewsByComponentType[typeof(T)] = view;
            if (component is IComponent)
                GetOrAdd<T>() = component;
        }

        public void OnComponentEnable<T>(BaseComponentView view, T component)
        {
            if (_world == null || !_world.IsEntityValid(_entity))
                return;

            _viewsByComponentType[typeof(T)] = view;
            if (component is IComponent)
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