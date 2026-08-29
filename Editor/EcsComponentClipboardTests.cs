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
    internal sealed class ClipboardNestedData
    {
        public int Value;
        public int[] Values;
    }

    [Serializable]
    internal abstract class ClipboardPolymorphicData
    {
        public int BaseValue;
    }

    [Serializable]
    internal sealed class ClipboardDerivedData : ClipboardPolymorphicData
    {
        public string Text;
        public List<int> Values;
    }

    [Serializable]
    internal struct ClipboardRequired : IComponent
    {
        public int Value;
        private static ClipboardRequired Default => new() { Value = 30 };
        private static void OnAdded(ref ClipboardRequired instance, Object _) => instance.Value++;
    }

    [Serializable]
    [EcsRequireComponent(typeof(ClipboardRequired))]
    internal struct ClipboardValue : IComponent
    {
        public int Scalar;
        public ClipboardNestedData Nested;
        [SerializeReference] public ClipboardPolymorphicData Polymorphic;
        public GameObject Reference;
    }

    [Serializable]
    [EcsExcludeComponent(typeof(ClipboardValue))]
    internal struct ClipboardValueBlocker : IComponent { }

    public sealed class EcsComponentClipboardTests
    {
        private EntityPreset _preset;
        private SerializedObject _serializedPreset;
        private SerializedProperty _componentsProperty;
        private GameObject _referenceObject;

        [SetUp]
        public void SetUp()
        {
            EcsComponentClipboard.Clear();
            _referenceObject = new GameObject("ECS clipboard reference");
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
            EcsComponentClipboard.Clear();
            if (_preset != null)
                Object.DestroyImmediate(_preset);
            if (_referenceObject != null)
                Object.DestroyImmediate(_referenceObject);
        }

        [Test]
        public void ClipboardClone_DeepCopiesSerializedManagedDataAndPreservesUnityReferences()
        {
            var source = CreateSourceWrapper();
            Assert.IsTrue(EcsComponentClipboard.TryCopy(source, out var copyError), copyError);

            var sourceValue = source.Component;
            sourceValue.Nested.Value = 999;
            sourceValue.Nested.Values[0] = 999;
            ((ClipboardDerivedData)sourceValue.Polymorphic).Text = "mutated";
            ((ClipboardDerivedData)sourceValue.Polymorphic).Values[0] = 999;

            Assert.IsTrue(
                EcsComponentClipboard.TryCreateWrapperClone(out var clonedWrapper, out var cloneError),
                cloneError);
            var clone = ((ComponentWrapper<ClipboardValue>)clonedWrapper).Component;
            var cloneDerived = (ClipboardDerivedData)clone.Polymorphic;

            Assert.AreEqual(12, clone.Scalar);
            Assert.AreEqual(34, clone.Nested.Value);
            CollectionAssert.AreEqual(new[] { 5, 6, 7 }, clone.Nested.Values);
            Assert.AreEqual(56, cloneDerived.BaseValue);
            Assert.AreEqual("copied", cloneDerived.Text);
            CollectionAssert.AreEqual(new[] { 8, 9 }, cloneDerived.Values);
            Assert.AreSame(_referenceObject, clone.Reference);
            Assert.AreNotSame(sourceValue.Nested, clone.Nested);
            Assert.AreNotSame(sourceValue.Nested.Values, clone.Nested.Values);
            Assert.AreNotSame(sourceValue.Polymorphic, clone.Polymorphic);
            Assert.AreNotSame(
                ((ClipboardDerivedData)sourceValue.Polymorphic).Values,
                cloneDerived.Values);
        }

        [Test]
        public void PasteValues_UpdatesSameTypeComponentWithoutReplacingItsWrapper()
        {
            var source = CreateSourceWrapper();
            Assert.IsTrue(EcsComponentClipboard.TryCopy(source, out var copyError), copyError);
            AddExisting(new ClipboardValue
            {
                Scalar = -1,
                Nested = new ClipboardNestedData { Value = -2, Values = new[] { -3 } },
                Polymorphic = new ClipboardDerivedData { Text = "destination" }
            });
            var wrapperBeforePaste = _preset.Components[0];

            Assert.IsTrue(EcsComponentClipboard.PasteComponentValues(
                _preset,
                typeof(ClipboardValue)));

            var wrapperAfterPaste = _preset.Components[0];
            var pasted = ((ComponentWrapper<ClipboardValue>)wrapperAfterPaste).Component;
            Assert.AreSame(wrapperBeforePaste, wrapperAfterPaste);
            Assert.AreEqual(12, pasted.Scalar);
            Assert.AreEqual(34, pasted.Nested.Value);
            Assert.AreEqual("copied", ((ClipboardDerivedData)pasted.Polymorphic).Text);
            Assert.AreSame(_referenceObject, pasted.Reference);
        }

        [Test]
        public void PasteAsNew_AddsDefaultRequirementsThenCopiedComponentValues()
        {
            var source = CreateSourceWrapper();
            Assert.IsTrue(EcsComponentClipboard.TryCopy(source, out var copyError), copyError);

            Assert.IsTrue(EcsComponentClipboard.PasteComponentAsNew(_preset));

            CollectionAssert.AreEqual(
                new[] { typeof(ClipboardRequired), typeof(ClipboardValue) },
                _preset.Components.Select(component => component.GetComponentType()).ToArray());
            Assert.IsTrue(_preset.TryGetComponentDefaultValue(out ClipboardRequired required));
            Assert.AreEqual(31, required.Value);
            Assert.IsTrue(_preset.TryGetComponentDefaultValue(out ClipboardValue pasted));
            Assert.AreEqual(12, pasted.Scalar);
            Assert.AreEqual(34, pasted.Nested.Value);
            Assert.AreEqual("copied", ((ClipboardDerivedData)pasted.Polymorphic).Text);
            Assert.AreSame(_referenceObject, pasted.Reference);
        }

        [Test]
        public void PasteAsNew_UsesExclusionValidationWithoutPartiallyAddingRequirements()
        {
            AddExisting(new ClipboardValueBlocker());
            var source = CreateSourceWrapper();
            Assert.IsTrue(EcsComponentClipboard.TryCopy(source, out var copyError), copyError);
            var originalSize = _preset.Components.Count;
            LogAssert.Expect(
                LogType.Error,
                new Regex($".*{nameof(ClipboardValueBlocker)}.*{nameof(ClipboardValue)}.*"));

            Assert.IsFalse(EcsComponentClipboard.PasteComponentAsNew(_preset));

            Assert.AreEqual(originalSize, _preset.Components.Count);
            Assert.IsFalse(_preset.TryGetComponentDefaultValue(out ClipboardRequired _));
            Assert.IsFalse(_preset.TryGetComponentDefaultValue(out ClipboardValue _));
        }

        private ComponentWrapper<ClipboardValue> CreateSourceWrapper()
        {
            var wrapper = new ComponentWrapper<ClipboardValue>();
            wrapper.InitFromComponent(new ClipboardValue
            {
                Scalar = 12,
                Nested = new ClipboardNestedData
                {
                    Value = 34,
                    Values = new[] { 5, 6, 7 }
                },
                Polymorphic = new ClipboardDerivedData
                {
                    BaseValue = 56,
                    Text = "copied",
                    Values = new List<int> { 8, 9 }
                },
                Reference = _referenceObject
            });
            return wrapper;
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
    }
}
#endif
