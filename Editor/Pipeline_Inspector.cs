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
        private static Dictionary<ESystemCategory, string> _systemLabels;
        private static Dictionary<ESystemCategory, List<MonoScript>> _systemScripts;

        private bool _addListExpanded;
        private string _addSearch;
        private string _addedSearch;
        
        //TODO: rewrite with Dictionary lookups like _systemScripts. don't forget to rewrite EcsPipeline SystemScripts properties also
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
                var scriptProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.Script));
                var activeProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.Active));
                var nonPausableProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.NonPausable));

                DrawElement(rect, scriptProp, activeProp, nonPausableProp, index);
            };

            reorderableList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight;

            // Set up element filtering logic (we'll filter during GUI rendering)
            reorderableList.onCanAddCallback = _ => true;
            reorderableList.onCanRemoveCallback = _ => true;
            
            //TODO: on system's position changed in runtime doesn't change the position of runtime system instance

            return reorderableList;
        }
        
        private int _removedElement = -1;
        private GUIContent _systemToggleContent = new GUIContent();
        private void DrawElement(Rect rect, SerializedProperty scriptProp, SerializedProperty activeProp, SerializedProperty nonPausableProp, int index)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var nearestLeft = rect.width - lineHeight;
            const float gap = 3.0f;
            if (GUI.Button(new Rect(rect.x + nearestLeft, rect.y, lineHeight, lineHeight), "-"))
                _removedElement = index;
            nearestLeft -= 15 + gap;

            nonPausableProp.boolValue = EditorGUI.Toggle(new Rect(rect.x + nearestLeft, rect.y, lineHeight, lineHeight), nonPausableProp.boolValue);
            nearestLeft -= gap;
            const string nonPausableLabel = "N:";
            const float widthPerCharacter = 6.5f;
            float labelWidth = nonPausableLabel.Length * widthPerCharacter;
            nearestLeft -= labelWidth + 1;
            _systemToggleContent.text = nonPausableLabel;
            _systemToggleContent.tooltip = "Non pausable";
            EditorGUI.LabelField(new Rect(rect.x + nearestLeft, rect.y, labelWidth + lineHeight + gap, lineHeight), _systemToggleContent);

            nearestLeft -= lineHeight + gap;
            activeProp.boolValue = EditorGUI.Toggle(new Rect(rect.x + nearestLeft, rect.y, lineHeight, lineHeight), activeProp.boolValue);
            const string activeLabel = "A:";
            labelWidth = activeLabel.Length * widthPerCharacter;
            nearestLeft -= labelWidth + 1;
            _systemToggleContent.text = activeLabel;
            _systemToggleContent.tooltip = "Active";
            EditorGUI.LabelField(new Rect(rect.x + nearestLeft, rect.y, labelWidth + lineHeight + gap, lineHeight), _systemToggleContent);

            nearestLeft -= gap;
            EditorGUI.ObjectField(new Rect(rect.x, rect.y, nearestLeft, lineHeight), scriptProp, GUIContent.none);
        }

        private void Initialize()
        {
            var systemsObjects = Resources.FindObjectsOfTypeAll(typeof(MonoScript)).Where(obj =>
            {
                var monoScript = obj as MonoScript;
                return monoScript != null && typeof(EcsSystem).IsAssignableFrom(monoScript.GetClass());
            });

            var allCategories = IntegrationHelper.SystemCategories;

            _systemLabels = new(allCategories.Length);
            //TODO: not working for now
            // foreach (var category in allCategories)
            //     _systemLabels[category] = category.ToString();
            _systemLabels[ESystemCategory.Init] = nameof(ECSPipeline._initSystemScripts);
            _systemLabels[ESystemCategory.Update] = nameof(ECSPipeline._updateSystemScripts);
            _systemLabels[ESystemCategory.LateUpdate] = nameof(ECSPipeline._lateUpdateSystemScripts);
            _systemLabels[ESystemCategory.FixedUpdate] = nameof(ECSPipeline._fixedUpdateSystemScripts);
            _systemLabels[ESystemCategory.LateFixedUpdate] = nameof(ECSPipeline._lateFixedUpdateSystemScripts);
            _systemLabels[ESystemCategory.OnEnable] = nameof(ECSPipeline._enableSystemScripts);
            _systemLabels[ESystemCategory.OnDisable] = nameof(ECSPipeline._disableSystemScripts);
            _systemLabels[ESystemCategory.Reactive] = nameof(ECSPipeline._reactiveSystemScripts);
            //===================================================
            
            _systemScripts = new(allCategories.Length);
            foreach (var category in allCategories)
                _systemScripts[category] = new();

            foreach (var systemObject in systemsObjects)
            {
                var systemMonoscript = systemObject as MonoScript;
                if (systemMonoscript == null)
                {
                    Debug.LogError("System object is not a MonoScript");
                    continue;
                }
                var t = systemMonoscript.GetClass();
                if (!t.IsSubclassOf(typeof(EcsSystem)))
                    continue;
                var attribute = t.GetCustomAttribute<SystemAttribute>();
                var categories = attribute != null ? attribute.Categories : ESystemCategory.Update;
                foreach (var category in allCategories)
                {
                    if (categories.Has(category))
                        _systemScripts[category].Add(systemMonoscript);
                }
            }

            _initList = InitializeSystemList(nameof(ECSPipeline._initSystemScripts));
            _updateList = InitializeSystemList(nameof(ECSPipeline._updateSystemScripts));
            _lateUpdateList = InitializeSystemList(nameof(ECSPipeline._lateUpdateSystemScripts));
            _fixedUpdateList = InitializeSystemList(nameof(ECSPipeline._fixedUpdateSystemScripts));
            _lateFixedUpdateList = InitializeSystemList(nameof(ECSPipeline._lateFixedUpdateSystemScripts));
            _enableList = InitializeSystemList(nameof(ECSPipeline._enableSystemScripts));
            _disableList = InitializeSystemList(nameof(ECSPipeline._disableSystemScripts));
            _reactiveList = InitializeSystemList(nameof(ECSPipeline._reactiveSystemScripts));
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
                foreach (var category in IntegrationHelper.SystemCategories)
                {
                    DrawAddList(_systemLabels[category], _systemScripts[category], Pipeline.GetSystemScriptsByCategory(category),
                        script => OnAddSystem(script, category));
                }
                EditorGUILayout.EndVertical();
            }

            _addedSearch = EditorGUILayout.TextField(_addedSearch);
            //TODO: fold systems lists
            DrawSystemCategory(_initList, _systemLabels[ESystemCategory.Init]);
            DrawSystemCategory(_updateList, _systemLabels[ESystemCategory.Update]);
            DrawSystemCategory(_lateUpdateList, _systemLabels[ESystemCategory.LateUpdate]);
            DrawSystemCategory(_fixedUpdateList, _systemLabels[ESystemCategory.FixedUpdate]);
            DrawSystemCategory(_lateFixedUpdateList, _systemLabels[ESystemCategory.LateFixedUpdate]);
            DrawSystemCategory(_enableList, _systemLabels[ESystemCategory.OnEnable]);
            DrawSystemCategory(_disableList, _systemLabels[ESystemCategory.OnDisable]);
            DrawSystemCategory(_reactiveList, _systemLabels[ESystemCategory.Reactive]);
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
                    var scriptProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.Script));

                    if (scriptProp.objectReferenceValue != null &&
                        scriptProp.objectReferenceValue.name.Contains(_addedSearch, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                        var activeProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.Active));
                        var nonPausableProp = element.FindPropertyRelative(nameof(ECSPipeline.SystemEntry.NonPausable));
                        DrawElement(rect, scriptProp, activeProp, nonPausableProp, i);
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