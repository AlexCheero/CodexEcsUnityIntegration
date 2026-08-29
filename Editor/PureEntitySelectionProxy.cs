using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Components;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal sealed class PureEntitySelectionProxy : ScriptableObject
    {
        [NonSerialized]
        private EcsWorld _world;
        [NonSerialized]
        private Entity _entity;
        [NonSerialized]
        private int _entityId = -1;

        internal EcsWorld World => _world;
        internal Entity Entity => _entity;
        internal int EntityId => _entityId;
        internal bool IsEntityValid =>
            PureEntityEditorUtility.IsEntityReferenceValid(_world, _entityId, in _entity);
        internal bool IsPureEntityValid =>
            PureEntityEditorUtility.IsPureEntityReferenceValid(_world, _entityId, in _entity);

        internal void Bind(EcsWorld world, int entityId, in Entity entity)
        {
            _world = world;
            _entityId = entityId;
            _entity = entity;
            name = $"ECS Entity {entityId}";
        }

        internal void Unbind()
        {
            _world = null;
            _entity = default;
            _entityId = -1;
            name = "ECS Entity";
        }

        internal bool Matches(EcsWorld world, int entityId, in Entity entity) =>
            ReferenceEquals(_world, world) &&
            _entityId == entityId &&
            _entity.Val == entity.Val;
    }

    [CustomEditor(typeof(PureEntitySelectionProxy))]
    internal sealed class PureEntitySelectionProxyEditor : UnityEditor.Editor
    {
        private void OnEnable() => EditorApplication.update += Repaint;

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            EntityEditorHelper.CleanProxiesCache();
        }

        public override void OnInspectorGUI()
        {
            var proxy = (PureEntitySelectionProxy)target;
            if (!proxy.IsEntityValid)
            {
                EditorGUILayout.HelpBox(
                    "This ECS entity no longer exists. Select another entity in the Pure Entity Hierarchy.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Entity ID", proxy.EntityId.ToString());
            if (!proxy.World.Have<PureEntity>(proxy.EntityId))
            {
                EditorGUILayout.HelpBox(
                    $"Entity {proxy.EntityId} no longer has {nameof(PureEntity)}.",
                    MessageType.Warning);
                return;
            }

            if (proxy.World.Have<Rotation>(proxy.EntityId) &&
                !proxy.World.Have<Position>(proxy.EntityId))
            {
                EditorGUILayout.HelpBox(
                    $"{nameof(Rotation)} can be inspected, but the Scene rotation handle " +
                    $"needs {nameof(Position)} to place its pivot.",
                    MessageType.Info);
            }

            EntityEditorHelper.DrawRuntimeInspector(proxy.World, proxy.EntityId, proxy);
        }
    }
}
