using Components;
using ECS;
using System;
using System.Reflection;
using Tags;
using UnityEngine;

public class EntityView : MonoBehaviour
{
    public Entity Entity { get; private set; }
    private EcsWorld _world;
    public int Id { get => Entity.GetId(); }
    public int Version { get => Entity.GetVersion(); }

#if DEBUG
    public bool IsValid { get => _world.IsEntityValid(Entity); }
#endif

    [SerializeField]
    public EntityMeta Data = new EntityMeta { Metas = new ComponentMeta[0] };

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        _world = world;

        var entityId = _world.Create();
        Entity = _world.GetById(entityId);

        MethodInfo addMethodInfo = typeof(EcsWorld).GetMethod("Add");

        foreach (var meta in Data.Metas)
        {
            Type compType;
            object componentObj;
            if (meta.UnityComponent != null)
            {
                compType = meta.UnityComponent.GetType();
                componentObj = meta.UnityComponent;
            }
            else
            {
                compType = IntegrationHelper.GetTypeByName(meta.ComponentName, EGatheredTypeCategory.EcsComponent);
#if DEBUG
                if (compType == null)
                    throw new Exception("can't find component type " + meta.ComponentName);
#endif
                componentObj = Activator.CreateInstance(compType);

                foreach (var field in meta.Fields)
                {
                    var fieldInfo = compType.GetField(field.Name);
                    var defaultValueAttribute = fieldInfo.GetCustomAttribute<DefaultValue>();
                    object defaultValue = defaultValueAttribute?.Value;
                    var value = field.IsHiddenInEditor ? defaultValue : field.GetValue();
                    if (value == null)
                        continue;

                    fieldInfo.SetValue(componentObj, value);
                }
            }
            AddParams[0] = Id;
            AddParams[1] = componentObj;

            MethodInfo genAddMethodInfo = addMethodInfo.MakeGenericMethod(compType);
            genAddMethodInfo.Invoke(_world, AddParams);
        }

        return entityId;
    }

    public bool Have<T>() => _world.Have<T>(Id);
    public ref T AddAndReturnRef<T>(T component = default) => ref _world.AddAndReturnRef(Id, component);
    public void Add<T>(T component = default) => _world.Add<T>(Id, component);
    public T GetEcsComponent<T>() => _world.GetComponent<T>(Id);
    public ref T GetEcsComponentByRef<T>() => ref _world.GetComponentByRef<T>(Id);
    public void CopyFromEntity(Entity from) => _world.CopyComponents(from, Entity);

    public void DeleteSelf()
    {
        _world.Delete(Id);
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        var collidedView = collision.gameObject.GetComponent<EntityView>();
        ProcessCollision(collidedView);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessCollision(other.GetComponent<EntityView>());
    }

    private void ProcessCollision(EntityView collidedView)
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

    private static void AddCollisionComponents(EntityView view, Entity otherEntity)
    {
        if (view.Have<CollisionWith>())
        {
            if (view.Have<OverrideCollision>())
                view.GetEcsComponentByRef<CollisionWith>().entity = otherEntity;
        }
        else
        {
            view.Add(new CollisionWith { entity = otherEntity });
        }
    }
}
