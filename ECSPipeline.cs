using CodexECS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//TODO: add fields to init EcsCacheSettings
public class ECSPipeline : MonoBehaviour
{
    private EcsWorld _world;

    private struct SystemData
    {
        public EcsSystem System;
        public bool Switch;
    }

    private Dictionary<ESystemCategory, Dictionary<Type, int>> _systemToIndexMapping;
    private Dictionary<ESystemCategory, EcsSystem[]> _systems;

    private EcsSystem[] GetSystemByCategory(ESystemCategory category) =>
        _systems.ContainsKey(category) ? _systems[category] : null;

    //TODO: same as for EntityView: define different access modifiers for UNITY_EDITOR
    [SerializeField]
    public string[] _initSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _updateSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _lateUpdateSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _fixedUpdateSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _lateFixedUpdateSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _enableSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _disableSystemTypeNames = new string[0];
    [SerializeField]
    public string[] _reactiveSystemTypeNames = new string[0];

    private ref string[] GetSystemTypeNamesByCategory(ESystemCategory category)
    {
        switch (category)
        {
            case ESystemCategory.Init:
                return ref _initSystemTypeNames;
            case ESystemCategory.Update:
                return ref _updateSystemTypeNames;
            case ESystemCategory.LateUpdate:
                return ref _lateUpdateSystemTypeNames;
            case ESystemCategory.FixedUpdate:
                return ref _fixedUpdateSystemTypeNames;
            case ESystemCategory.LateFixedUpdate:
                return ref _lateFixedUpdateSystemTypeNames;
            case ESystemCategory.OnEnable:
                return ref _enableSystemTypeNames;
            case ESystemCategory.OnDisable:
                return ref _disableSystemTypeNames;
            case ESystemCategory.Reactive:
                return ref _reactiveSystemTypeNames;
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

        _world.CallReactiveSystems();
    }

    private void CreateSystemsByNames(ESystemCategory category, object[] systemCtorParams)
    {
        var names = GetSystemTypeNamesByCategory(category);
        if (names == null || names.Length < 1)
            return;

        var systems = new EcsSystem[names.Length];

        _systemToIndexMapping[category] = new();
        for (int i = 0; i < names.Length; i++)
        {
            var systemType = IntegrationHelper.SystemTypes[names[i]];
            if (systemType == null)
                throw new Exception("can't find system type " + names[i]);
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
    public bool AddSystem(string systemName, ESystemCategory systemCategory)
    {
        ref var systemNames = ref GetSystemTypeNamesByCategory(systemCategory);
        ref var switches = ref GetSystemSwitchesByCategory(systemCategory);
        return AddSystem(systemName, ref systemNames, ref switches);
    }

    private bool AddSystem(string systemName, ref string[] systems, ref bool[] switches)
    {
        foreach (var sysName in systems)
            if (systemName == sysName) return false;

        Array.Resize(ref systems, systems.Length + 1);
        systems[systems.Length - 1] = systemName;

        Array.Resize(ref switches, switches.Length + 1);
        switches[switches.Length - 1] = true; ;

        return true;
    }

    public void RemoveMetaAt(ESystemCategory systemCategory, int idx)
    {
        ref var systemNames = ref GetSystemTypeNamesByCategory(systemCategory);
        ref var switches = ref GetSystemSwitchesByCategory(systemCategory);
        RemoveMetaAt(idx, ref systemNames, ref switches);
    }

    private void RemoveMetaAt(int idx, ref string[] systems, ref bool[] switches)
    {
        var newLength = systems.Length - 1;
        for (int i = idx; i < newLength; i++)
        {
            systems[i] = systems[i + 1];
            switches[i] = switches[i + 1];
        }
        Array.Resize(ref systems, newLength);
    }

    public bool Move(ESystemCategory systemCategory, int idx, bool up)
    {
        var systemNames = GetSystemTypeNamesByCategory(systemCategory);
        var switches = GetSystemSwitchesByCategory(systemCategory);
        return Move(idx, up, systemNames, switches);
    }

    private bool Move(int idx, bool up, string[] systems, bool[] switches)
    {
        //var newIdx = up ? idx + 1 : idx - 1;
        //TODO: no idea why it works like that, but have to invert indices to move systems properly
        var newIdx = up ? idx - 1 : idx + 1;
        if (newIdx < 0 || newIdx > systems.Length - 1)
            return false;

        var tempName = systems[newIdx];
        systems[newIdx] = systems[idx];
        systems[idx] = tempName;

        var tempSwitch = switches[newIdx];
        switches[newIdx] = switches[idx];
        switches[idx] = tempSwitch;

        return true;
    }
#endif
}
