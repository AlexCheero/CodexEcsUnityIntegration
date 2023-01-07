using Components;
using ECS;
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

    public int InitAsEntity(EcsWorld world)
    {
        _world = world;
#if UNITY_EDITOR
        var entityId = IntegrationHelper.InitAsEntity(world, Data, this);
#else
        var entityId = IntegrationHelper.InitAsEntity(world, Data);
#endif
        Entity = _world.GetById(entityId);
        return entityId;
    }

    public bool Have<T>() => _world.Have<T>(Id);
    public void Add<T>(T component = default) => _world.Add<T>(Id, component);
    public ref T GetEcsComponent<T>() => ref _world.GetComponent<T>(Id);
    public void CopyFromEntity(Entity from) => _world.CopyComponents(from, Entity);

    public void DeleteFromWorld() => _world.Delete(Id);
    
    public void DeleteSelf()
    {
        _world.Delete(Id);
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        var collidedView = EntityViewHelper.GetOwnerEntityView(collision.gameObject);
        ProcessCollision(collidedView);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessCollision(EntityViewHelper.GetOwnerEntityView(other));
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
                view.GetEcsComponent<CollisionWith>().entity = otherEntity;
        }
        else
        {
            view.Add(new CollisionWith { entity = otherEntity });
        }
    }
}
