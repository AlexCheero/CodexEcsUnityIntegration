using System;
using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal sealed class RuntimeEntityInspector : IDisposable
    {
        private readonly Dictionary<Type, RuntimeComponentEditBuffer> _buffers = new();
        private readonly Dictionary<Type, bool> _foldouts = new();
        private readonly List<Type> _components = new();
        private readonly HashSet<Type> _present = new();
        private EcsWorld _world;
        private Entity _entity;
        private int _entityId;
        private EntityPreset _sourcePreset;
        private string _filter = "";
        private string _addFilter = "";
        private bool _showComponents = true;
        private bool _showAdd;

        internal void Draw(EcsWorld world, int entityId, Object context)
        {
            var entity = world.GetById(entityId);
            if (!ReferenceEquals(world, _world) || _entity.Val != entity.Val || _entityId != entityId)
            {
                Dispose();
                _world = world;
                _entity = entity;
                _entityId = entityId;
            }

            EntityPreset.TryGetSourcePreset(world, entityId, in entity, out _sourcePreset);
            if (_sourcePreset != null)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Source Preset", _sourcePreset, typeof(EntityPreset), false);

            // Only enumerate this entity's mask; no world scan or wrapper creation for tags.
            _components.Clear();
            _present.Clear();
            foreach (var componentId in world.GetMask(entityId))
            {
                var type = ComponentMapping.GetTypeForId(componentId);
                if (typeof(IComponent).IsAssignableFrom(type))
                {
                    _present.Add(type);
                    _components.Add(type);
                }
            }
            _components.Sort(CompareTypes);

            // Release removed component buffers so a later re-add starts with fresh state.
            foreach (var type in _foldouts.Keys)
                if (!_present.Contains(type) && _buffers.Remove(type, out var removed))
                    removed.Dispose();

            _showComponents = EditorGUILayout.Foldout(_showComponents, "Components", true);
            if (_showComponents)
            {
                _filter = EditorGUILayout.TextField("Search", _filter);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Expand All")) SetFoldouts(true);
                    if (GUILayout.Button("Fold All")) SetFoldouts(false);
                }
                foreach (var type in _components)
                {
                    if (!Matches(type, _filter)) continue;
                    var remove = false;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope()) DrawComponent(type);
                        remove = GUILayout.Button("-", GUILayout.Width(20));
                    }
                    if (remove)
                    {
                        try { world.Remove_Dynamic(type, entityId); }
                        catch (Exception exception) { Debug.LogException(exception, context); }
                        if (_buffers.Remove(type, out var buffer)) buffer.Dispose();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button(_showAdd ? "Fold" : "Add Component")) _showAdd = !_showAdd;
            if (!_showAdd) return;
            _addFilter = EditorGUILayout.TextField("Search", _addFilter);
            foreach (var type in IntegrationHelper.ComponentTypes)
            {
                if (_present.Contains(type) || !Matches(type, _addFilter)) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(type.Name);
                    if (GUILayout.Button("+", GUILayout.Width(25)))
                        EcsComponentInspectorUtility.TryAddRuntimeComponents(world, entityId, type, context);
                }
            }
        }

        private void DrawComponent(Type type)
        {
            if (!EntityEditorHelper.HasUnitySerializableFields(type))
            {
                EditorGUILayout.LabelField(type.Name);
                return;
            }
            _foldouts.TryGetValue(type, out var expanded);
            expanded = EditorGUILayout.Foldout(expanded, type.Name, true);
            _foldouts[type] = expanded;
            if (!expanded) return;

            if (!type.IsSerializable)
            {
                EditorGUILayout.HelpBox(
                    $"Declare {type.Name} with [Serializable] to edit its fields in the inspector.",
                    MessageType.Info);
                return;
            }

            if (!_buffers.TryGetValue(type, out var buffer))
            {
                buffer = new RuntimeComponentEditBuffer(_world, _entityId, type);
                _buffers.Add(type, buffer);
            }
            // Layout reads current data; Repaint reuses the same serialized snapshot.
            // Input events also refresh so typing never writes stale runtime fields.
            if (Event.current.type != EventType.Repaint) buffer.Refresh();
            if (!buffer.IsValid) return;
            EditorGUI.indentLevel++;
            if (buffer.ComponentProperty != null)
                EntityEditorHelper.DrawComponentFields(buffer.ComponentProperty, type);
            else
                EditorGUILayout.HelpBox($"Unity cannot serialize {type.Name} for inspection.", MessageType.Info);
            if (buffer.Commit()) SceneView.RepaintAll();
            EditorGUI.indentLevel--;
            if (_sourcePreset != null && buffer.ComponentProperty != null &&
                GUILayout.Button(new GUIContent("Apply to Preset",
                    $"Save this component's current serialized values to {_sourcePreset.name}.")))
            {
                if (!RuntimeEntityPresetUtility.TryApplyComponent(
                        _world, _entityId, in _entity, type, out var error))
                    Debug.LogError(error, _sourcePreset);
            }
        }

        private void SetFoldouts(bool expanded)
        {
            foreach (var type in _components)
                if (Matches(type, _filter)) _foldouts[type] = expanded;
        }

        private static bool Matches(Type type, string filter) =>
            string.IsNullOrEmpty(filter) || type.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static int CompareTypes(Type left, Type right) =>
            string.Compare(left.Name, right.Name, StringComparison.Ordinal);

        public void Dispose()
        {
            foreach (var buffer in _buffers.Values) buffer.Dispose();
            _buffers.Clear();
            _components.Clear();
            _present.Clear();
            _world = null;
            _entity = default;
            _entityId = 0;
            _sourcePreset = null;
        }
    }
}
