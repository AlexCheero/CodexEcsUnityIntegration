using System;
using System.Collections.Generic;
using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [Serializable]
    public sealed class EntityGroupMask
    {
        [SerializeField] private bool _specified;
        // Persist type identities, never the component IDs assigned by this process.
        [SerializeField] private List<string> _componentTypes = new();
        [NonSerialized] private bool _resolved;
        [NonSerialized] private BitMask _mask;

        public BitMask Resolve(in BitMask initialComponents)
        {
            if (!_specified)
                return initialComponents;
            if (!_resolved)
            {
                _mask = new BitMask();
                for (var i = 0; i < _componentTypes.Count; i++)
                    _mask.Set(ComponentMapping.GetIdForType(Type.GetType(_componentTypes[i], true)));
                _resolved = true;
            }
            return _mask;
        }

#if UNITY_EDITOR
        public void Invalidate() => _resolved = false;
#endif
    }
}
