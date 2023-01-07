
using System.Text;
using ECS;
using UnityEngine;

public static class WorldExtension
{
    public static void UnityDebugEntity(this EcsWorld world, Entity entity, string msg = "") => world.UnityDebugEntity(entity.GetId(), msg);

    public static void UnityDebugEntity(this EcsWorld world, int id, string msg = "")
    {
        var sb = new StringBuilder(msg);
        world.DebugEntity(id, sb);
        Debug.Log(sb);
    }
}
