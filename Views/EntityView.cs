using Components;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tags;
using UnityEngine;

public class EntityView : MonoBehaviour
{
    private static MethodInfo WorldAddMethodInfo;
    private static Dictionary<Type, MethodInfo> GenericAddMethodInfos;
    private static List<Component> _componentsBuffer;

    static EntityView()
    {
        WorldAddMethodInfo = typeof(EcsWorld).GetMethod("Add");
        GenericAddMethodInfos = new Dictionary<Type, MethodInfo>();
        _componentsBuffer = new(32);

#if UNITY_EDITOR
        ViewRegistrator.Register();
#endif
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
    void Awake()
    {
        _componentViews = GetComponents<BaseComponentView>();
#if UNITY_EDITOR
        _viewsByComponentType = _componentViews.ToDictionary(view => view.GetEcsComponentType(), view => view);
        _typesToCheck = new HashSet<Type>(_viewsByComponentType.Keys);
        _typesBuffer = new HashSet<Type>(_viewsByComponentType.Keys);
#endif
    }

    public void ForceInit()
    {
        if (_componentViews == null || _componentViews.Length == 0)
            _componentViews = GetComponents<BaseComponentView>();

#if UNITY_EDITOR
        _viewsByComponentType ??= _componentViews.ToDictionary(view => view.GetEcsComponentType(), view => view);
        _typesToCheck ??= new HashSet<Type>(_viewsByComponentType.Keys);
        _typesBuffer ??= new HashSet<Type>(_viewsByComponentType.Keys);
#endif
    }

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        _world = world;
        _id = world.Create();
        _entity = _world.GetById(_id);

        foreach (var view in _componentViews)
            view.AddToWorld(_world, _id);
        GatherUnityComponents(_world);

        return _id;
    }

    private void GatherUnityComponents(EcsWorld world)
    {
        GetComponents(_componentsBuffer);//won't work for multithreading
        foreach (var unityComponent in _componentsBuffer)
        {
            if (unityComponent is BaseComponentView)
                continue;

            AddParams[0] = _id;
            AddParams[1] = unityComponent;
            var compType = unityComponent.GetType();
            do
            {
                AddUnityComponent(world, compType, AddParams);
                compType = compType.BaseType;
            }
            while (compType != typeof(MonoBehaviour) && compType != typeof(Behaviour) && compType != typeof(Component));
        }
    }

    private void AddUnityComponent(EcsWorld world, Type type, object[] addParams)
    {
        MethodInfo methodInfo;
        if (GenericAddMethodInfos.ContainsKey(type))
            methodInfo = GenericAddMethodInfos[type];
        else
        {
            methodInfo = WorldAddMethodInfo.MakeGenericMethod(type);
            GenericAddMethodInfos.Add(type, methodInfo);
        }

        methodInfo.Invoke(world, addParams);
    }

    public bool Have<T>() => _world.Have<T>(_id);
    public void Add<T>(T component = default) => _world.Add(_id, component);
    public ref T GetOrAdd<T>() => ref _world.GetOrAddComponent<T>(_id);
    public ref T GetEcsComponent<T>() => ref _world.GetComponent<T>(_id);
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
            contactPoint = collision.GetContact(0).point
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
        if (Have<TriggerFireComponent>())
        {
            if (Have<OverrideTriggerFire>())
                GetEcsComponent<TriggerFireComponent>().coliider = other;
        }
        else
        {
            Add(new TriggerFireComponent { coliider = other });
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
            var isComponent = typeof(IComponent).IsAssignableFrom(type);
            var isTag = typeof(ITag).IsAssignableFrom(type);
            if (!isComponent && !isTag)
                continue;

            if (!_viewsByComponentType.ContainsKey(type))
            {
                var viewType = ViewRegistrator.GetViewTypeByCompType(type);
                _validationGuard = true;
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
