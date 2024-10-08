using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(ECSPipeline))]
    public class Pipeline_Inspector : UnityEditor.Editor
    {
        //add more system types if needed
        private const string InitSystemsLabel = "Init Systems";
        private const string UpdateSystemsLabel = "Update Systems";
        private const string LateUpdateSystemsLabel = "Late Update Systems";
        private const string FixedSystemsLabel = "Fixed Update Systems";
        private const string LateFixedSystemsLabel = "Late Fixed Update Systems";
        private const string EnableSystemsLabel = "On Enable Systems";
        private const string DisableSystemsLabel = "On Disable Systems";
        private const string ReactiveSystemsLabel = "On Add Reactive Systems";

        private static List<MonoScript> initSystems;
        private static List<MonoScript> updateSystems;
        private static List<MonoScript> lateUpdateSystems;
        private static List<MonoScript> fixedUpdateSystems;
        private static List<MonoScript> lateFixedUpdateSystems;
        private static List<MonoScript> enableSystems;
        private static List<MonoScript> disableSystems;
        private static List<MonoScript> reactiveSystems;

        private static List<MonoScript> systemScripts;

        private bool _addListExpanded;
        private string _addSearch;
        private string _addedSearch;
        
        private ReorderableList _initList;
        private ReorderableList _updateList;
        private ReorderableList _lateUpdateList;
        private ReorderableList _fixedUpdateList;
        private ReorderableList _lateFixedUpdateList;
        private ReorderableList _enableList;
        private ReorderableList _disableList;
        private ReorderableList _reactiveList;

        private ECSPipeline _pipeline;
        private ECSPipeline Pipeline
        {
            get
            {
                if (_pipeline == null)
                    _pipeline = (ECSPipeline)target;
                return _pipeline;
            }
        }

        private Dictionary<string, SerializedProperty> _serializedProperties;
        private SerializedProperty GetCachedSerializedProperty(string propertyName)
        {
            _serializedProperties ??= new();
            if (!_serializedProperties.ContainsKey(propertyName))
                _serializedProperties[propertyName] = serializedObject.FindProperty(propertyName);
            return _serializedProperties[propertyName];
        }
        
        private ReorderableList InitializeSystemList(string propertyName)
        {
            var arrayProperty = GetCachedSerializedProperty(propertyName);
            // Initialize the reorderable list
            var reorderableList = new ReorderableList(serializedObject,
                arrayProperty,
                true, false, true, true);

            // Set up drawing logic for each element
            reorderableList.drawElementCallback = (rect, index, _, _) =>
            {
                var element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                var scriptProp = element.FindPropertyRelative("Script");
                var activeProp = element.FindPropertyRelative("Active");

                DrawElement(rect, scriptProp, activeProp, arrayProperty, index);
            };

            reorderableList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight;

            // Set up element filtering logic (we'll filter during GUI rendering)
            reorderableList.onCanAddCallback = _ => true;
            reorderableList.onCanRemoveCallback = _ => true;
            
            //TODO: on system's position changed in runtime doesn't change the position of runtime system instance

            return reorderableList;
        }
        
        private int _removedElement = -1;
        private void DrawElement(Rect rect, SerializedProperty scriptProp, SerializedProperty activeProp, SerializedProperty arrayProperty, int index)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            const float gap = 5.0f;
        
            //TODO: fix indents
            EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width - lineHeight - 30, EditorGUIUtility.singleLineHeight),
                scriptProp, GUIContent.none);
        
            EditorGUI.PropertyField(new Rect(rect.x + rect.width - 2*lineHeight, rect.y, 30, EditorGUIUtility.singleLineHeight),
                activeProp, GUIContent.none);

            if (GUI.Button(new Rect(rect.x + rect.width - lineHeight, rect.y, lineHeight, lineHeight), "-"))
                _removedElement = index;
        }

        private void Initialize()
        {
            var systems = Resources.FindObjectsOfTypeAll(typeof(MonoScript)).Where(obj =>
            {
                var monoScript = obj as MonoScript;
                return monoScript != null && typeof(EcsSystem).IsAssignableFrom(monoScript.GetClass());
            });
            systemScripts = new();
            foreach (var systemObj in systems)
                systemScripts.Add(systemObj as MonoScript);
            
            initSystems = new();
            updateSystems = new();
            lateUpdateSystems = new();
            fixedUpdateSystems = new();
            lateFixedUpdateSystems = new();
            enableSystems = new();
            disableSystems = new();
            reactiveSystems = new();

            foreach (var ms in systemScripts)
            {
                var t = ms.GetClass();
                if (!t.IsSubclassOf(typeof(EcsSystem)))
                    continue;
                var attribute = t.GetCustomAttribute<SystemAttribute>();
                var categories = attribute != null ? attribute.Categories : new[] { ESystemCategory.Update };
                foreach (var category in categories)
                {
                    switch (category)
                    {
                        case ESystemCategory.Init:
                            initSystems.Add(ms);
                            break;
                        case ESystemCategory.Update:
                            updateSystems.Add(ms);
                            break;
                        case ESystemCategory.LateUpdate:
                            lateUpdateSystems.Add(ms);
                            break;
                        case ESystemCategory.FixedUpdate:
                            fixedUpdateSystems.Add(ms);
                            break;
                        case ESystemCategory.LateFixedUpdate:
                            lateFixedUpdateSystems.Add(ms);
                            break;
                        case ESystemCategory.OnEnable:
                            enableSystems.Add(ms);
                            break;
                        case ESystemCategory.OnDisable:
                            disableSystems.Add(ms);
                            break;
                        case ESystemCategory.Reactive:
                            reactiveSystems.Add(ms);
                            break;
                    }
                }
            }

            _initList = InitializeSystemList("_initSystemScripts");
            _updateList = InitializeSystemList("_updateSystemScripts");
            _lateUpdateList = InitializeSystemList("_lateUpdateSystemScripts");
            _fixedUpdateList = InitializeSystemList("_fixedUpdateSystemScripts");
            _lateFixedUpdateList = InitializeSystemList("_lateFixedUpdateSystemScripts");
            _enableList = InitializeSystemList("_enableSystemScripts");
            _disableList = InitializeSystemList("_disableSystemScripts");
            _reactiveList = InitializeSystemList("_reactiveSystemScripts");
        }

        public override VisualElement CreateInspectorGUI()
        {
            Initialize();
            return base.CreateInspectorGUI();
        }
        
        public override void OnInspectorGUI()
        {
            var listText = _addListExpanded ? "Shrink systems list" : "Expand systems list";
            if (GUILayout.Button(new GUIContent(listText), GUILayout.ExpandWidth(false)))
                _addListExpanded = !_addListExpanded;
            if (_addListExpanded)
            {
                _addSearch = EditorGUILayout.TextField(_addSearch);
                EditorGUILayout.BeginVertical();
                DrawAddList(InitSystemsLabel, initSystems, Pipeline._initSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.Init));
                DrawAddList(UpdateSystemsLabel, updateSystems, Pipeline._updateSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.Update));
                DrawAddList(LateUpdateSystemsLabel, lateUpdateSystems, Pipeline._lateUpdateSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.LateUpdate));
                DrawAddList(FixedSystemsLabel, fixedUpdateSystems, Pipeline._fixedUpdateSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.FixedUpdate));
                DrawAddList(LateFixedSystemsLabel, lateFixedUpdateSystems, Pipeline._lateFixedUpdateSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.LateFixedUpdate));
                DrawAddList(EnableSystemsLabel, enableSystems, Pipeline._enableSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.OnEnable));
                DrawAddList(DisableSystemsLabel, disableSystems, Pipeline._disableSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.OnDisable));
                DrawAddList(ReactiveSystemsLabel, reactiveSystems, Pipeline._reactiveSystemScripts,
                    script => OnAddSystem(script, ESystemCategory.Reactive));
                EditorGUILayout.EndVertical();
            }

            _addedSearch = EditorGUILayout.TextField(_addedSearch);
            //TODO: fold systems lists
            DrawSystemCategory(_initList, "_initSystemScripts");
            DrawSystemCategory(_updateList, "_updateSystemScripts");
            DrawSystemCategory(_lateUpdateList, "_lateUpdateSystemScripts");
            DrawSystemCategory(_fixedUpdateList, "_fixedUpdateSystemScripts");
            DrawSystemCategory(_lateFixedUpdateList, "_lateFixedUpdateSystemScripts");
            DrawSystemCategory(_enableList, "_enableSystemScripts");
            DrawSystemCategory(_disableList, "_disableSystemScripts");
            DrawSystemCategory(_reactiveList, "_reactiveSystemScripts");
        }

        private static bool ShouldSkipItem(MonoScript item, ECSPipeline.SystemEntry[] skippedItems)
        {
            for (var i = 0; i < skippedItems.Length; i++)
            {
                if (item == skippedItems[i].Script)
                    return true;
            }

            return false;
        }

        public void DrawAddList(string label, List<MonoScript> systems, ECSPipeline.SystemEntry[] except, Action<MonoScript> onAdd)
        {
            EditorGUILayout.LabelField(label + ':');
            GUILayout.Space(10);
            for (var i = 0; i < systems.Count; i++)
            {
                var systemScript = systems[i];
                var systemName = systemScript.GetClass().FullName;
                if (!IntegrationHelper.IsSearchMatch(_addSearch, systemName) || ShouldSkipItem(systemScript, except))
                    continue;

                EditorGUILayout.BeginHorizontal();

                //TODO: add lines between components for readability
                //      or remove "+" button and make buttons with component names on it
                EditorGUILayout.ObjectField(systemScript, typeof(MonoScript), false);
                bool tryAdd = GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false));
                if (tryAdd)
                    onAdd(systemScript);

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
        }

        private void DrawSystemCategory(ReorderableList list, string propertyName)
        {
            if (list.count == 0)
                return;
            GUILayout.Space(10);
            //TODO: use proper human-readable name and optimize
            EditorGUILayout.LabelField(propertyName);
            
            // Update the serialized object
            serializedObject.Update();

            SerializedProperty arrayProperty = GetCachedSerializedProperty(propertyName);
            
            // Filter the list and draw only matching elements
            if (!string.IsNullOrEmpty(_addedSearch))
            {
                for (int i = 0; i < arrayProperty.arraySize; i++)
                {
                    var element = arrayProperty.GetArrayElementAtIndex(i);
                    var scriptProp = element.FindPropertyRelative("Script");

                    if (scriptProp.objectReferenceValue != null &&
                        scriptProp.objectReferenceValue.name.Contains(_addedSearch, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                        var activeProp = element.FindPropertyRelative("Active");
                        DrawElement(rect, scriptProp, activeProp, arrayProperty, i);
                    }
                }
            }
            else
            {
                // No filter, draw the full list
                list.DoLayoutList();
            }
            
            if (_removedElement > -1)
            {
                arrayProperty.DeleteArrayElementAtIndex(_removedElement);
                _removedElement = -1;
                EditorUtility.SetDirty(target);
            }

            // Apply changes back to the serialized object
            serializedObject.ApplyModifiedProperties();
        }

        private void OnAddSystem(MonoScript script, ESystemCategory systemCategory)
        {
            //_addListExpanded = false;

            var pipeline = Pipeline;
            if (pipeline.AddSystem(script, systemCategory))
                EditorUtility.SetDirty(target);
        }
    }
}