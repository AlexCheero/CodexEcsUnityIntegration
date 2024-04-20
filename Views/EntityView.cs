using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    public class EntityView : MonoBehaviour
    {
        private List<Tuple<Type, Component>> _componentsBuffer;

        static EntityView()
        {
            ViewRegistrator.Register();
        }

        private BaseComponentView[] _componentViews;
        private EcsWorld _world;
        private Entity _entity;
        private int _id;

        public Entity Entity { get => _entity; private set => _entity = value; }
        public EcsWorld World { get => _world; private set => _world = value; }
        public int Id { get => _id; private set => _id = value; }
        public int Version { get => _entity.GetVersion(); }
        public bool IsValid { get => _world != null && _world.IsEntityValid(_entity); }

        //TODO: maybe move to void OnValidate()
        void Awake() => Init();
        public void Init()
        {
            if (_componentViews == null || _componentViews.Length == 0)
                _componentViews = GetComponents<BaseComponentView>();
            if (_componentsBuffer == null || _componentsBuffer.Count == 0)
                _componentsBuffer = GatherUnityComponents();

#if UNITY_EDITOR
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
                    if (!ComponentTypeToIdMapping.Mapping.ContainsKey(compType))
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
            specificType.TypeInitializer?.Invoke(null, null);
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
        public void Add<T>(T component = default) => _world.Add(_id, component);
        public ref T GetOrAdd<T>() => ref _world.GetOrAddComponent<T>(_id);
        public ref T GetEcsComponent<T>() => ref _world.GetComponent<T>(_id);
        public void Remove<T>() => _world.Remove<T>(_id);
        public void TryRemove<T>() => _world.TryRemove<T>(_id);

        public void DeleteFromWorld() => _world.Delete(_id);

        void OnDestroy()
        {
            if (_world != null && _world.IsEntityValid(_entity))
                _world.Delete(_id);
        }

        void OnCollisionEnter(Collision collision)
        {
            var collisionComponent = new CollisionComponent
            {
                collider = collision.collider,
                contactPoint = collision.GetContact(0).point,
                rb = collision.rigidbody
            };
            if (Have<CollisionComponent>())
            {
                if (Have<OverrideCollision>())
                    GetEcsComponent<CollisionComponent>() = collisionComponent;
            }
            else
            {
                Add(collisionComponent);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Have<TriggerEnterComponent>())
            {
                if (Have<OverrideTriggerEnter>())
                    GetEcsComponent<TriggerEnterComponent>().collider = other;
            }
            else
            {
                Add(new TriggerEnterComponent { collider = other });
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (Have<TriggerExitComponent>())
            {
                if (Have<OverrideTriggerExit>())
                    GetEcsComponent<TriggerExitComponent>().collider = other;
            }
            else
            {
                Add(new TriggerExitComponent { collider = other });
            }
        }

#if UNITY_EDITOR
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
            else if (component is ITag && !Have<T>())
                Add<T>();
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
                var isTag = typeof(ITag).IsAssignableFrom(type);
                if (!isComponent && !isTag)
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

                if (isComponent)
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