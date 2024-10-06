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
        private EcsWorld _world;

        private Dictionary<ESystemCategory, Dictionary<Type, int>> _systemToIndexMapping;
        private Dictionary<ESystemCategory, EcsSystem[]> _systems;

        private EcsSystem[] GetSystemByCategory(ESystemCategory category) =>
            _systems.ContainsKey(category) ? _systems[category] : null;

        //TODO: same as for EntityView: define different access modifiers for UNITY_EDITOR
        [SerializeField]
        public MonoScript[] _initSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _updateSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _lateUpdateSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _fixedUpdateSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _lateFixedUpdateSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _enableSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _disableSystemScripts = Array.Empty<MonoScript>();
        [SerializeField]
        public MonoScript[] _reactiveSystemScripts = Array.Empty<MonoScript>();

        private ref MonoScript[] GetSystemScriptsByCategory(ESystemCategory category)
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

        [SerializeField]
        public bool[] _initSwitches = new bool[0];
        [SerializeField]
        public bool[] _updateSwitches = new bool[0];
        [SerializeField]
        public bool[] _lateUpdateSwitches = new bool[0];
        [SerializeField]
        public bool[] _fixedUpdateSwitches = new bool[0];
        [SerializeField]
        public bool[] _lateFixedUpdateSwitches = new bool[0];
        [SerializeField]
        public bool[] _enableSwitches = new bool[0];
        [SerializeField]
        public bool[] _disableSwitches = new bool[0];
        [SerializeField]
        public bool[] _reactiveSwitches = new bool[0];

        private ref bool[] GetSystemSwitchesByCategory(ESystemCategory category)
        {
            switch (category)
            {
                case ESystemCategory.Init:
                    return ref _initSwitches;
                case ESystemCategory.Update:
                    return ref _updateSwitches;
                case ESystemCategory.LateUpdate:
                    return ref _lateUpdateSwitches;
                case ESystemCategory.FixedUpdate:
                    return ref _fixedUpdateSwitches;
                case ESystemCategory.LateFixedUpdate:
                    return ref _lateFixedUpdateSwitches;
                case ESystemCategory.OnEnable:
                    return ref _enableSwitches;
                case ESystemCategory.OnDisable:
                    return ref _disableSwitches;
                case ESystemCategory.Reactive:
                    return ref _reactiveSwitches;
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

            CreateSystemsByNames(ESystemCategory.Init, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.Update, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.LateUpdate, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.FixedUpdate, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.LateFixedUpdate, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.OnEnable, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.OnDisable, systemCtorParams);
            CreateSystemsByNames(ESystemCategory.Reactive, systemCtorParams);
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
            TickSystemCategory(GetSystemByCategory(ESystemCategory.Init), _initSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.Update), _updateSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.LateUpdate), _lateUpdateSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.FixedUpdate), _fixedUpdateSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.LateFixedUpdate), _lateFixedUpdateSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.OnEnable), _enableSwitches);
            InitSystemCategory(GetSystemByCategory(ESystemCategory.OnDisable), _disableSwitches);
        }

        public bool IsPaused { get; private set; }
        public void Unpause()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            TickSystemCategory(GetSystemByCategory(ESystemCategory.OnEnable), _enableSwitches);
            StartLateFixedUpdateSystemsIfAny();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            TickSystemCategory(GetSystemByCategory(ESystemCategory.OnDisable), _disableSwitches, true);
            StopAllCoroutines();
        }

        void Update()
        {
            TickSystemCategory(GetSystemByCategory(ESystemCategory.Update), _updateSwitches);
        }

        void LateUpdate()
        {
            TickSystemCategory(GetSystemByCategory(ESystemCategory.LateUpdate), _lateUpdateSwitches);
        }

        void FixedUpdate()
        {
            TickSystemCategory(GetSystemByCategory(ESystemCategory.FixedUpdate), _fixedUpdateSwitches);
        }

        private bool StartLateFixedUpdateSystemsIfAny()
        {
            var shouldStart = _lateFixedUpdateSwitches.Length > 0 && _lateFixedUpdateSwitches.Any(systemSwitch => systemSwitch);
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

                TickSystemCategory(GetSystemByCategory(ESystemCategory.LateFixedUpdate), _lateFixedUpdateSwitches);
            }
        }

        private void InitSystemCategory(EcsSystem[] systems, bool[] switches)
        {
            if (systems == null)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                if (switches[i])
                    systems[i].Init(_world);
            }
        }

        private void TickSystemCategory(EcsSystem[] systems, bool[] switches, bool forceTick = false)
        {
            if (systems == null)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                bool shouldReturn = !forceTick && IsPaused && systems[i].IsPausable;
                if (shouldReturn)
                    continue;
                if (switches[i])
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
                var systemType = scripts[i].GetClass();
                if (systemType == null)
                    throw new Exception("can't find system type " + scripts[i]);
                _systemToIndexMapping[category][systemType] = i;
                systems[i] = (EcsSystem)Activator.CreateInstance(systemType, systemCtorParams);
            }

            _systems[category] = systems;
        }

        public void SwitchSystem<T>(ESystemCategory category, bool on) where T : EcsSystem
        {
            var systemIndex = _systemToIndexMapping[category][typeof(T)];
            var switches = GetSystemSwitchesByCategory(category);
            switches[systemIndex] = on;
        }

#if UNITY_EDITOR
        public bool AddSystem(MonoScript script, ESystemCategory systemCategory)
        {
            ref var scripts = ref GetSystemScriptsByCategory(systemCategory);
            ref var switches = ref GetSystemSwitchesByCategory(systemCategory);
            return AddSystem(script, ref scripts, ref switches);
        }

        private bool AddSystem(MonoScript newScript, ref MonoScript[] scripts, ref bool[] switches)
        {
            foreach (var script in scripts)
                if (newScript == script) return false;

            Array.Resize(ref scripts, scripts.Length + 1);
            scripts[^1] = newScript;

            Array.Resize(ref switches, switches.Length + 1);
            switches[^1] = true; ;

            return true;
        }

        public void RemoveMetaAt(ESystemCategory systemCategory, int idx)
        {
            ref var scripts = ref GetSystemScriptsByCategory(systemCategory);
            ref var switches = ref GetSystemSwitchesByCategory(systemCategory);
            RemoveMetaAt(idx, ref scripts, ref switches);
        }

        private void RemoveMetaAt(int idx, ref MonoScript[] scripts, ref bool[] switches)
        {
            var newLength = scripts.Length - 1;
            for (int i = idx; i < newLength; i++)
            {
                scripts[i] = scripts[i + 1];
                switches[i] = switches[i + 1];
            }
            Array.Resize(ref scripts, newLength);
        }

        public bool Move(ESystemCategory systemCategory, int idx, bool up)
        {
            var scripts = GetSystemScriptsByCategory(systemCategory);
            var switches = GetSystemSwitchesByCategory(systemCategory);
            return Move(idx, up, scripts, switches);
        }

        private bool Move(int idx, bool up, MonoScript[] scripts, bool[] switches)
        {
            //var newIdx = up ? idx + 1 : idx - 1;
            //TODO: no idea why it works like that, but have to invert indices to move systems properly
            var newIdx = up ? idx - 1 : idx + 1;
            if (newIdx < 0 || newIdx > scripts.Length - 1)
                return false;

            (scripts[newIdx], scripts[idx]) = (scripts[idx], scripts[newIdx]);

            (switches[newIdx], switches[idx]) = (switches[idx], switches[newIdx]);

            return true;
        }
#endif
    }
}