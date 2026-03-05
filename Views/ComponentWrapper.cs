using System;
using System.Runtime.CompilerServices;
using CodexECS;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{
    [Serializable]
    public abstract class ComponentWrapper
    {
        public abstract void AddToWorld(EcsWorld world, int id);
        public abstract Type GetComponentType();
        
#if UNITY_EDITOR
        public const string ComponentPropertyName = "_component";
        
        [NonSerialized]
        public bool IsExpanded;
        
        public abstract void InitFromComponent(IComponent component);
        public abstract void ReadFromWorld(EcsWorld world, int eid);
        public abstract void WriteToWorld(EcsWorld world, int eid);
#endif
    }
    
    [Serializable]
    public class ComponentWrapper<T> : ComponentWrapper where T : IComponent
    {
        [SerializeField]
        private T _component;
        public T Component
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _component;
        }

        public override void AddToWorld(EcsWorld world, int id)
        {
            ComponentMeta<T>.Init(ref _component);
            world.Add(id, _component);
        }
        
        public override Type GetComponentType() => typeof(T);

#if UNITY_EDITOR
        public override void InitFromComponent(IComponent component) => _component = (T)component;

        public override void ReadFromWorld(EcsWorld world, int eid)
        {
            if (!ComponentMeta<T>.IsTag)
                _component = world.Get<T>(eid);
        }

        public override void WriteToWorld(EcsWorld world, int eid)
        {
            if (ComponentMeta<T>.IsTag)
                return;
            ComponentMeta<T>.Init(ref _component);
            world.Get<T>(eid) = _component;
        }
#endif
    }
}