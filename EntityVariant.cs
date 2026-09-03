using System;
using CodexFramework.Utils.Pools;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [Serializable]
    public struct EntityVariant
    {
        [SerializeField]
        private bool _usePreset;
        [SerializeField]
        private EntityView _view;
        [SerializeField]
        private EntityPreset _preset;

        public bool IsPreset => _usePreset;
        public EntityView View => _view;
        public EntityPreset Preset => _preset;
        public bool IsAssigned => _usePreset ? _preset != null : _view != null;

        public EntityVariant(EntityView view)
        {
            _usePreset = false;
            _view = view;
            _preset = null;
        }

        public EntityVariant(EntityPreset preset)
        {
            _usePreset = true;
            _view = null;
            _preset = preset;
        }
    }

    [Serializable]
    public struct PooledEntityVariant
    {
        [SerializeField]
        private bool _usePreset;
        [SerializeField]
        private PooledEntityView _view;
        [SerializeField]
        private EntityPreset _preset;

        public bool IsPreset => _usePreset;
        public PooledEntityView View => _view;
        public EntityPreset Preset => _preset;
        public bool IsAssigned => _usePreset ? _preset != null : _view != null;

        public PooledEntityVariant(PooledEntityView view)
        {
            _usePreset = false;
            _view = view;
            _preset = null;
        }

        public PooledEntityVariant(EntityPreset preset)
        {
            _usePreset = true;
            _view = null;
            _preset = preset;
        }
    }
}
