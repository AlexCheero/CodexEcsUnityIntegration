
using System.Text;
using ECS;
using UnityEngine;

public static class WorldExtension
{
    public static void UnityDebugEntity(this EcsWorld world, string msg, Entity entity) => world.UnityDebugEntity(msg, entity.GetId());

    public static void UnityDebugEntity(this EcsWorld world, string msg, int id)
    {
        var sb = new StringBuilder(msg);
        world.DebugEntity(id, sb);
        Debug.Log(sb);
    }
}
