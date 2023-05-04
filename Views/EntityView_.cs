using Components;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tags;
using UnityEngine;

public class EntityView_ : MonoBehaviour
{
    private static MethodInfo WorldAddMethodInfo;
    private static Dictionary<Type, MethodInfo> GenericAddMethodInfos;

    static EntityView_()
    {
        WorldAddMethodInfo = typeof(EcsWorld).GetMethod("Add");
        GenericAddMethodInfos = new Dictionary<Type, MethodInfo>();

#if UNITY_EDITOR
        ViewRegistrator.Register();
#endif
    }

    private BaseComponentView[] _componentViews;

    public Entity Entity { get; private set; }
    public EcsWorld World { get; private set; }
    public int Id { get => Entity.GetId(); }
    public int Version { get => Entity.GetVersion(); }
    public bool IsValid { get => World.IsEntityValid(Entity); }

    void Awake()
    {
        _componentViews = GetComponents<BaseComponentView>();
#if UNITY_EDITOR
        _viewsByComponentType = _componentViews.ToDictionary(view => view.GetEcsComponentType(), view => view);
        _typesToCheck = new HashSet<Type>(_viewsByComponentType.Keys);
        _typesBuffer = new HashSet<Type>(_viewsByComponentType.Keys);
#endif
    }

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        World = world;
        var entityId = world.Create();
        Entity = World.GetById(entityId);

        foreach (var view in _componentViews)
            view.AddToWorld(World, entityId);
        GatherUnityComponents(World);

        Add(GetComponent<Collider>());

        return entityId;
    }

    private void GatherUnityComponents(EcsWorld world)
    {
        var unityComponents = GetComponents<Component>().Where(comp => !(comp is BaseComponentView)).ToArray();
        foreach (var unityComponent in unityComponents)
        {
            var compType = IntegrationHelper.GetTypeByName(unityComponent.GetType().FullName, EGatheredTypeCategory.UnityObject);

            AddParams[0] = Id;
            AddParams[1] = unityComponent;

            MethodInfo methodInfo;
            if (GenericAddMethodInfos.ContainsKey(compType))
                methodInfo = GenericAddMethodInfos[compType];
            else
            {
                methodInfo = WorldAddMethodInfo.MakeGenericMethod(compType);
                GenericAddMethodInfos.Add(compType, methodInfo);
            }

            methodInfo.Invoke(world, AddParams);
        }
    }

    public bool Have<T>() => World.Have<T>(Id);
    public void Add<T>(T component = default) => World.Add(Id, component);
    public ref T GetOrAdd<T>() => ref World.GetOrAddComponent<T>(Id);
    public ref T GetEcsComponent<T>() => ref World.GetComponent<T>(Id);
    public void TryRemove<T>() => World.TryRemove<T>(Id);

    public void DeleteFromWorld() => World.Delete(Id);

    public void DeleteSelf()
    {
        World.Delete(Id);
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        var collidedView = EntityViewHelper.GetOwnerEntityView_(collision.gameObject);
        ProcessCollision(collidedView);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessCollision(EntityViewHelper.GetOwnerEntityView_(other));
    }

    private void ProcessCollision(EntityView_ collidedView)
    {
        if (collidedView != null)
        {
            AddCollisionComponents(this, collidedView.Entity);
            AddCollisionComponents(collidedView, Entity);
        }
        else
        {
            AddCollisionComponents(this, EntityExtension.NullEntity);
        }
    }

    private static void AddCollisionComponents(EntityView_ view, Entity otherEntity)
    {
        if (view.Have<CollisionWith>())
        {
            if (view.Have<OverrideCollision>())
                view.GetEcsComponent<CollisionWith>().entity = otherEntity;
        }
        else
        {
            view.Add(new CollisionWith { entity = otherEntity });
        }
    }

#if UNITY_EDITOR
    public void OnComponentValidate<T>(BaseComponentView view, T component)
    {
        if (World == null || !World.IsEntityValid(Entity))
            return;

        _viewsByComponentType[typeof(T)] = view;
        GetOrAdd<T>() = component;
    }

    public void OnComponentDestroy<T>()
    {
        if (World == null || !World.IsEntityValid(Entity))
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
        World.GetTypesForId(Id, _typesBuffer);
        foreach (var type in _typesBuffer)
        {
            var isComponent = typeof(IComponent).IsAssignableFrom(type);
            var isTag = typeof(ITag).IsAssignableFrom(type);
            if (!isComponent && !isTag)
                continue;

            if (!_viewsByComponentType.ContainsKey(type))
            {
                var viewType = ViewRegistrator.GetViewTypeByCompType(type);
                _viewsByComponentType[type] = gameObject.AddComponent(viewType) as BaseComponentView;
            }
            
            if (isComponent)
                _viewsByComponentType[type].UpdateFromWorld(World, Id);
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