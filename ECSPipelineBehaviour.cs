using CodexECS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    //TODO: add fields to init EcsCacheSettings
    public class ECSPipelineBehaviour : MonoBehaviour
    {
        [Serializable]
        public struct SystemEntry
        {
#if UNITY_EDITOR
            public MonoScript Script;
#endif
            public string Name;
            public bool Active;
            public bool NonPausable;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var isDirty = false;

            void ValidateEntries(ref SystemEntry[] systems)
            {
                systems ??= Array.Empty<SystemEntry>();
                var previousLength = systems.Length;
                Utils.RemoveEntries(ref systems, entry =>
                    entry.Script == null || entry.Script.GetClass() == null);
                isDirty |= systems.Length != previousLength;

                for (var i = 0; i < systems.Length; i++)
                {
                    var typeName = systems[i].Script.GetClass().FullName;
                    if (string.Equals(systems[i].Name, typeName, StringComparison.Ordinal))
                        continue;

                    systems[i].Name = typeName;
                    isDirty = true;
                }
            }

            ValidateEntries(ref _initSystemScripts);
            ValidateEntries(ref _updateSystemScripts);
            ValidateEntries(ref _lateUpdateSystemScripts);
            ValidateEntries(ref _fixedUpdateSystemScripts);
            ValidateEntries(ref _lateFixedUpdateSystemScripts);
            ValidateEntries(ref _enableSystemScripts);
            ValidateEntries(ref _disableSystemScripts);
            ValidateEntries(ref _reactiveSystemScripts);

            if (isDirty)
                EditorUtility.SetDirty(this);
        }
#endif

        private Dictionary<ESystemCategory, Dictionary<Type, int>> _systemToIndexMapping;
        private Dictionary<ESystemCategory, EcsPipeline<ESystemCategory>.Registration[]> _registrations;
        private EcsPipeline<ESystemCategory> _pipeline;

        private void OnDestroy()
        {
            _pipeline?.Dispose();
            _pipeline = null;
        }

        private EcsPipeline<ESystemCategory>.Registration[] GetRegistrationsByCategory(
            ESystemCategory category) =>
            _registrations != null && _registrations.TryGetValue(category, out var registrations)
                ? registrations
                : null;

        //TODO: same as for EntityView: define different access modifiers for UNITY_EDITOR
        [SerializeField]
        public SystemEntry[] _initSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _updateSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _lateUpdateSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _fixedUpdateSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _lateFixedUpdateSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _enableSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _disableSystemScripts = Array.Empty<SystemEntry>();
        [SerializeField]
        public SystemEntry[] _reactiveSystemScripts = Array.Empty<SystemEntry>();

        public ref SystemEntry[] GetSystemScriptsByCategory(ESystemCategory category)
        {
            switch (category)
            {
                case ESystemCategory.Init:
                    return ref _initSystemScripts;
                case ESystemCategory.Update:
                    return ref _updateSystemScripts;
                case ESystemCategory.LateUpdate:
                    return ref _lateUpdateSystemScripts;
                case ESystemCategory.FixedUpdate:
                    return ref _fixedUpdateSystemScripts;
                case ESystemCategory.LateFixedUpdate:
                    return ref _lateFixedUpdateSystemScripts;
                case ESystemCategory.OnEnable:
                    return ref _enableSystemScripts;
                case ESystemCategory.OnDisable:
                    return ref _disableSystemScripts;
                case ESystemCategory.Reactive:
                    return ref _reactiveSystemScripts;
                default:
                    throw new Exception("category not implemented: " + category.ToString());
            }
        }

        public void Init(EcsWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _pipeline?.Dispose();
            _pipeline = new EcsPipeline<ESystemCategory>(
                world,
                IntegrationHelper.SystemCategories);
            _systemToIndexMapping = new();
            _registrations = new();

            foreach (var systemCategory in IntegrationHelper.SystemCategories)
                CreateSystemsByNames(systemCategory);

            IsPaused = _pipeline.IsPaused;
        }

        public void Switch(bool on)
        {
            gameObject.SetActive(on);
            if (!on)
                return;

            RunInitSystems();
            StartLateFixedUpdateSystemsIfAny();
        }

        public void RunInitSystems(bool force = false)
        {
            TickSystemCategory(ESystemCategory.Init, force);
            foreach (var systemCategory in IntegrationHelper.SystemCategories)
                InitSystemCategory(systemCategory);
        }

        public bool IsPaused { get; private set; }
        public void Unpause()
        {
            if (!IsPaused)
                return;

            EnsureInitialized().Unpause();
            IsPaused = _pipeline.IsPaused;
            TickSystemCategory(ESystemCategory.OnEnable);
            StartLateFixedUpdateSystemsIfAny();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            EnsureInitialized().Pause();
            IsPaused = _pipeline.IsPaused;
            TickSystemCategory(ESystemCategory.OnDisable, true);
            StopAllCoroutines();
        }

        void Update()
        {
            TickSystemCategory(ESystemCategory.Update);
        }

        void LateUpdate()
        {
            TickSystemCategory(ESystemCategory.LateUpdate);
        }

        void FixedUpdate()
        {
            TickSystemCategory(ESystemCategory.FixedUpdate);
        }

        private bool StartLateFixedUpdateSystemsIfAny()
        {
            var shouldStart = _lateFixedUpdateSystemScripts.Length > 0 && _lateFixedUpdateSystemScripts.Any(system => system.Active);
            if (shouldStart)
                StartCoroutine(LateFixedUpdate());
            return shouldStart;
        }

        private readonly WaitForFixedUpdate _waitForFixedUpdate = new WaitForFixedUpdate();
        private IEnumerator LateFixedUpdate()
        {
            while (true)
            {
                yield return _waitForFixedUpdate;
                if (!gameObject.activeInHierarchy)
                    yield break;

                TickSystemCategory(ESystemCategory.LateFixedUpdate);
            }
        }

        private void InitSystemCategory(ESystemCategory category)
        {
            var registrations = GetRegistrationsByCategory(category);
            if (registrations == null || registrations.Length == 0)
                return;

            SyncRegistrationStates(category, registrations);
            EnsureInitialized().Initialize(category);
        }
        
        private void TickSystemCategory(ESystemCategory category, bool forceTick = false)
        {
            var registrations = GetRegistrationsByCategory(category);
            if (registrations == null || registrations.Length == 0)
                return;

            SyncRegistrationStates(category, registrations);
            EnsureInitialized().Tick(category, forceTick);
        }

        private void SyncRegistrationStates(
            ESystemCategory category,
            EcsPipeline<ESystemCategory>.Registration[] registrations)
        {
            var scripts = GetSystemScriptsByCategory(category);
            if (scripts == null || scripts.Length != registrations.Length)
            {
                throw new InvalidOperationException(
                    $"ECSPipeline '{name}' category {category} changed after initialization. " +
                    "Its serialized system-entry count must remain stable while running.");
            }

            for (var i = 0; i < registrations.Length; i++)
            {
                registrations[i].Active = scripts[i].Active;
                registrations[i].NonPausable = scripts[i].NonPausable;
            }
        }

        private void CreateSystemsByNames(ESystemCategory category)
        {
            ref var scripts = ref GetSystemScriptsByCategory(category);
            scripts ??= Array.Empty<SystemEntry>();
            if (scripts.Length == 0)
                return;

            var registrations = new EcsPipeline<ESystemCategory>.Registration[scripts.Length];
            _systemToIndexMapping[category] = new();
            for (var i = 0; i < scripts.Length; i++)
            {
                var entry = scripts[i];
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    throw new InvalidOperationException(
                        $"ECSPipeline '{name}' category {category} entry {i} has no serialized " +
                        $"{nameof(SystemEntry.Name)}. Open and resave the pipeline in the Unity " +
                        "Editor so its MonoScript can repair this legacy entry.");
                }

                if (!IntegrationHelper.SystemTypes.TryGetValue(entry.Name, out var systemType) ||
                    systemType == null)
                {
                    throw new InvalidOperationException(
                        $"ECSPipeline '{name}' category {category} entry {i} cannot resolve ECS " +
                        $"system type '{entry.Name}'. The name must equal Type.FullName and the " +
                        "type must be included in a Unity player assembly.");
                }

                var entryIndex = i;
                var systemName = entry.Name;
                _systemToIndexMapping[category][systemType] = i;
                registrations[i] = EnsureInitialized().Register(
                    category,
                    pipelineWorld => CreateSystem(
                        systemType,
                        pipelineWorld,
                        category,
                        entryIndex,
                        systemName),
                    entry.Active,
                    entry.NonPausable);
            }

            _registrations[category] = registrations;
        }

        private EcsSystem CreateSystem(
            Type systemType,
            EcsWorld world,
            ESystemCategory category,
            int entryIndex,
            string systemName)
        {
            try
            {
                return (EcsSystem)Activator.CreateInstance(systemType, world);
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocationException &&
                            invocationException.InnerException != null
                    ? invocationException.InnerException
                    : exception;
                throw new InvalidOperationException(
                    $"ECSPipeline '{name}' failed to construct '{systemName}' in category " +
                    $"{category} at entry {entryIndex}. Systems require a public constructor " +
                    "accepting one EcsWorld argument.",
                    cause);
            }
        }

        private EcsPipeline<ESystemCategory> EnsureInitialized() =>
            _pipeline ?? throw new InvalidOperationException(
                $"ECSPipeline '{name}' must be initialized with {nameof(Init)} before use.");

        public void SwitchSystem<T>(ESystemCategory category, bool on) where T : EcsSystem
        {
            if (_systemToIndexMapping == null ||
                !_systemToIndexMapping.TryGetValue(category, out var typeToIndex) ||
                !typeToIndex.TryGetValue(typeof(T), out var systemIndex))
            {
                throw new InvalidOperationException(
                    $"System '{typeof(T).FullName}' is not registered in ECSPipeline '{name}' " +
                    $"category {category}.");
            }

            var scripts = GetSystemScriptsByCategory(category);
            var registrations = GetRegistrationsByCategory(category);
            if (scripts == null || registrations == null ||
                systemIndex >= scripts.Length || systemIndex >= registrations.Length)
            {
                throw new InvalidOperationException(
                    $"ECSPipeline '{name}' category {category} registrations are out of sync.");
            }

            scripts[systemIndex].Active = on;
            registrations[systemIndex].Active = on;
        }

#if UNITY_EDITOR
        public bool AddSystem(MonoScript script, ESystemCategory systemCategory)
        {
            ref var scripts = ref GetSystemScriptsByCategory(systemCategory);
            return AddSystem(script, ref scripts);
        }

        private bool AddSystem(MonoScript newScript, ref SystemEntry[] systemEntries)
        {
            foreach (var systemEntry in systemEntries)
                if (newScript == systemEntry.Script) return false;

            Array.Resize(ref systemEntries, systemEntries.Length + 1);
            systemEntries[^1] = new SystemEntry { Script = newScript, Active = true };

            return true;
        }

        public void RemoveMetaAt(ESystemCategory systemCategory, int idx)
        {
            ref var scripts = ref GetSystemScriptsByCategory(systemCategory);
            RemoveMetaAt(idx, ref scripts);
        }

        private void RemoveMetaAt(int idx, ref SystemEntry[] scripts)
        {
            var newLength = scripts.Length - 1;
            for (int i = idx; i < newLength; i++)
                scripts[i] = scripts[i + 1];
            Array.Resize(ref scripts, newLength);
        }
#endif
    }
}
