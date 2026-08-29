using System;
using System.Reflection;
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
        public abstract int GetComponentId();
        public abstract IComponent GetBoxedDefaultValue();
        
#if UNITY_EDITOR
        public const string ComponentPropertyName = "_component";
        
        [NonSerialized]
        public bool IsExpanded;
        
        public abstract void InitFromComponent(IComponent component);
        public abstract void OnAdded(UnityEngine.Object owner);
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
            if (!world.Have<T>(id))
            {
                world.Add(id, _component);
                return;
            }

            if (!ComponentMeta<T>.IsTag)
                world.Replace(id, _component);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Type GetComponentType() => typeof(T);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetComponentId() => ComponentMeta<T>.Id;

        /// <summary>
        /// Exposes the serialized prototype value without requiring callers to reflect into
        /// the private generic field. Value-type components are intentionally boxed here;
        /// this path is for startup inspection, not frame-time ECS access.
        /// </summary>
        public override IComponent GetBoxedDefaultValue() => _component;

#if UNITY_EDITOR
        private static void DefaultOnAdded(ref T instance, UnityEngine.Object owner) { }
        private delegate void OnAddedDelegate(ref T instance, UnityEngine.Object owner);
        private static readonly OnAddedDelegate _onAdded;

        static ComponentWrapper()
        {
            var onAddedMethod = typeof(T).GetMethod(nameof(OnAdded),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (onAddedMethod != null)
                _onAdded = (OnAddedDelegate)Delegate.CreateDelegate(typeof(OnAddedDelegate), onAddedMethod);
            else
                _onAdded = DefaultOnAdded;
        }

        public override void InitFromComponent(IComponent component) => _component = (T)component;

        public override void OnAdded(UnityEngine.Object owner) => _onAdded(ref _component, owner);

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
            world.Replace(eid, _component);
        }
#endif
    }
}
