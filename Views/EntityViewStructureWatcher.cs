using UnityEditor;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
#if UNITY_EDITOR
    public static class EntityViewStructureWatcher
    {
        [InitializeOnLoadMethod]
        static void Subscribe() => ObjectChangeEvents.changesPublished += OnChanges;

        private static void OnChanges(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                // if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectStructure)
                //     continue;

                stream.GetChangeGameObjectStructureEvent(i, out var change);
                var obj = EditorUtility.InstanceIDToObject(change.instanceId);

                if (obj is GameObject go)
                {
                    var view = go.GetComponent<EntityView>();
                    if (view != null)
                        view.RebuildUnityComponentsCache();
                }
            }
        }
    }
#endif
}