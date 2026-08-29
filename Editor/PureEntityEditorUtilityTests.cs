#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using NUnit.Framework;
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
