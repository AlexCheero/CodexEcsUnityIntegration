#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor.Tests
{
    [Serializable]
    internal struct ConstraintLeaf : IComponent
    {
        public int Value;
        private static ConstraintLeaf Default => new() { Value = 17 };

        private static void OnAdded(ref ConstraintLeaf instance, Object owner) =>
            instance.Value += owner == null ? 0 : 1;
    }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintLeaf))]
    internal struct ConstraintMiddle : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintMiddle))]
    internal struct ConstraintRoot : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintCycleB))]
    internal struct ConstraintCycleA : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintCycleA))]
    internal struct ConstraintCycleB : IComponent { }

    [Serializable]
    [EcsExcludeComponent(typeof(ConstraintBlocked))]
    internal struct ConstraintBlocker : IComponent { }

    [Serializable]
    internal struct ConstraintBlocked : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintBlocked))]
    internal struct ConstraintRequiresBlocked : IComponent { }

    [Serializable]
    [EcsExcludeComponent(typeof(ConstraintBlocker))]
    internal struct ConstraintRejectsBlocker : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(string))]
    internal struct ConstraintInvalidRequirement : IComponent { }

    [Serializable]
    internal struct ConstraintRollbackDependency : IComponent { }

    [Serializable]
    [EcsRequireComponent(typeof(ConstraintRollbackDependency))]
    internal struct ConstraintThrowingRuntimeComponent : IComponent
    {
        public int Value;

        private static void Init(ref ConstraintThrowingRuntimeComponent _) =>
            throw new InvalidOperationException("constraint runtime add failure");
    }

    public sealed class EcsComponentInspectorUtilityTests
    {
        private EntityPreset _preset;
        private SerializedObject _serializedPreset;
        private SerializedProperty _componentsProperty;

        [SetUp]
        public void SetUp()
        {
            _preset = ScriptableObject.CreateInstance<EntityPreset>();
            _serializedPreset = new SerializedObject(_preset);
            _serializedPreset.Update();
            _componentsProperty = _serializedPreset.FindProperty(EntityPreset.ComponentsPropertyName);
            _componentsProperty.ClearArray();
            _serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            _serializedPreset.Update();
        }

        [TearDown]
        public void TearDown()
        {
            if (_preset != null)
                Object.DestroyImmediate(_preset);
        }

        [Test]
        public void AdditionPlan_AddsRecursiveRequirementsBeforeRequestedComponent()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintRoot),
                Array.Empty<Type>(),
                plan,
                out var error);

            Assert.IsTrue(success, error);
            CollectionAssert.AreEqual(
                new[] { typeof(ConstraintLeaf), typeof(ConstraintMiddle), typeof(ConstraintRoot) },
                plan);
        }

        [Test]
        public void AdditionPlan_TraversesRequirementsOfAnAlreadyExistingDependency()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintRoot),
                new[] { typeof(ConstraintMiddle) },
                plan,
                out var error);

            Assert.IsTrue(success, error);
            CollectionAssert.AreEqual(new[] { typeof(ConstraintLeaf), typeof(ConstraintRoot) }, plan);
        }

        [Test]
        public void AdditionPlan_CyclicRequirementsAddEveryTypeOnce()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintCycleA),
                Array.Empty<Type>(),
                plan,
                out var error);

            Assert.IsTrue(success, error);
            CollectionAssert.AreEqual(new[] { typeof(ConstraintCycleB), typeof(ConstraintCycleA) }, plan);
        }

        [Test]
        public void AdditionPlan_RejectsExistingAndIncomingExclusionsInEitherDirection()
        {
            var plan = new List<Type>();

            Assert.IsFalse(EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintBlocked),
                new[] { typeof(ConstraintBlocker) },
                plan,
                out var existingExcludesError));
            StringAssert.Contains(nameof(ConstraintBlocker), existingExcludesError);
            Assert.IsEmpty(plan);

            Assert.IsFalse(EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintRejectsBlocker),
                new[] { typeof(ConstraintBlocker) },
                plan,
                out var incomingExcludesError));
            StringAssert.Contains(nameof(ConstraintRejectsBlocker), incomingExcludesError);
            Assert.IsEmpty(plan);
        }

        [Test]
        public void AdditionPlan_RejectsAConflictingRequiredComponentAtomically()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintRequiresBlocked),
                new[] { typeof(ConstraintBlocker) },
                plan,
                out var error);

            Assert.IsFalse(success);
            StringAssert.Contains(nameof(ConstraintBlocked), error);
            Assert.IsEmpty(plan);
        }

        [Test]
        public void AdditionPlan_ReportsInvalidRequirementMetadata()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(ConstraintInvalidRequirement),
                Array.Empty<Type>(),
                plan,
                out var error);

            Assert.IsFalse(success);
            StringAssert.Contains(nameof(IComponent), error);
            Assert.IsEmpty(plan);
        }

        [Test]
        public void AdditionPlan_RejectsInternalReactiveMarker()
        {
            var plan = new List<Type>();

            var success = EcsComponentInspectorUtility.TryBuildAdditionPlan(
                typeof(MatchReact),
                Array.Empty<Type>(),
                plan,
                out var error);

            Assert.IsFalse(success);
            StringAssert.Contains("internal reactive marker", error);
            Assert.IsEmpty(plan);
        }

        [Test]
        public void OfflineAdd_AppendsRequiredWrappersWithDefaultAndOnAddedValues()
        {
            var success = EcsComponentInspectorUtility.TryAddSerializedComponents(
                _componentsProperty,
                _preset.Components,
                _preset,
                typeof(ConstraintRoot));
            _serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(success);
            CollectionAssert.AreEqual(
                new[] { typeof(ConstraintLeaf), typeof(ConstraintMiddle), typeof(ConstraintRoot) },
                _preset.Components.Select(component => component.GetComponentType()).ToArray());
            Assert.IsTrue(_preset.TryGetComponentDefaultValue(out ConstraintLeaf leaf));
            Assert.AreEqual(18, leaf.Value);
        }

        [Test]
        public void OfflineAdd_LogsAndLeavesSerializedListUntouchedOnConflict()
        {
            AddExisting(new ConstraintBlocker());
            var originalSize = _componentsProperty.arraySize;
            LogAssert.Expect(
                LogType.Error,
                new Regex($".*{nameof(ConstraintBlocker)}.*{nameof(ConstraintBlocked)}.*"));

            var success = EcsComponentInspectorUtility.TryAddSerializedComponents(
                _componentsProperty,
                _preset.Components,
                _preset,
                typeof(ConstraintRequiresBlocked));

            Assert.IsFalse(success);
            Assert.AreEqual(originalSize, _componentsProperty.arraySize);
        }

        [Test]
        public void RuntimeAdd_UsesTheSameRequirementAndExclusionRules()
        {
            var gameObject = new GameObject("EcsComponentConstraintRuntimeTest");
            try
            {
                var view = gameObject.AddComponent<EntityView>();
                InitializeEmptyComponentList(view);
                view.RebuildUnityComponentsCache();
                var world = new EcsWorld();
                view.InitAsEntity(world);

                Assert.IsTrue(EcsComponentInspectorUtility.TryAddRuntimeComponents(
                    view,
                    typeof(ConstraintRoot)));
                Assert.IsTrue(view.Have<ConstraintLeaf>());
                Assert.IsTrue(view.Have<ConstraintMiddle>());
                Assert.IsTrue(view.Have<ConstraintRoot>());
                Assert.AreEqual(17, view.Get<ConstraintLeaf>().Value);

                view.Add<ConstraintBlocker>();
                LogAssert.Expect(
                    LogType.Error,
                    new Regex($".*{nameof(ConstraintBlocker)}.*{nameof(ConstraintBlocked)}.*"));
                Assert.IsFalse(EcsComponentInspectorUtility.TryAddRuntimeComponents(
                    view,
                    typeof(ConstraintRequiresBlocked)));
                Assert.IsFalse(view.Have<ConstraintBlocked>());
                Assert.IsFalse(view.Have<ConstraintRequiresBlocked>());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeAdd_RollsBackRequirementsWhenAComponentInitializerThrows()
        {
            var gameObject = new GameObject("EcsComponentConstraintRollbackTest");
            try
            {
                var view = gameObject.AddComponent<EntityView>();
                InitializeEmptyComponentList(view);
                view.RebuildUnityComponentsCache();
                var world = new EcsWorld();
                view.InitAsEntity(world);
                LogAssert.Expect(
                    LogType.Exception,
                    new Regex(".*constraint runtime add failure.*"));

                Assert.IsFalse(EcsComponentInspectorUtility.TryAddRuntimeComponents(
                    view,
                    typeof(ConstraintThrowingRuntimeComponent)));

                Assert.IsFalse(view.Have<ConstraintRollbackDependency>());
                Assert.IsFalse(view.Have<ConstraintThrowingRuntimeComponent>());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private void AddExisting<T>(T component) where T : IComponent
        {
            var index = _componentsProperty.arraySize;
            _componentsProperty.InsertArrayElementAtIndex(index);
            var wrapper = new ComponentWrapper<T>();
            wrapper.InitFromComponent(component);
            _componentsProperty.GetArrayElementAtIndex(index).managedReferenceValue = wrapper;
            _serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            _serializedPreset.Update();
        }

        private static void InitializeEmptyComponentList(EntityView view)
        {
            var serializedView = new SerializedObject(view);
            serializedView.Update();
            serializedView.FindProperty(EntityView.ComponentsPropertyName).ClearArray();
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
