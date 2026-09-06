using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal static class RuntimeEntityPresetUtility
    {
        internal static bool TryApplyComponent(EcsWorld world, int entityId, in Entity entity,
            Type componentType, out string error)
        {
            error = null;
            if (!EntityPreset.TryGetSourcePreset(world, entityId, in entity, out var preset))
            {
                error = "The runtime entity no longer has a valid source preset.";
                return false;
            }
            if (!world.GetMask(entityId).Check(ComponentMapping.EnsureTypeRegistered(componentType)))
            {
                error = $"The runtime entity no longer has {componentType.Name}.";
                return false;
            }

            var proxy = ScriptableObject.CreateInstance<RuntimeComponentProxy>();
            proxy.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var snapshotProxy = ScriptableObject.CreateInstance<RuntimeComponentProxy>();
            snapshotProxy.hideFlags = proxy.hideFlags;
            try
            {
                var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
                proxy.Value = (ComponentWrapper)Activator.CreateInstance(wrapperType);
                proxy.Value.ReadFromWorld(world, entityId);
                var json = EditorJsonUtility.ToJson(proxy);
                // A separate host is essential: FromJsonOverwrite can reuse a host's
                // existing managed references, including their nonserialized live state.
                EditorJsonUtility.FromJsonOverwrite(json, snapshotProxy);
                var snapshot = snapshotProxy.Value;

                using var serializedPreset = new SerializedObject(preset);
                var components = serializedPreset.FindProperty(EntityPreset.ComponentsPropertyName);
                for (var i = 0; i < components.arraySize; i++)
                {
                    var property = components.GetArrayElementAtIndex(i);
                    if (property.managedReferenceValue is not ComponentWrapper wrapper ||
                        wrapper.GetComponentType() != componentType)
                        continue;

                    Undo.RecordObject(preset, $"Apply {componentType.Name} to Preset");
                    wrapper.InitFromComponent(snapshot.GetBoxedDefaultValue());
                    property.managedReferenceValue = wrapper;
                    serializedPreset.ApplyModifiedPropertiesWithoutUndo();
                    Save(preset);
                    return true;
                }

                // A component added during play can be applied explicitly too, using the
                // same dependency/constraint rules as adding it in the preset inspector.
                if (!EcsComponentInspectorUtility.TryAddSerializedComponents(
                        components, preset.Components, preset, componentType, snapshot))
                {
                    error = $"Could not add {componentType.Name} to the source preset; see the Console.";
                    return false;
                }
                serializedPreset.ApplyModifiedProperties();
                Save(preset);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(proxy);
                UnityEngine.Object.DestroyImmediate(snapshotProxy);
            }
        }

        private static void Save(EntityPreset preset)
        {
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }
    }
}
