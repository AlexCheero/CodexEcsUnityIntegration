#if UNITY_INCLUDE_TESTS
using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using CodexFramework.CodexEcsUnityIntegration.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor.Tests
{
    public sealed class EntityGroupMaskTests
    {
        [Serializable] private struct Anchor : IComponent { public int Value; }
        [Serializable] private struct Extra : IComponent { }

        [TestCase(false)]
        [TestCase(true)]
        public void Preset_NoneAndEmptyUseDifferentGroupsAndKeepAllComponents(bool specified)
        {
            var preset = ScriptableObject.CreateInstance<EntityPreset>();
            try
            {
                SetComponents(preset);
                SetMask(preset, specified);
                var world = new EcsWorld();
                var id = preset.CreatePureEntity(world);
                var expected = specified ? new BitMask() : new BitMask(
                    ComponentMeta<Anchor>.Id, ComponentMeta<Extra>.Id, ComponentMeta<PureEntity>.Id);
                var peer = world.CreateWithComponents(expected);
                Assert.AreEqual(id >> Layout.PageShift, peer >> Layout.PageShift);
                Assert.AreEqual(17, world.Get<Anchor>(id).Value);
                Assert.IsTrue(world.Have<Extra>(id));
                Assert.IsTrue(world.Have<PureEntity>(id));
            }
            finally { UnityEngine.Object.DestroyImmediate(preset); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void View_SelectedMaskGroupsHybridAndPureCreation(bool pure)
        {
            var go = new GameObject("Group mask test");
            try
            {
                var view = go.AddComponent<EntityView>();
                SetComponents(view);
                view.RebuildUnityComponentsCache();
                SetMask(view, true, typeof(Anchor));
                var world = new EcsWorld();
                var id = pure ? view.CreatePureEntity(world) : view.InitAsEntity(world);
                var mask = new BitMask(ComponentMeta<Anchor>.Id);
                var peer = world.CreateWithComponents(mask);
                Assert.AreEqual(id >> Layout.PageShift, peer >> Layout.PageShift);
                Assert.AreEqual(17, world.Get<Anchor>(id).Value);
                Assert.IsTrue(world.Have<Extra>(id));
                Assert.AreEqual(!pure, world.Have<Transform>(id));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Preset_SerializedSelectionSurvivesReload()
        {
            var preset = ScriptableObject.CreateInstance<EntityPreset>();
            var copy = ScriptableObject.CreateInstance<EntityPreset>();
            try
            {
                SetComponents(preset);
                SetMask(preset, true, typeof(Anchor));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(preset), copy);
                var world = new EcsWorld();
                var anchor = new BitMask(ComponentMeta<Anchor>.Id);
                var selected = copy.CreatePureEntity(world);
                var peer = world.CreateWithComponents(anchor);
                Assert.AreEqual(selected >> Layout.PageShift, peer >> Layout.PageShift);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void ConversionAndInspectorEditsPreserveAndRefreshTheSelectedMask()
        {
            var preset = ScriptableObject.CreateInstance<EntityPreset>();
            var go = new GameObject("Mask conversion test");
            try
            {
                SetComponents(preset);
                SetMask(preset, true, typeof(Anchor));
                var view = go.AddComponent<EntityView>();
                view.RebuildUnityComponentsCache();
                var copy = typeof(EntityViewPresetConverter).GetMethod("CopyComponents",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                copy.Invoke(null, new UnityEngine.Object[] { preset, view });
                var world = new EcsWorld();
                var initial = view.CreatePureEntity(world);
                var anchor = new BitMask(ComponentMeta<Anchor>.Id);
                Assert.AreEqual(initial >> Layout.PageShift,
                    world.CreateWithComponents(anchor) >> Layout.PageShift);
                SetMask(view, true);
                var edited = view.CreatePureEntity(world);
                Assert.AreNotEqual(initial >> Layout.PageShift, edited >> Layout.PageShift);
                copy.Invoke(null, new UnityEngine.Object[] { view, preset });
                var converted = preset.CreatePureEntity(world);
                Assert.AreEqual(edited >> Layout.PageShift, converted >> Layout.PageShift);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private static void SetComponents(UnityEngine.Object owner)
        {
            using var so = new SerializedObject(owner);
            var components = so.FindProperty("_components");
            components.arraySize = 2;
            var anchor = new ComponentWrapper<Anchor>();
            anchor.InitFromComponent(new Anchor { Value = 17 });
            components.GetArrayElementAtIndex(0).managedReferenceValue = anchor;
            components.GetArrayElementAtIndex(1).managedReferenceValue = new ComponentWrapper<Extra>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMask(UnityEngine.Object owner, bool specified, params Type[] types)
        {
            using var so = new SerializedObject(owner);
            var mask = so.FindProperty("_groupMask");
            mask.FindPropertyRelative("_specified").boolValue = specified;
            var selected = mask.FindPropertyRelative("_componentTypes");
            selected.arraySize = types.Length;
            for (var i = 0; i < types.Length; i++)
                selected.GetArrayElementAtIndex(i).stringValue = types[i].AssemblyQualifiedName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
