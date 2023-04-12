
using ECS;
using UnityEngine;

public static class WorldExtension
{
#if DEBUG
    public static void UnityDebugEntity(this EcsWorld world, Entity entity, string msg = "") => world.UnityDebugEntity(entity.GetId(), msg);
    public static void UnityDebugEntity(this EcsWorld world, int id, string msg = "") => Debug.Log(msg + world.DebugEntity(id));
#endif

    public static int GetRandomEntity(this EcsWorld world, int filterId) => world.GetNthEntityFromFilter(filterId, Random.Range(0, world.EntitiesCount(filterId)));
}
