using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    // Owned by one inspector and bound to a full entity generation, never a preset.
    internal sealed class RuntimeComponentEditBuffer : IDisposable
    {
        private readonly EcsWorld _world;
        private readonly int _entityId;
        private readonly Entity _entity;
        private readonly int _componentId;
        private readonly RuntimeComponentProxy _proxy;
        private IComponent _baseline;
        private int _dirtyCount;

        internal SerializedObject Serialized { get; }
        internal SerializedProperty ComponentProperty { get; }

        internal RuntimeComponentEditBuffer(EcsWorld world, int entityId, Type type)
        {
            _world = world;
            _entityId = entityId;
            _entity = world.GetById(entityId);
            _componentId = ComponentMapping.EnsureTypeRegistered(type);
            _proxy = ScriptableObject.CreateInstance<RuntimeComponentProxy>();
            _proxy.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor |
                               HideFlags.DontSaveInBuild;
            _proxy.Value = (ComponentWrapper)Activator.CreateInstance(
                typeof(ComponentWrapper<>).MakeGenericType(type));
            Read();
            Serialized = new SerializedObject(_proxy);
            ComponentProperty = Serialized.FindProperty("Value")
                .FindPropertyRelative(ComponentWrapper.ComponentPropertyName);
        }

        internal bool IsValid =>
            PureEntityEditorUtility.IsEntityReferenceValid(_world, _entityId, in _entity) &&
            _world.GetMask(_entityId).Check(_componentId);

        internal void Refresh()
        {
            // Object selectors and managed-reference menus can apply outside OnInspectorGUI.
            Commit();
            if (!IsValid)
                return;
            Read();
            Serialized.Update();
        }

        private void Read()
        {
            _proxy.Value.ReadFromWorld(_world, _entityId);
            _baseline = (IComponent)RuntimeComponentData.Clone(_proxy.Value.GetBoxedDefaultValue());
            _proxy.Value.InitFromComponent((IComponent)RuntimeComponentData.Clone(_baseline));
            _dirtyCount = EditorUtility.GetDirtyCount(_proxy);
        }

        internal bool Commit()
        {
            if (!IsValid)
                return false;
            var changed = Serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!changed && _dirtyCount == EditorUtility.GetDirtyCount(_proxy))
                return false;

            var edited = _proxy.Value.GetBoxedDefaultValue();
            _proxy.Value.ReadFromWorld(_world, _entityId);
            var live = _proxy.Value.GetBoxedDefaultValue();
            var merged = (IComponent)RuntimeComponentData.Merge(_baseline, edited, live);
            _proxy.Value.InitFromComponent(merged);
            _proxy.Value.WriteToWorld(_world, _entityId);
            Read();
            Serialized.Update();
            return true;
        }

        public void Dispose()
        {
            Serialized.Dispose();
            Object.DestroyImmediate(_proxy);
        }
    }

    // Clone only editable data. Nonserialized ECS handles/containers keep their live identity;
    // Unity object fields remain references, as they do in the ordinary Unity inspector.
    internal static class RuntimeComponentData
    {
        private static readonly MethodInfo MemberwiseClone = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<Type, FieldInfo[]> Fields = new();
        private static readonly Dictionary<Type, bool> ValueOnly = new();

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        internal static object Clone(object value) =>
            value == null || IsValueOnly(value.GetType()) ? value :
                Clone(value, new Dictionary<object, object>(ReferenceComparer.Instance));

        private static object Clone(object value, Dictionary<object, object> copies)
        {
            if (value == null || IsValueOnly(value.GetType()))
                return value;
            if (copies.TryGetValue(value, out var existing))
                return existing;
            if (value is AnimationCurve curve)
                return new AnimationCurve(curve.keys)
                    { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
            if (value is Gradient gradient)
            {
                var copy = new Gradient { mode = gradient.mode, colorSpace = gradient.colorSpace };
                copy.SetKeys(gradient.colorKeys, gradient.alphaKeys);
                return copy;
            }
            if (value is IList list)
            {
                var copy = value is Array array
                    ? (IList)array.Clone()
                    : (IList)Activator.CreateInstance(value.GetType());
                copies.Add(value, copy);
                for (var i = 0; i < list.Count; i++)
                {
                    var item = Clone(list[i], copies);
                    if (value is Array) copy[i] = item;
                    else copy.Add(item);
                }
                return copy;
            }
            var result = MemberwiseClone.Invoke(value, null);
            copies.Add(value, result);
            foreach (var field in GetFields(value.GetType()))
                field.SetValue(result, Clone(field.GetValue(value), copies));
            return result;
        }

        // Apply only changed authored fields to the latest component. A system may have
        // advanced other values while an object picker or context menu was open.
        internal static object Merge(object baseline, object edited, object live)
            => Merge(baseline, edited, live,
                new Dictionary<object, object>(ReferenceComparer.Instance));

        private static object Merge(object baseline, object edited, object live,
            Dictionary<object, object> merged)
        {
            if (Equal(baseline, edited, new HashSet<object>(ReferenceComparer.Instance)))
                return live;
            if (baseline == null || edited == null || live == null ||
                baseline.GetType() != edited.GetType() || live.GetType() != edited.GetType() ||
                IsAtomic(edited.GetType()) ||
                edited is AnimationCurve || edited is Gradient)
                return Clone(edited);

            if (merged.TryGetValue(baseline, out var existing)) return existing;
            if (edited is IList editedList)
            {
                var baselineList = (IList)baseline;
                var liveList = (IList)live;
                if (baselineList.Count != editedList.Count || liveList.Count != editedList.Count)
                    return Clone(edited);
                var listResult = live is Array array ? (IList)array.Clone() :
                    (IList)Activator.CreateInstance(live.GetType());
                merged.Add(baseline, listResult);
                for (var i = 0; i < editedList.Count; i++)
                {
                    var item = Merge(baselineList[i], editedList[i], liveList[i], merged);
                    if (live is Array) listResult[i] = item;
                    else listResult.Add(item);
                }
                return listResult;
            }
            var result = MemberwiseClone.Invoke(live, null);
            merged.Add(baseline, result);
            foreach (var field in GetFields(edited.GetType()))
            {
                var before = field.GetValue(baseline);
                var after = field.GetValue(edited);
                if (!Equal(before, after, new HashSet<object>(ReferenceComparer.Instance)))
                    field.SetValue(result, Merge(before, after, field.GetValue(live), merged));
            }
            return result;
        }

        private static bool Equal(object left, object right, HashSet<object> visited)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.GetType() != right.GetType()) return false;
            if (IsAtomic(left.GetType()) || left is AnimationCurve || left is Gradient)
                return left.Equals(right);
            if (!visited.Add(left)) return true;
            if (left is IList list)
            {
                var other = (IList)right;
                if (list.Count != other.Count) return false;
                for (var i = 0; i < list.Count; i++)
                    if (!Equal(list[i], other[i], visited)) return false;
                return true;
            }
            foreach (var field in GetFields(left.GetType()))
                if (!Equal(field.GetValue(left), field.GetValue(right), visited)) return false;
            return true;
        }

        private static bool IsAtomic(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(decimal) || typeof(Object).IsAssignableFrom(type);

        private static bool IsValueOnly(Type type)
        {
            if (IsAtomic(type)) return true;
            if (!type.IsValueType) return false;
            if (ValueOnly.TryGetValue(type, out var cached)) return cached;
            foreach (var field in GetFields(type))
                if (!IsValueOnly(field.FieldType)) return ValueOnly[type] = false;
            return ValueOnly[type] = true;
        }

        private static FieldInfo[] GetFields(Type type)
        {
            if (Fields.TryGetValue(type, out var fields)) return fields;
            var result = new List<FieldInfo>();
            for (var current = type; current != null; current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsInitOnly && EntityEditorHelper.IsUnitySerializableField(field))
                        result.Add(field);
            fields = result.ToArray();
            Fields.Add(type, fields);
            return fields;
        }
    }
}
