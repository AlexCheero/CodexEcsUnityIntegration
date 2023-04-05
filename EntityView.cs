using Components;
using ECS;
using Tags;
using UnityEngine;

public class EntityView : MonoBehaviour
{
    public Entity Entity { get; private set; }
    public EcsWorld World { get; private set; }
    public int Id { get => Entity.GetId(); }
    public int Version { get => Entity.GetVersion(); }

#if DEBUG
    public bool IsValid { get => World.IsEntityValid(Entity); }
#endif

    [SerializeField]
    public EntityMeta Data = new EntityMeta { Metas = new ComponentMeta[0] };

    public int InitAsEntity(EcsWorld world)
    {
        World = world;
#if DEBUG
        var entityId = IntegrationHelper.InitAsEntity(world, Data, this);
#else
        var entityId = IntegrationHelper.InitAsEntity(world, Data);
#endif
        Entity = World.GetById(entityId);
        return entityId;
    }

    public bool Have<T>() => World.Have<T>(Id);
    public void Add<T>(T component = default) => World.Add<T>(Id, component);
    public ref T GetEcsComponent<T>() => ref World.GetComponent<T>(Id);
    public void CopyFromEntity(Entity from) => World.CopyComponents(from, Entity);

    public void DeleteFromWorld() => World.Delete(Id);
    
    public void DeleteSelf()
    {
        World.Delete(Id);
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
