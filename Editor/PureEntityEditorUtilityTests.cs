#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using CodexFramework.CodexEcsUnityIntegration.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor.Tests
{
    public sealed class PureEntityEditorUtilityTests
    {
        private struct Required : IComponent { }
        private struct Excluded : IComponent { }

        [EcsRequireComponent(typeof(Required))]
        private struct RequiresRequired : IComponent { }

        private struct Initialized : IComponent
        {
            public int Value;

            private static Initialized Default => new() { Value = 40 };
            private static void Init(ref Initialized instance) => instance.Value++;
        }

        [Serializable]
        private sealed class EditableSettings
        {
            public int Amount;
            public int Tick;
            [NonSerialized] public int RuntimeTick;
        }

        [Serializable]
        private struct Editable : IComponent
        {
            public int Value;
            public int Tick;
            public EditableSettings Settings;
            public List<EditableSettings> Items;
            public UnityEngine.Object Reference;
            [NonSerialized] public HashSet<int> RuntimeCache;
            internal static int InitCalls;
            internal static int CleanupCalls;
            private static void Init(ref Editable value) => InitCalls++;
            private static void Cleanup(ref Editable value) => CleanupCalls++;
        }

        [Test]
        public void RuntimeInspector_EditIsIsolatedFromPresetAndSiblingEntity()
        {
            var preset = ScriptableObject.CreateInstance<EntityPreset>();
            try
            {
                var wrapper = new ComponentWrapper<Editable>();
                wrapper.InitFromComponent(new Editable
                {
                    Value = 10,
                    Settings = new EditableSettings { Amount = 20 },
                    Items = new List<EditableSettings> { new() { Amount = 30 } },
                    Reference = preset,
                    RuntimeCache = new HashSet<int> { 7 }
                });
                typeof(EntityPreset).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(preset, new List<ComponentWrapper> { wrapper });
                var world = new EcsWorld();
                var selected = preset.CreatePureEntity(world);
                var sibling = preset.CreatePureEntity(world);
                var initCalls = Editable.InitCalls;
                var cleanupCalls = Editable.CleanupCalls;
                var runtimeCache = world.Get<Editable>(selected).RuntimeCache;
                Assert.AreSame(wrapper.Component.Settings, world.Get<Editable>(selected).Settings);

                using var buffer = new RuntimeComponentEditBuffer(world, selected, typeof(Editable));
                buffer.ComponentProperty.FindPropertyRelative("Value").intValue = 11;
                buffer.ComponentProperty.FindPropertyRelative("Settings.Amount").intValue = 21;
                buffer.ComponentProperty.FindPropertyRelative("Items").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("Amount").intValue = 31;
                Assert.IsTrue(buffer.Commit());

                Assert.AreEqual(11, world.Get<Editable>(selected).Value);
                Assert.AreEqual(21, world.Get<Editable>(selected).Settings.Amount);
                Assert.AreEqual(31, world.Get<Editable>(selected).Items[0].Amount);
                Assert.AreEqual(10, wrapper.Component.Value);
                Assert.AreEqual(20, wrapper.Component.Settings.Amount);
                Assert.AreEqual(30, wrapper.Component.Items[0].Amount);
                Assert.AreEqual(20, world.Get<Editable>(sibling).Settings.Amount);
                Assert.AreEqual(30, world.Get<Editable>(sibling).Items[0].Amount);
                Assert.AreSame(preset, world.Get<Editable>(selected).Reference);
                Assert.AreSame(runtimeCache, world.Get<Editable>(selected).RuntimeCache);
                Assert.AreEqual(initCalls, Editable.InitCalls);
                Assert.AreEqual(cleanupCalls, Editable.CleanupCalls);
            }
            finally { UnityEngine.Object.DestroyImmediate(preset); }
        }

        [Test]
        public void RuntimeInspector_PresetFreeEntityKeepsRotationAndLatestRuntimeState()
        {
            var world = new EcsWorld();
            var id = world.CreateWithComponents(new BitMask(
                ComponentMeta<PureEntity>.Id, ComponentMeta<Position>.Id,
                ComponentMeta<Rotation>.Id, ComponentMeta<Editable>.Id));
            using (var position = new RuntimeComponentEditBuffer(world, id, typeof(Position)))
            {
                var expected = new Vector3(4f, 5f, 6f);
                position.ComponentProperty.FindPropertyRelative("position").vector3Value = expected;
                Assert.IsTrue(position.Commit());
                Assert.AreEqual(expected, world.Get<Position>(id).position);
            }
            using (var rotation = new RuntimeComponentEditBuffer(world, id, typeof(Rotation)))
            {
                var expected = Quaternion.Euler(10f, 20f, 30f);
                rotation.ComponentProperty.FindPropertyRelative("rotation").quaternionValue = expected;
                Assert.IsTrue(rotation.Commit());
                Assert.AreEqual(expected, world.Get<Rotation>(id).rotation);
            }
            using var buffer = new RuntimeComponentEditBuffer(world, id, typeof(Editable));
            world.Get<Editable>(id).Tick = 99;
            buffer.ComponentProperty.FindPropertyRelative("Value").intValue = 42;
            Assert.IsTrue(buffer.Commit());
            Assert.AreEqual(42, world.Get<Editable>(id).Value);
            Assert.AreEqual(99, world.Get<Editable>(id).Tick);
            world.Get<Editable>(id).Value = 55;
            buffer.Refresh();
            Assert.AreEqual(55, buffer.ComponentProperty.FindPropertyRelative("Value").intValue);
        }

        [Test]
        public void RuntimeInspector_ExternalSerializedApplyAndSeparateInspectorsKeepTheirTargets()
        {
            var world = new EcsWorld();
            var mask = new BitMask(ComponentMeta<PureEntity>.Id, ComponentMeta<Editable>.Id);
            var first = world.CreateWithComponents(mask);
            var second = world.CreateWithComponents(mask);
            using var firstBuffer = new RuntimeComponentEditBuffer(world, first, typeof(Editable));
            using var secondBuffer = new RuntimeComponentEditBuffer(world, second, typeof(Editable));
            firstBuffer.ComponentProperty.FindPropertyRelative("Value").intValue = 15;
            firstBuffer.Serialized.ApplyModifiedPropertiesWithoutUndo();
            world.Get<Editable>(first).Tick = 27;
            firstBuffer.Refresh();
            secondBuffer.ComponentProperty.FindPropertyRelative("Value").intValue = 36;
            Assert.IsTrue(secondBuffer.Commit());
            Assert.AreEqual(15, world.Get<Editable>(first).Value);
            Assert.AreEqual(27, world.Get<Editable>(first).Tick);
            Assert.AreEqual(36, world.Get<Editable>(second).Value);
        }

        [Test]
        public void RuntimeInspector_DoesNotWriteToDeletedOrRecycledEntity()
        {
            var world = new EcsWorld();
            var mask = new BitMask(ComponentMeta<PureEntity>.Id, ComponentMeta<Editable>.Id);
            var id = world.CreateWithComponents(mask);
            using var buffer = new RuntimeComponentEditBuffer(world, id, typeof(Editable));
            buffer.ComponentProperty.FindPropertyRelative("Value").intValue = 42;
            world.Delete(id);
            Assert.IsFalse(buffer.Commit());
            Assert.AreEqual(id, world.CreateWithComponents(mask));
            Assert.IsFalse(buffer.Commit());
            Assert.AreEqual(0, world.Get<Editable>(id).Value);
        }

        [Test]
        public void RuntimeInspector_NestedEditsPreserveNewerFieldsAndRuntimeOnlyData()
        {
            var world = new EcsWorld();
            var id = world.CreateWithComponents(new BitMask(ComponentMeta<PureEntity>.Id, ComponentMeta<Editable>.Id));
            world.Get<Editable>(id).Settings = new EditableSettings();
            world.Get<Editable>(id).Items = new List<EditableSettings> { new() };
            using var buffer = new RuntimeComponentEditBuffer(world, id, typeof(Editable));
            world.Get<Editable>(id).Settings.Tick = 11;
            world.Get<Editable>(id).Settings.RuntimeTick = 12;
            world.Get<Editable>(id).Items[0].Tick = 13;
            world.Get<Editable>(id).Items[0].RuntimeTick = 14;
            buffer.ComponentProperty.FindPropertyRelative("Settings.Amount").intValue = 25;
            buffer.ComponentProperty.FindPropertyRelative("Items").GetArrayElementAtIndex(0)
                .FindPropertyRelative("Amount").intValue = 26;
            Assert.IsTrue(buffer.Commit());
            Assert.AreEqual(25, world.Get<Editable>(id).Settings.Amount);
            Assert.AreEqual(11, world.Get<Editable>(id).Settings.Tick);
            Assert.AreEqual(12, world.Get<Editable>(id).Settings.RuntimeTick);
            Assert.AreEqual(26, world.Get<Editable>(id).Items[0].Amount);
            Assert.AreEqual(13, world.Get<Editable>(id).Items[0].Tick);
            Assert.AreEqual(14, world.Get<Editable>(id).Items[0].RuntimeTick);
        }

        [Test]
        public void BuildFilter_RequiresPureEntityAndHonorsRequiredAndExcludedTypes()
        {
            var world = new EcsWorld();
            var expected = world.CreateWithComponents(new BitMask(
                ComponentMeta<PureEntity>.Id,
                ComponentMeta<Required>.Id));
            world.CreateWithComponents(new BitMask(
                ComponentMeta<PureEntity>.Id,
                ComponentMeta<Required>.Id,
                ComponentMeta<Excluded>.Id));
            world.CreateWithComponents(new BitMask(ComponentMeta<Required>.Id));
            world.CreateWithComponents(new BitMask(ComponentMeta<PureEntity>.Id));

            var filter = PureEntityEditorUtility.BuildFilter(
                world,
                new[] { typeof(Required) },
                new[] { typeof(Excluded) });

            CollectionAssert.AreEqual(new[] { expected }, Snapshot(filter));
        }

        [Test]
        public void BuildFilter_RejectsInternalReactiveMarker()
        {
            var world = new EcsWorld();

            Assert.Throws<System.ArgumentException>(() =>
                PureEntityEditorUtility.BuildFilter(
                    world,
                    new[] { typeof(MatchReact) },
                    System.Array.Empty<System.Type>()));
        }

        [Test]
        public void ComponentPicker_ExcludesInternalReactiveMarker()
        {
            CollectionAssert.DoesNotContain(
                IntegrationHelper.ComponentTypes,
                typeof(MatchReact));
        }

        [Test]
        public void EntityReference_DoesNotRetargetARecycledEntityId()
        {
            var world = new EcsWorld();
            var entityId = world.CreateWithComponents(
                new BitMask(ComponentMeta<PureEntity>.Id));
            var original = world.GetById(entityId);

            world.Delete(entityId);
            var recycledId = world.CreateWithComponents(
                new BitMask(ComponentMeta<PureEntity>.Id));
            var recycled = world.GetById(recycledId);

            Assert.AreEqual(entityId, recycledId);
            Assert.IsFalse(PureEntityEditorUtility.IsEntityReferenceValid(
                world,
                entityId,
                in original));
            Assert.IsTrue(PureEntityEditorUtility.IsPureEntityReferenceValid(
                world,
                recycledId,
                in recycled));
        }

        [Test]
        public void TransformAccess_MovesAndNormalizesRotation()
        {
            var world = new EcsWorld();
            var entityId = world.CreateWithComponents(new BitMask(
                ComponentMeta<PureEntity>.Id,
                ComponentMeta<Position>.Id,
                ComponentMeta<Rotation>.Id));
            var entity = world.GetById(entityId);

            Assert.IsTrue(PureEntityEditorUtility.TrySetPosition(
                world,
                entityId,
                in entity,
                new Vector3(3f, 4f, 5f)));
            Assert.IsTrue(PureEntityEditorUtility.TrySetRotation(
                world,
                entityId,
                in entity,
                new Quaternion(0f, 0f, 0f, 2f)));

            Assert.AreEqual(new Vector3(3f, 4f, 5f), world.Get<Position>(entityId).position);
            Assert.AreEqual(Quaternion.identity, world.Get<Rotation>(entityId).rotation);
        }

        [Test]
        public void RuntimeInspectorAddition_UsesRequirementsAndMetadataDefaults()
        {
            var world = new EcsWorld();
            var entityId = world.CreateWithComponents(
                new BitMask(ComponentMeta<PureEntity>.Id));

            Assert.IsTrue(EcsComponentInspectorUtility.TryAddRuntimeComponents(
                world,
                entityId,
                typeof(RequiresRequired)));
            Assert.IsTrue(world.Have<Required>(entityId));
            Assert.IsTrue(world.Have<RequiresRequired>(entityId));

            world.Add_Dynamic(typeof(Initialized), entityId);
            Assert.AreEqual(41, world.Get<Initialized>(entityId).Value);
            world.Remove_Dynamic(typeof(Initialized), entityId);
            Assert.IsFalse(world.Have<Initialized>(entityId));
        }

        private static List<int> Snapshot(EcsFilter filter)
        {
            var result = new List<int>();
            foreach (var entityId in filter)
                result.Add(entityId);
            return result;
        }
    }
}
#endif
