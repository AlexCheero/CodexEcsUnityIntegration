using Components;
using ECS;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

//TODO: add fields to init EcsCacheSettings
public class ECSPipeline : MonoBehaviour
{
    private EcsWorld _world;
    
    private EcsSystem[] _initSystems;
    private EcsSystem[] _updateSystems;
    private EcsSystem[] _lateUpdateSystems;
    private EcsSystem[] _fixedUpdateSystems;
    private EcsSystem[] _lateFixedUpdateSystems;//use for physics cleanup
    private EcsSystem[] _enableSystems;
    private EcsSystem[] _disableSystems;

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

    public EcsWorld World => _world;

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
            default:
                throw new Exception("category not implemented: " + category.ToString());
        }
    }

    void Start()
    {
        _world = new EcsWorld();

        var systemCtorParams = new object[] { _world };

#if DEBUG
        _initSystems = CreateSystemsByNames(_initSystemTypeNames, systemCtorParams);
        _updateSystems = CreateSystemsByNames(_updateSystemTypeNames, systemCtorParams);
        _lateUpdateSystems = CreateSystemsByNames(_lateUpdateSystemTypeNames, systemCtorParams);
        _fixedUpdateSystems = CreateSystemsByNames(_fixedUpdateSystemTypeNames, systemCtorParams);
        _lateFixedUpdateSystems = CreateSystemsByNames(_lateFixedUpdateSystemTypeNames, systemCtorParams);
        _enableSystems = CreateSystemsByNames(_enableSystemTypeNames, systemCtorParams);
        _disableSystems = CreateSystemsByNames(_disableSystemTypeNames, systemCtorParams);
#else
        _initSystems = CreateSystemsByNames(_initSystemTypeNames, systemCtorParams)?.Where((n, i) => _initSwitches[i]).ToArray();
        _updateSystems = CreateSystemsByNames(_updateSystemTypeNames, systemCtorParams)?.Where((n, i) => _updateSwitches[i]).ToArray();
        _lateUpdateSystems = CreateSystemsByNames(_lateUpdateSystemTypeNames, systemCtorParams)?.Where((n, i) => _lateUpdateSwitches[i]).ToArray();
        _fixedUpdateSystems = CreateSystemsByNames(_fixedUpdateSystemTypeNames, systemCtorParams)?.Where((n, i) => _fixedUpdateSwitches[i]).ToArray();
        _lateFixedUpdateSystems = CreateSystemsByNames(_lateFixedUpdateSystemTypeNames, systemCtorParams)?.Where((n, i) => _lateFixedUpdateSwitches[i]).ToArray();
        _enableSystems = CreateSystemsByNames(_enableSystemTypeNames, systemCtorParams)?.Where((n, i) => _enableSwitches[i]).ToArray();
        _disableSystems = CreateSystemsByNames(_disableSystemTypeNames, systemCtorParams)?.Where((n, i) => _disableSwitches[i]).ToArray();
#endif

        foreach (var view in FindObjectsOfType<EntityView>())
            view.InitAsEntity(_world);

        //call init systems after initing all the start entities
#if DEBUG
        TickSystemCategory(_initSystems, _initSwitches);
#else
        TickSystemCategory(_initSystems);
#endif

        StartCoroutine(LateFixedUpdate());
    }

    public bool IsPaused { get; private set; }
    public void Unpause()
    {
        if (!IsPaused)
            return;

        IsPaused = false;
#if DEBUG
        TickSystemCategory(_enableSystems, _enableSwitches);
#else
        TickSystemCategory(_enableSystems);
#endif

        StartCoroutine(LateFixedUpdate());
    }

    public void Pause()
    {
        if (IsPaused)
            return;

        IsPaused = true;
#if DEBUG
        TickSystemCategory(_disableSystems, _disableSwitches, true);
#else
        TickSystemCategory(_disableSystems, true);
#endif

        StopAllCoroutines();
    }

    void Update()
    {
#if DEBUG
        TickSystemCategory(_updateSystems, _updateSwitches);
#else
        TickSystemCategory(_updateSystems);
#endif
    }

    void LateUpdate()
    {
#if DEBUG
        TickSystemCategory(_lateUpdateSystems, _lateUpdateSwitches);
#else
        TickSystemCategory(_lateUpdateSystems);
#endif
    }

    void FixedUpdate()
    {
#if DEBUG
        TickSystemCategory(_fixedUpdateSystems, _fixedUpdateSwitches);
#else
        TickSystemCategory(_fixedUpdateSystems);
#endif
    }

    private readonly WaitForFixedUpdate _waitForFixedUpdate = new WaitForFixedUpdate();
    private IEnumerator LateFixedUpdate()
    {
        while (true)
        {
            yield return _waitForFixedUpdate;
            if (!gameObject.activeInHierarchy)
                yield break;

#if DEBUG
            TickSystemCategory(_lateFixedUpdateSystems, _lateFixedUpdateSwitches);
#else
            TickSystemCategory(_lateFixedUpdateSystems);
#endif
        }
    }

#if DEBUG
    private void TickSystemCategory(EcsSystem[] systems, bool[] switches, bool forceTick = false)
#else
    private void TickSystemCategory(EcsSystem[] systems, bool forceTick = false)
#endif
    {
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            bool shouldReturn = !forceTick && IsPaused;
            if (shouldReturn)
                return;
#if DEBUG
            if (switches[i])
#endif
                systems[i].Tick(_world);
        }
    }

    private EcsSystem[] CreateSystemsByNames(string[] names, object[] systemCtorParams)
    {
        if (names == null || names.Length < 1)
            return null;

        var systems = new EcsSystem[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            var systemType = IntegrationHelper.GetTypeByName(names[i], EGatheredTypeCategory.System);
#if DEBUG
            if (systemType == null)
                throw new Exception("can't find system type " + names[i]);
#endif
            systems[i] = (EcsSystem)Activator.CreateInstance(systemType, systemCtorParams);
        }

        return systems;
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
