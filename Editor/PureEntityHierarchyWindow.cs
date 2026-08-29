using System;
using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal sealed class PureEntityHierarchyWindow : EditorWindow
    {
        private const double RefreshInterval = 0.2d;

        [SerializeField]
        private TreeViewState _treeViewState;
        [SerializeField]
        private bool _filtersExpanded = true;
        [SerializeField]
        private List<string> _requiredComponentTypeNames = new();
        [SerializeField]
        private List<string> _excludedComponentTypeNames = new();

        private readonly List<Type> _requiredComponentTypes = new();
        private readonly List<Type> _excludedComponentTypes = new();
        private readonly List<EntityRecord> _entities = new();

        private PureEntityTreeView _treeView;
        private SearchField _searchField;
        private ComponentTypeDropdown _componentTypeDropdown;
        private PureEntitySelectionProxy _selectionProxy;
        private EcsWorld _world;
        private EcsFilter _filter;
        private bool _filterDirty = true;
        private string _filterError;
        private double _nextRefreshTime;
        private bool _isGlobalRotationDrag;
        private Quaternion _globalRotationStart = Quaternion.identity;

        [MenuItem("Window/ECS/Pure Entity Hierarchy")]
        private static void Open() =>
            GetWindow<PureEntityHierarchyWindow>("Pure ECS Entities");

        private void OnEnable()
        {
            titleContent = new GUIContent("Pure ECS Entities");
            _treeViewState ??= new TreeViewState();
            _treeView = new PureEntityTreeView(
                _treeViewState,
                SelectEntity,
                FocusEntityInScene);
            _searchField = new SearchField();
            RestoreComponentTypes();
            SceneView.duringSceneGui += DuringSceneGui;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ReleaseSelectionProxy();
            EntityEditorHelper.CleanProxiesCache();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
                ResetRuntimeState();
        }

        private void OnInspectorUpdate()
        {
            if (!TryGetRuntime(out _, out var world))
                return;

            SetWorld(world);
            if (EnsureFilter())
                RefreshEntities();
            Repaint();
        }

        private void OnGUI()
        {
            var haveRuntime = TryGetRuntime(out var controller, out var world);
            DrawToolbar(haveRuntime ? controller : null);
            DrawFilters();

            if (!haveRuntime)
            {
                if (_world != null)
                    ResetRuntimeState();
                EditorGUILayout.HelpBox(
                    "Pure ECS entities are available while the game is playing and an " +
                    "ECSPipelineController has initialized its world.",
                    MessageType.Info);
                return;
            }

            SetWorld(world);
            if (!EnsureFilter())
            {
                EditorGUILayout.HelpBox(_filterError, MessageType.Error);
                return;
            }

            RefreshEntities();
            DrawEntityTree();
        }

        private void DrawToolbar(ECSPipelineController controller)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Pure entities", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(controller == null))
            {
                var paused = controller != null && controller.IsPaused;
                if (GUILayout.Button(
                        paused ? "Resume Systems" : "Pause Systems",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(105)))
                {
                    controller.TogglePause();
                }
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                _nextRefreshTime = 0d;
                RefreshEntities(true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            _filtersExpanded = EditorGUILayout.Foldout(
                _filtersExpanded,
                "Component query",
                true);
            if (!_filtersExpanded)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Must have", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(typeof(PureEntity).FullName);
            DrawTypeList(_requiredComponentTypes, RemoveRequiredType);
            if (GUILayout.Button("Add required component..."))
                ShowComponentDropdown(AddRequiredType);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Must not have", EditorStyles.boldLabel);
            DrawTypeList(_excludedComponentTypes, RemoveExcludedType);
            if (GUILayout.Button("Add excluded component..."))
                ShowComponentDropdown(AddExcludedType);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4f);
        }

        private static void DrawTypeList(IReadOnlyList<Type> types, Action<int> removeAt)
        {
            for (var i = 0; i < types.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(
                    types[i].FullName ?? types[i].Name,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("-", GUILayout.Width(22)))
                {
                    removeAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ShowComponentDropdown(Action<Type> onSelected)
        {
            var candidates = new List<Type>();
            var componentTypes = IntegrationHelper.ComponentTypes;
            for (var i = 0; i < componentTypes.Count; i++)
            {
                var type = componentTypes[i];
                if (type == typeof(PureEntity) ||
                    _requiredComponentTypes.Contains(type) ||
                    _excludedComponentTypes.Contains(type))
                    continue;
                candidates.Add(type);
            }

            _componentTypeDropdown = new ComponentTypeDropdown(
                new AdvancedDropdownState(),
                candidates,
                onSelected);
            _componentTypeDropdown.Show(GUILayoutUtility.GetLastRect());
        }

        private void AddRequiredType(Type type)
        {
            AddType(type, _requiredComponentTypes, _requiredComponentTypeNames);
        }

        private void AddExcludedType(Type type)
        {
            AddType(type, _excludedComponentTypes, _excludedComponentTypeNames);
        }

        private void AddType(Type type, List<Type> types, List<string> typeNames)
        {
            if (type == null || type == typeof(PureEntity) ||
                _requiredComponentTypes.Contains(type) ||
                _excludedComponentTypes.Contains(type))
                return;

            types.Add(type);
            typeNames.Add(type.AssemblyQualifiedName);
            MarkFilterDirty();
        }

        private void RemoveRequiredType(int index) =>
            RemoveType(index, _requiredComponentTypes, _requiredComponentTypeNames);

        private void RemoveExcludedType(int index) =>
            RemoveType(index, _excludedComponentTypes, _excludedComponentTypeNames);

        private void RemoveType(int index, List<Type> types, List<string> typeNames)
        {
            if (index < 0 || index >= types.Count)
                return;
            types.RemoveAt(index);
            typeNames.RemoveAt(index);
            MarkFilterDirty();
        }

        private void RestoreComponentTypes()
        {
            RestoreComponentTypes(_requiredComponentTypeNames, _requiredComponentTypes);
            RestoreComponentTypes(_excludedComponentTypeNames, _excludedComponentTypes);

            for (var i = _excludedComponentTypes.Count - 1; i >= 0; i--)
            {
                if (_requiredComponentTypes.Contains(_excludedComponentTypes[i]))
                    RemoveType(i, _excludedComponentTypes, _excludedComponentTypeNames);
            }
            MarkFilterDirty();
        }

        private static void RestoreComponentTypes(List<string> typeNames, List<Type> types)
        {
            types.Clear();
            var seen = new HashSet<Type>();
            for (var i = typeNames.Count - 1; i >= 0; i--)
            {
                var type = Type.GetType(typeNames[i], false);
                if (type == null || type == typeof(PureEntity) || type == typeof(MatchReact) ||
                    !typeof(IComponent).IsAssignableFrom(type) ||
                    type.IsAbstract || type.IsInterface || type.IsGenericType ||
                    !seen.Add(type))
                {
                    typeNames.RemoveAt(i);
                }
            }

            // The reverse validation above permits removals without shifting unvisited
            // entries; restore the user-visible order from the surviving serialized names.
            for (var i = 0; i < typeNames.Count; i++)
                types.Add(Type.GetType(typeNames[i], true));
        }

        private void MarkFilterDirty()
        {
            _filterDirty = true;
            _nextRefreshTime = 0d;
            Repaint();
        }

        private bool EnsureFilter()
        {
            if (!_filterDirty && _filter != null)
                return true;

            try
            {
                _filter = PureEntityEditorUtility.BuildFilter(
                    _world,
                    _requiredComponentTypes,
                    _excludedComponentTypes);
                _filterDirty = false;
                _filterError = null;
                return true;
            }
            catch (Exception exception)
            {
                _filter = null;
                _filterError = $"Cannot build the pure-entity query: {exception.Message}";
                return false;
            }
        }

        private void DrawEntityTree()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _treeView.searchString = _searchField.OnToolbarGUI(_treeView.searchString);
            GUILayout.Label($"{_entities.Count}", EditorStyles.miniLabel, GUILayout.Width(45));
            EditorGUILayout.EndHorizontal();

            var treeRect = GUILayoutUtility.GetRect(
                0f,
                100000f,
                0f,
                100000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            _treeView.OnGUI(treeRect);

            if (_selectionProxy != null && _selectionProxy.IsPureEntityValid)
            {
                EditorGUILayout.HelpBox(
                    "Use the Move (W) and Rotate (E) tools in the Scene view. Runtime " +
                    "systems may overwrite transform values unless the pipeline is paused.",
                    MessageType.None);
            }
        }

        private void RefreshEntities(bool force = false)
        {
            if (_world == null || _filter == null)
                return;
            var now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextRefreshTime)
                return;
            _nextRefreshTime = now + RefreshInterval;

            _entities.Clear();
            foreach (var entityId in _filter)
            {
                if (!_world.IsIdValid(entityId))
                    continue;
                var entity = _world.GetById(entityId);
                if (_world.IsEntityValid(entity))
                    _entities.Add(new EntityRecord(entityId, entity));
            }
            _entities.Sort((left, right) => left.Id.CompareTo(right.Id));
            _treeView.SetEntities(_entities);
            ValidateSelection();
        }

        private void SelectEntity(int entityId)
        {
            for (var i = 0; i < _entities.Count; i++)
            {
                var record = _entities[i];
                if (record.Id != entityId)
                    continue;

                if (_selectionProxy == null)
                {
                    _selectionProxy = CreateInstance<PureEntitySelectionProxy>();
                    // Keep the transient proxy out of the hierarchy and serialized data,
                    // but do not use HideAndDontSave: that composite includes NotEditable
                    // and prevents its custom inspector from editing component values.
                    _selectionProxy.hideFlags = HideFlags.HideInHierarchy |
                                                HideFlags.DontSaveInEditor |
                                                HideFlags.DontSaveInBuild;
                }
                ResetGlobalRotationDrag();
                _selectionProxy.Bind(_world, record.Id, in record.Entity);
                Selection.activeObject = _selectionProxy;
                SceneView.RepaintAll();
                Repaint();
                return;
            }
        }

        private void FocusEntityInScene(int entityId)
        {
            SelectEntity(entityId);
            var selectedEntity = _selectionProxy != null
                ? _selectionProxy.Entity
                : default;
            if (_selectionProxy == null ||
                !PureEntityEditorUtility.TryGetPosition(
                    _selectionProxy.World,
                    _selectionProxy.EntityId,
                    in selectedEntity,
                    out var position))
                return;

            SceneView.lastActiveSceneView?.Frame(
                new Bounds(position, Vector3.one),
                false);
        }

        private void ValidateSelection()
        {
            if (_selectionProxy == null)
                return;

            for (var i = 0; i < _entities.Count; i++)
            {
                var record = _entities[i];
                if (_selectionProxy.Matches(_world, record.Id, in record.Entity))
                    return;
            }

            _treeView.SetSelection(Array.Empty<int>());
            if (Selection.activeObject == _selectionProxy)
                Selection.activeObject = null;
            _selectionProxy.Unbind();
            ResetGlobalRotationDrag();
            SceneView.RepaintAll();
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            var selectedEntity = _selectionProxy != null
                ? _selectionProxy.Entity
                : default;
            if (_selectionProxy == null ||
                Selection.activeObject != _selectionProxy ||
                !_selectionProxy.IsPureEntityValid ||
                !PureEntityEditorUtility.TryGetPosition(
                    _selectionProxy.World,
                    _selectionProxy.EntityId,
                    in selectedEntity,
                    out var position))
                return;

            var haveRotation = PureEntityEditorUtility.TryGetRotation(
                _selectionProxy.World,
                _selectionProxy.EntityId,
                in selectedEntity,
                out var rotation);
            var markerSize = HandleUtility.GetHandleSize(position) * 0.08f;
            using (new Handles.DrawingScope(Handles.selectedColor))
            {
                Handles.SphereHandleCap(
                    0,
                    position,
                    Quaternion.identity,
                    markerSize,
                    EventType.Repaint);
                Handles.Label(position + Vector3.up * markerSize * 1.5f, _selectionProxy.name);

                switch (Tools.current)
                {
                    case Tool.Move:
                    {
                        var handleRotation = Tools.pivotRotation == PivotRotation.Local && haveRotation
                            ? rotation
                            : Quaternion.identity;
                        EditorGUI.BeginChangeCheck();
                        var movedPosition = Handles.PositionHandle(position, handleRotation);
                        if (EditorGUI.EndChangeCheck())
                        {
                            PureEntityEditorUtility.TrySetPosition(
                                _selectionProxy.World,
                                _selectionProxy.EntityId,
                                in selectedEntity,
                                movedPosition);
                            OnTransformChanged(sceneView);
                        }
                        break;
                    }
                    case Tool.Rotate when haveRotation:
                    {
                        var isGlobal = Tools.pivotRotation == PivotRotation.Global;
                        if (!isGlobal)
                            ResetGlobalRotationDrag();

                        EditorGUI.BeginChangeCheck();
                        var rotatedHandle = Handles.RotationHandle(
                            isGlobal ? Quaternion.identity : rotation,
                            position);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Quaternion rotatedEntity;
                            if (isGlobal)
                            {
                                // RotationHandle returns the cumulative handle rotation for
                                // the active drag. Keep the handle world-aligned and apply
                                // that delta to the entity rotation captured at drag start.
                                if (!_isGlobalRotationDrag)
                                {
                                    _isGlobalRotationDrag = true;
                                    _globalRotationStart = rotation;
                                }
                                rotatedEntity = rotatedHandle * _globalRotationStart;
                            }
                            else
                            {
                                rotatedEntity = rotatedHandle;
                            }

                            PureEntityEditorUtility.TrySetRotation(
                                _selectionProxy.World,
                                _selectionProxy.EntityId,
                                in selectedEntity,
                                rotatedEntity);
                            OnTransformChanged(sceneView);
                        }

                        if (isGlobal && GUIUtility.hotControl == 0)
                            ResetGlobalRotationDrag();
                        break;
                    }
                    default:
                        ResetGlobalRotationDrag();
                        break;
                }
            }
        }

        private void ResetGlobalRotationDrag()
        {
            _isGlobalRotationDrag = false;
            _globalRotationStart = Quaternion.identity;
        }

        private void OnTransformChanged(SceneView sceneView)
        {
            GUI.changed = true;
            _nextRefreshTime = 0d;
            sceneView.Repaint();
            Repaint();
        }

        private void SetWorld(EcsWorld world)
        {
            if (ReferenceEquals(_world, world))
                return;

            ReleaseSelectionProxy();
            _world = world;
            _filter = null;
            _filterDirty = true;
            _filterError = null;
            _nextRefreshTime = 0d;
            _entities.Clear();
            _treeView.SetEntities(_entities);
        }

        private void ResetRuntimeState()
        {
            ReleaseSelectionProxy();
            _world = null;
            _filter = null;
            _filterDirty = true;
            _filterError = null;
            _entities.Clear();
            _treeView?.SetEntities(_entities);
        }

        private void ReleaseSelectionProxy()
        {
            if (_selectionProxy == null)
                return;
            if (Selection.activeObject == _selectionProxy)
                Selection.activeObject = null;
            DestroyImmediate(_selectionProxy);
            _selectionProxy = null;
            ResetGlobalRotationDrag();
            SceneView.RepaintAll();
        }

        private static bool TryGetRuntime(
            out ECSPipelineController controller,
            out EcsWorld world)
        {
            controller = null;
            world = null;
            if (!EditorApplication.isPlaying || !ECSPipelineController.IsCreated)
                return false;

            controller = ECSPipelineController.Instance;
            if (controller == null)
                return false;
            world = controller.World;
            return world != null;
        }

        private readonly struct EntityRecord
        {
            internal readonly int Id;
            internal readonly Entity Entity;

            internal EntityRecord(int id, Entity entity)
            {
                Id = id;
                Entity = entity;
            }
        }

        private sealed class PureEntityTreeView : TreeView
        {
            private readonly Action<int> _onSelected;
            private readonly Action<int> _onDoubleClicked;
            private readonly List<EntityRecord> _entities = new();

            internal PureEntityTreeView(
                TreeViewState state,
                Action<int> onSelected,
                Action<int> onDoubleClicked)
                : base(state)
            {
                _onSelected = onSelected;
                _onDoubleClicked = onDoubleClicked;
                showBorder = true;
                showAlternatingRowBackgrounds = true;
                Reload();
            }

            internal void SetEntities(IReadOnlyList<EntityRecord> entities)
            {
                _entities.Clear();
                for (var i = 0; i < entities.Count; i++)
                    _entities.Add(entities[i]);
                Reload();
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(0, -1, "Root")
                {
                    children = new List<TreeViewItem>(_entities.Count)
                };
                for (var i = 0; i < _entities.Count; i++)
                {
                    var entity = _entities[i];
                    root.AddChild(new TreeViewItem(entity.Id, 0, $"Entity {entity.Id}"));
                }
                return root;
            }

            protected override bool CanMultiSelect(TreeViewItem item) => false;

            protected override void RowGUI(RowGUIArgs args)
            {
                // TreeView does not raise SelectionChanged when the highlighted row is
                // clicked again. Explicitly re-bind it so a user can return from another
                // Unity selection without first selecting a different ECS entity.
                var reselect = Event.current.type == EventType.MouseDown &&
                               Event.current.button == 0 &&
                               args.rowRect.Contains(Event.current.mousePosition);
                base.RowGUI(args);
                if (reselect)
                    _onSelected(args.item.id);
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                if (selectedIds.Count > 0)
                    _onSelected(selectedIds[0]);
            }

            protected override void DoubleClickedItem(int id) => _onDoubleClicked(id);
        }

        private sealed class ComponentTypeDropdown : AdvancedDropdown
        {
            private readonly IReadOnlyList<Type> _types;
            private readonly Action<Type> _onSelected;

            internal ComponentTypeDropdown(
                AdvancedDropdownState state,
                IReadOnlyList<Type> types,
                Action<Type> onSelected)
                : base(state)
            {
                _types = types;
                _onSelected = onSelected;
                minimumSize = new Vector2(360f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("ECS components");
                for (var i = 0; i < _types.Count; i++)
                    root.AddChild(new ComponentTypeItem(_types[i]));
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is ComponentTypeItem componentItem)
                    _onSelected(componentItem.Type);
            }
        }

        private sealed class ComponentTypeItem : AdvancedDropdownItem
        {
            internal readonly Type Type;

            internal ComponentTypeItem(Type type)
                : base(type.FullName ?? type.Name)
            {
                Type = type;
            }
        }
    }
}
