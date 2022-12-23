using Components;
using ECS;
using System;
using System.Collections;
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
            default:
                throw new Exception("category not implemented: " + category.ToString());
        }
    }

#if UNITY_EDITOR
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
            default:
                throw new Exception("category not implemented: " + category.ToString());
        }
    }
#endif

    void Start()
    {
        _world = new EcsWorld();

        var systemCtorParams = new object[] { _world };
        _initSystems = CreateSystemsByNames(_initSystemTypeNames, systemCtorParams);
        _updateSystems = CreateSystemsByNames(_updateSystemTypeNames, systemCtorParams);
        _lateUpdateSystems = CreateSystemsByNames(_lateUpdateSystemTypeNames, systemCtorParams);
        _fixedUpdateSystems = CreateSystemsByNames(_fixedUpdateSystemTypeNames, systemCtorParams);
        _lateFixedUpdateSystems = CreateSystemsByNames(_lateFixedUpdateSystemTypeNames, systemCtorParams);

        foreach (var view in FindObjectsOfType<EntityView>(true))
            view.InitAsEntity(_world);

        //call init systems after initing all the start entities
#if UNITY_EDITOR
        TickSystemCategory(_initSystems, _initSwitches);
#else
        TickSystemCategory(_initSystems);
#endif

        StartCoroutine(LateFixedUpdate());
    }

    void Update()
    {
#if UNITY_EDITOR
        TickSystemCategory(_updateSystems, _updateSwitches);
#else
        TickSystemCategory(_updateSystems);
#endif
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        TickSystemCategory(_lateUpdateSystems, _lateUpdateSwitches);
#else
        TickSystemCategory(_lateUpdateSystems);
#endif
    }

    void FixedUpdate()
    {
#if UNITY_EDITOR
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

#if UNITY_EDITOR
            TickSystemCategory(_lateFixedUpdateSystems, _lateFixedUpdateSwitches);
#else
            TickSystemCategory(_lateFixedUpdateSystems);
#endif
        }
    }

#if UNITY_EDITOR
    private void TickSystemCategory(EcsSystem[] systems, bool[] switches)
#else
    private void TickSystemCategory(EcsSystem[] systems)
#endif
    {
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
#if UNITY_EDITOR
            if (switches[i])
#endif
                systems[i].Tick(_world);
        }
    }

    private EcsSystem[] CreateSystemsByNames(string[] names, object[] systemCtorParams)
    {
        if (names.Length < 1)
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
