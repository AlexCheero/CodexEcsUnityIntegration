using System;
using System.Collections.Generic;
using UnityEngine;
using SystemEntry = CodexFramework.CodexEcsUnityIntegration.ECSPipelineBehaviour.SystemEntry;

namespace CodexFramework.CodexEcsUnityIntegration
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ECS/Systems Override")]
    public sealed class SystemsOverride : MonoBehaviour
    {
        [Serializable]
        public sealed class SystemSelection
        {
            public SystemEntry[] _initSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _updateSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _lateUpdateSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _fixedUpdateSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _lateFixedUpdateSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _enableSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _disableSystemScripts = Array.Empty<SystemEntry>();
            public SystemEntry[] _reactiveSystemScripts = Array.Empty<SystemEntry>();

            public SystemEntry[] GetSystemScriptsByCategory(ESystemCategory category) => category switch
            {
                ESystemCategory.Init => _initSystemScripts,
                ESystemCategory.Update => _updateSystemScripts,
                ESystemCategory.LateUpdate => _lateUpdateSystemScripts,
                ESystemCategory.FixedUpdate => _fixedUpdateSystemScripts,
                ESystemCategory.LateFixedUpdate => _lateFixedUpdateSystemScripts,
                ESystemCategory.OnEnable => _enableSystemScripts,
                ESystemCategory.OnDisable => _disableSystemScripts,
                ESystemCategory.Reactive => _reactiveSystemScripts,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        [SerializeField] private SystemSelection _systemsToAdd = new();
        [SerializeField] private SystemSelection _systemsToRemove = new();

        public SystemSelection SystemsToAdd => _systemsToAdd;
        public SystemSelection SystemsToRemove => _systemsToRemove;

        // Called by the controller before constructing systems or running Init.
        internal void ApplyTo(ECSPipelineBehaviour pipeline)
        {
            foreach (var category in IntegrationHelper.SystemCategories)
            {
                var additions = _systemsToAdd.GetSystemScriptsByCategory(category);
                var removals = _systemsToRemove.GetSystemScriptsByCategory(category);
                if (additions.Length == 0 && removals.Length == 0)
                    continue;

                ref var entries = ref pipeline.GetSystemScriptsByCategory(category);
                var result = new List<SystemEntry>(entries);
                foreach (var removal in removals)
                    result.RemoveAll(entry => entry.Name == removal.Name);
                foreach (var addition in additions)
                {
                    var index = result.FindIndex(entry => entry.Name == addition.Name);
                    if (index >= 0)
                        result[index] = addition;
                    else
                        result.Add(addition);
                }
                entries = result.ToArray();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var category in IntegrationHelper.SystemCategories)
            {
                UpdateNames(_systemsToAdd.GetSystemScriptsByCategory(category));
                UpdateNames(_systemsToRemove.GetSystemScriptsByCategory(category));
            }
        }

        private static void UpdateNames(SystemEntry[] entries)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                var type = entries[i].Script != null ? entries[i].Script.GetClass() : null;
                entries[i].Name = type?.FullName;
            }
        }
#endif
    }
}
