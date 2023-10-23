using Components;
using ECS;
using UnityEngine;

//choose system type here
[System(ESystemCategory.LateUpdate)]
public class SynchTransformSystem : EcsSystem
{
    private readonly int _filterId;

    public SynchTransformSystem(EcsWorld world)
    {
        _filterId = world.RegisterFilter(new BitMask(Id<EcsTransform>(), Id<Transform>()));
    }

    public override void Tick(EcsWorld world)
    {
        foreach (var id in world.Enumerate(_filterId))
        {
            var transform = world.GetComponent<Transform>(id);
            var ecsTransform = world.GetComponent<EcsTransform>(id);

            //TODO: what to apply first?
            transform.SetLocalPositionAndRotation(ecsTransform.LocalPosition, ecsTransform.LocalRotation);
            transform.SetPositionAndRotation(ecsTransform.position, ecsTransform.rotation);
        }
    }
}