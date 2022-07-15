using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SystemsFeature", menuName = "ECS/New systems feature", order = -1)]
public class SystemsFeature : ScriptableObject
{
    [SerializeField]
    public ESystemCategory _category;
    [SerializeField]
    public string[] _systems = new string[0];

#if UNITY_EDITOR
    [SerializeField]
    public bool[] _switches = new bool[0];
#endif

#if UNITY_EDITOR
    public bool AddSystem(string systemName)
    {
        foreach (var sysName in _systems)
            if (systemName == sysName) return false;

        Array.Resize(ref _systems, _systems.Length + 1);
        _systems[_systems.Length - 1] = systemName;

        Array.Resize(ref _switches, _switches.Length + 1);
        _switches[_switches.Length - 1] = true; ;

        return true;
    }

    public void RemoveMetaAt(int idx)
    {
        var newLength = _systems.Length - 1;
        for (int i = idx; i < newLength; i++)
        {
            _systems[i] = _systems[i + 1];
            _switches[i] = _switches[i + 1];
        }
        Array.Resize(ref _systems, newLength);
    }

    public bool Move(int idx, bool up)
    {
        //var newIdx = up ? idx + 1 : idx - 1;
        //TODO: no idea why it works like that, but have to invert indices to move systems properly
        var newIdx = up ? idx - 1 : idx + 1;
        if (newIdx < 0 || newIdx > _systems.Length - 1)
            return false;

        var tempName = _systems[newIdx];
        _systems[newIdx] = _systems[idx];
        _systems[idx] = tempName;

        var tempSwitch = _switches[newIdx];
        _switches[newIdx] = _switches[idx];
        _switches[idx] = tempSwitch;

        return true;
    }
#endif
}
