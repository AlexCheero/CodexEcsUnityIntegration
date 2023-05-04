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

    public override void UpdateFromWorld(EcsWorld world, int id)
    {
        var comp = world.GetComponent<T>(id);
        Component = comp;
    }

#if UNITY_EDITOR
    private EntityView_ _owner;
    private EntityView_ Owner
    {
        get
        {
            _owner ??= GetComponent<EntityView_>();
            return _owner;
        }
    }

    void OnValidate() => Owner.OnComponentValidate(this, Component);

    void OnDestroy() => Owner.OnComponentDestroy<T>();

    public override Type GetEcsComponentType() => typeof(T);
#endif
}