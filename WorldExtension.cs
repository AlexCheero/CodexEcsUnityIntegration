using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class WorldExtension
    {
#if DEBUG
        public static void UnityDebugEntity(this EcsWorld world, Entity entity, bool printFields, string msg = "") =>
            world.UnityDebugEntity(entity.GetId(), printFields, msg);

        public static void UnityDebugEntity(this EcsWorld world, int id, bool printFields, string msg = "") =>
            Debug.Log(msg + world.DebugEntity(id, printFields));
#endif
    }
}