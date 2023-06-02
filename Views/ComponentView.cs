using ECS;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseComponentView : MonoBehaviour
{
    public abstract void AddToWorld(EcsWorld world, int id);

#if UNITY_EDITOR
    public abstract Type GetEcsComponentType();
    public abstract void UpdateFromWorld(EcsWorld world, int id);
#endif
}

public class ComponentView<T> : BaseComponentView
{
    public T Component;

    public override void AddToWorld(EcsWorld world, int id)
    {
        world.Add(id, Component);
    }

#if UNITY_EDITOR
    public override void UpdateFromWorld(EcsWorld world, int id)
    {
        var comp = world.GetComponent<T>(id);
        Component = comp;
    }

    private EntityView _owner;
    private EntityView Owner
    {
        get
        {
            _owner ??= GetComponent<EntityView>();
            return _owner;
        }
    }

    private bool _canValidate;//hack to validate only after game started and initialized
    void Start()
    {
        _canValidate = true;
    }

    //void OnEnable()
    //{
    //    if (Owner != null)
    //        Owner.OnComponentEnable(this, Component);
    //}

    void OnDisable()
    {
        _canValidate = false;
        if (Owner != null)
            Owner.OnComponentDisable<T>();
    }

    void OnValidate()
    {
        if (_canValidate)
            Owner.OnComponentValidate(this, Component);
    }

    public override Type GetEcsComponentType() => typeof(T);
#endif
}