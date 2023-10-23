using Components;
using ECS;
using Tags;
using UnityEngine;

//choose system type here
[System(ESystemCategory.LateUpdate)]
public class SynchTransformSystem : EcsSystem
{
    private readonly int _scaleFilterId;
    private readonly int _globalFilterId;
    private readonly int _localFilterId;

    public SynchTransformSystem(EcsWorld world)
    {
        _scaleFilterId = world.RegisterFilter(new BitMask(Id<EcsTransform>(), Id<ApplyEcsTransformScaleTag>(), Id<Transform>()));
        _localFilterId = world.RegisterFilter(new BitMask(Id<EcsTransform>(), Id<ApplyEcsLocalTransformTag>(), Id<Transform>()));
        _globalFilterId = world.RegisterFilter(new BitMask(Id<EcsTransform>(), Id<ApplyEcsTransformTag>(), Id<Transform>()));
    }

    public override void Tick(EcsWorld world)
    {
        foreach (var id in world.Enumerate(_scaleFilterId))
        {
            var transform = world.GetComponent<Transform>(id);
            var ecsTransform = world.GetComponent<EcsTransform>(id);
            transform.localScale = ecsTransform.scale;
            world.Remove<ApplyEcsTransformScaleTag>(id);
        }

        //TODO: what to apply first?
        foreach (var id in world.Enumerate(_localFilterId))
        {
            var transform = world.GetComponent<Transform>(id);
            var ecsTransform = world.GetComponent<EcsTransform>(id);
            transform.SetLocalPositionAndRotation(ecsTransform.LocalPosition, ecsTransform.LocalRotation);
            world.Remove<ApplyEcsLocalTransformTag>(id);
        }

        foreach (var id in world.Enumerate(_globalFilterId))
        {
            var transform = world.GetComponent<Transform>(id);
            var ecsTransform = world.GetComponent<EcsTransform>(id);
            transform.SetPositionAndRotation(ecsTransform.position, ecsTransform.rotation);
            world.Remove<ApplyEcsTransformTag>(id);
        }
    }
}