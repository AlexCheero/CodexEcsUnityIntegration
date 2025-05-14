using CodexECS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace CodexFramework.CodexEcsUnityIntegration
{

    //TODO: add fields to init EcsCacheSettings
    public class ECSPipeline : MonoBehaviour
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
            bool SynchScriptsWithTypeNames(SystemEntry[] systems)
            {
                var isDirty = false;
                for (var i = 0; i < systems.Length; i++)
                {
                    isDirty |= !systems[i].Name.Equals(systems[i].Script.GetClass().FullName);
                    systems[i].Name = systems[i].Script.GetClass().Name;
                }
                return isDirty;
            }

            var isDirty = SynchScriptsWithTypeNames(_initSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_updateSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_lateUpdateSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_fixedUpdateSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_lateFixedUpdateSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_enableSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_disableSystemScripts);
            isDirty |= SynchScriptsWithTypeNames(_reactiveSystemScripts);

            // if (isDirty)
            // {
            //     EditorUtility.SetDirty(this);
            // }
        }
#endif

        private EcsWorld _world;

        private Dictionary<ESystemCategory, Dictionary<Type, int>> _systemToIndexMapping;
        private Dictionary<ESystemCategory, EcsSystem[]> _systems;

        private EcsSystem[] GetSystemByCategory(ESystemCategory category) =>
            _systems.ContainsKey(category) ? _systems[category] : null;

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
            _world = world;
            var systemCtorParams = new object[] { _world };

            _systemToIndexMapping = new();
            _systems = new();

            foreach (var systemCategory in (ESystemCategory[])Enum.GetValues(typeof(ESystemCategory)))
                CreateSystemsByNames(systemCategory, systemCtorParams);
        }

        public void Switch(bool on)
        {
            gameObject.SetActive(on);
            if (!on)
                return;

            RunInitSystems();
            StartLateFixedUpdateSystemsIfAny();
        }

        public void RunInitSystems()
        {
            TickSystemCategory(ESystemCategory.Init);
            foreach (var systemCategory in (ESystemCategory[])Enum.GetValues(typeof(ESystemCategory)))
                InitSystemCategory(systemCategory);
        }

        public bool IsPaused { get; private set; }
        public void Unpause()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            TickSystemCategory(ESystemCategory.OnEnable);
            StartLateFixedUpdateSystemsIfAny();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
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
            var systems = GetSystemByCategory(category);
            var systemScripts = GetSystemScriptsByCategory(category);
#if DEBUG
            if (systems != null && systems.Length != systemScripts.Length)
                throw new Exception("systems and switches desynch");
#endif
            if (systems == null || systems.Length == 0)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                if (systemScripts[i].Active)
                    systems[i].Init(_world);
            }
        }
        
        private void TickSystemCategory(ESystemCategory category, bool forceTick = false)
        {
            var systems = GetSystemByCategory(category);
            var systemScripts = GetSystemScriptsByCategory(category);
#if DEBUG
            if (systems != null && systems.Length != systemScripts.Length)
                throw new Exception("systems and switches desynch");
#endif
            if (systems == null || systems.Length == 0)
                return;

            var isPaused = !forceTick && IsPaused;
            for (int i = 0; i < systems.Length; i++)
            {
                if (isPaused && !systemScripts[i].NonPausable)
                    continue;
                if (systemScripts[i].Active)
                    systems[i].Tick(_world);
            }
        }

        private void CreateSystemsByNames(ESystemCategory category, object[] systemCtorParams)
        {
            var scripts = GetSystemScriptsByCategory(category);
            if (scripts == null || scripts.Length < 1)
                return;

            var systems = new EcsSystem[scripts.Length];

            _systemToIndexMapping[category] = new();
            for (int i = 0; i < scripts.Length; i++)
            {
                var systemType = IntegrationHelper.SystemTypes[scripts[i].Name];
                if (systemType == null)
                    throw new Exception("can't find system type " + scripts[i].Name);
                _systemToIndexMapping[category][systemType] = i;
                systems[i] = (EcsSystem)Activator.CreateInstance(systemType, systemCtorParams);
            }

            _systems[category] = systems;
        }

        public void SwitchSystem<T>(ESystemCategory category, bool on) where T : EcsSystem
        {
            var systemIndex = _systemToIndexMapping[category][typeof(T)];
            var scripts = GetSystemScriptsByCategory(category);
            scripts[systemIndex].Active = on;
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