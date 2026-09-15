#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using CodexECS;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using SystemEntry = CodexFramework.CodexEcsUnityIntegration.ECSPipelineBehaviour.SystemEntry;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    public sealed class SystemsOverrideTests
    {
        [Test]
        public void RemovesOnlySelectedCategoryAndPreservesOrderAndOptions()
        {
            var owner = new GameObject("Systems override test");
            try
            {
                var pipeline = owner.AddComponent<ECSPipelineBehaviour>();
                var changes = owner.AddComponent<SystemsOverride>();
                foreach (var category in IntegrationHelper.SystemCategories)
                    pipeline.GetSystemScriptsByCategory(category) = new[] { Entry("First"), Entry("Removed"), Entry("Last") };
                changes.SystemsToRemove._updateSystemScripts = new[] { Entry("Removed") };
                changes.SystemsToAdd._updateSystemScripts = new[] { Entry("First", false, true), Entry("Appended") };

                changes.ApplyTo(pipeline);

                Assert.That(pipeline._updateSystemScripts.Select(entry => entry.Name), Is.EqualTo(new[] { "First", "Last", "Appended" }));
                Assert.That(pipeline._updateSystemScripts[0].Active, Is.False);
                Assert.That(pipeline._updateSystemScripts[0].NonPausable, Is.True);
                foreach (var category in IntegrationHelper.SystemCategories.Where(category => category != ESystemCategory.Update))
                    Assert.That(pipeline.GetSystemScriptsByCategory(category).Select(entry => entry.Name), Is.EqualTo(new[] { "First", "Removed", "Last" }));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void RemovalThenAdditionReplacesEntryWithoutDuplicatingOnRepeatedApplication()
        {
            var owner = new GameObject("Systems override test");
            try
            {
                var pipeline = owner.AddComponent<ECSPipelineBehaviour>();
                var changes = owner.AddComponent<SystemsOverride>();
                pipeline._initSystemScripts = new[] { Entry("Replaced"), Entry("Kept") };
                changes.SystemsToRemove._initSystemScripts = new[] { Entry("Replaced"), Entry("Absent") };
                changes.SystemsToAdd._initSystemScripts = new[] { Entry("Replaced", true, true) };
                changes.ApplyTo(pipeline);
                changes.ApplyTo(pipeline);
                Assert.That(pipeline._initSystemScripts.Select(entry => entry.Name), Is.EqualTo(new[] { "Kept", "Replaced" }));
                Assert.That(pipeline._initSystemScripts[1].NonPausable, Is.True);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void AddedInitSystemRunsAndRemovedSystemIsNeverConstructed()
        {
            var owner = new GameObject("Systems override test");
            IntegrationHelper.SystemTypes.Add(typeof(AddedSystem).FullName, typeof(AddedSystem));
            IntegrationHelper.SystemTypes.Add(typeof(RemovedSystem).FullName, typeof(RemovedSystem));
            AddedSystem.InitCalls = 0;
            AddedSystem.TickCalls = 0;
            try
            {
                var pipeline = owner.AddComponent<ECSPipelineBehaviour>();
                var changes = owner.AddComponent<SystemsOverride>();
                pipeline._initSystemScripts = new[] { Entry(typeof(RemovedSystem).FullName) };
                changes.SystemsToRemove._initSystemScripts = pipeline._initSystemScripts.ToArray();
                changes.SystemsToAdd._initSystemScripts = new[] { Entry(typeof(AddedSystem).FullName) };
                changes.ApplyTo(pipeline);
                pipeline.Init(new EcsWorld());
                pipeline.RunInitSystems();
                Assert.That(AddedSystem.InitCalls, Is.EqualTo(1));
                Assert.That(AddedSystem.TickCalls, Is.EqualTo(1));
                pipeline.SwitchSystem<AddedSystem>(ESystemCategory.Init, false);
                pipeline.RunInitSystems();
                Assert.That(AddedSystem.TickCalls, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                IntegrationHelper.SystemTypes.Remove(typeof(AddedSystem).FullName);
                IntegrationHelper.SystemTypes.Remove(typeof(RemovedSystem).FullName);
            }
        }

        private static SystemEntry Entry(string name, bool active = true, bool nonPausable = false) =>
            new() { Name = name, Active = active, NonPausable = nonPausable };

        public sealed class AddedSystem : EcsSystem
        {
            public static int InitCalls;
            public static int TickCalls;
            public AddedSystem(EcsWorld world) { }
            public override void Init(EcsWorld world) => InitCalls++;
            public override void Tick(EcsWorld world) => TickCalls++;
        }

        public sealed class RemovedSystem : EcsSystem
        {
            public RemovedSystem(EcsWorld world) => throw new InvalidOperationException("Removed system was constructed.");
        }
    }
}
#endif
