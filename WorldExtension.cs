using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class WorldExtension
    {
#if DEBUG
        public static void UnityDebugEntity(this EcsWorld world, Entity entity, string msg = "") => world.UnityDebugEntity(entity.GetId(), msg);
        public static void UnityDebugEntity(this EcsWorld world, int id, string msg = "") => Debug.Log(msg + world.DebugEntity(id));
#endif
    }
}