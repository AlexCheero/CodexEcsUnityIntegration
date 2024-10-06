using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
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
                    (script) => OnAddSystem(script, ESystemCategory.Init));
                DrawAddList(UpdateSystemsLabel, updateSystems, Pipeline._updateSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.Update));
                DrawAddList(LateUpdateSystemsLabel, lateUpdateSystems, Pipeline._lateUpdateSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.LateUpdate));
                DrawAddList(FixedSystemsLabel, fixedUpdateSystems, Pipeline._fixedUpdateSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.FixedUpdate));
                DrawAddList(LateFixedSystemsLabel, lateFixedUpdateSystems, Pipeline._lateFixedUpdateSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.LateFixedUpdate));
                DrawAddList(EnableSystemsLabel, enableSystems, Pipeline._enableSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.OnEnable));
                DrawAddList(DisableSystemsLabel, disableSystems, Pipeline._disableSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.OnDisable));
                DrawAddList(ReactiveSystemsLabel, reactiveSystems, Pipeline._reactiveSystemScripts,
                    (script) => OnAddSystem(script, ESystemCategory.Reactive));
                EditorGUILayout.EndVertical();
            }

            _addedSearch = EditorGUILayout.TextField(_addedSearch);
            DrawSystemCategory(ESystemCategory.Init);
            DrawSystemCategory(ESystemCategory.Update);
            DrawSystemCategory(ESystemCategory.LateUpdate);
            DrawSystemCategory(ESystemCategory.FixedUpdate);
            DrawSystemCategory(ESystemCategory.LateFixedUpdate);
            DrawSystemCategory(ESystemCategory.OnEnable);
            DrawSystemCategory(ESystemCategory.OnDisable);
            DrawSystemCategory(ESystemCategory.Reactive);
        }

        private static bool ShouldSkipItem(MonoScript item, MonoScript[] skippedItems)
        {
            foreach (var skippedItem in skippedItems)
            {
                if (item == skippedItem)
                    return true;
            }
            return false;
        }

        public void DrawAddList(string label, List<MonoScript> systems, MonoScript[] except, Action<MonoScript> onAdd)
        {
            EditorGUILayout.LabelField(label + ':');
            GUILayout.Space(10);
            foreach (var systemScript in systems)
            {
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

        private void DrawSystemCategory(ESystemCategory category)
        {
            MonoScript[] scripts;
            bool[] switches;
            //TODO: this switch duplicated in ECSPipeline, refactor
            switch (category)
            {
                case ESystemCategory.Init:
                    scripts = Pipeline._initSystemScripts;
                    switches = Pipeline._initSwitches;
                    break;
                case ESystemCategory.Update:
                    scripts = Pipeline._updateSystemScripts;
                    switches = Pipeline._updateSwitches;
                    break;
                case ESystemCategory.LateUpdate:
                    scripts = Pipeline._lateUpdateSystemScripts;
                    switches = Pipeline._lateUpdateSwitches;
                    break;
                case ESystemCategory.FixedUpdate:
                    scripts = Pipeline._fixedUpdateSystemScripts;
                    switches = Pipeline._fixedUpdateSwitches;
                    break;
                case ESystemCategory.LateFixedUpdate:
                    scripts = Pipeline._lateFixedUpdateSystemScripts;
                    switches = Pipeline._lateFixedUpdateSwitches;
                    break;
                case ESystemCategory.OnEnable:
                    scripts = Pipeline._enableSystemScripts;
                    switches = Pipeline._enableSwitches;
                    break;
                case ESystemCategory.OnDisable:
                    scripts = Pipeline._disableSystemScripts;
                    switches = Pipeline._disableSwitches;
                    break;
                case ESystemCategory.Reactive:
                    scripts = Pipeline._reactiveSystemScripts;
                    switches = Pipeline._reactiveSwitches;
                    break;
                default:
                    return;
            }

            if (scripts.Length == 0)
                return;

            GUILayout.Space(10);
            EditorGUILayout.LabelField(category.ToString());

            for (int i = 0; i < scripts.Length; i++)
            {
                if (!IntegrationHelper.IsSearchMatch(_addedSearch, scripts[i].GetClass().FullName))
                    continue;

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.ObjectField(scripts[i], typeof(MonoScript), false);
                bool newState = EditorGUILayout.Toggle(switches[i]);
                if (newState != switches[i])
                    EditorUtility.SetDirty(target);
                switches[i] = newState;
                if (GUILayout.Button(new GUIContent("-"), GUILayout.ExpandWidth(false)))
                {
                    Pipeline.RemoveMetaAt(category, i);
                    i--;
                    EditorUtility.SetDirty(target);
                }

                if (GUILayout.Button(new GUIContent("^"), GUILayout.ExpandWidth(false)))
                {
                    if (Pipeline.Move(category, i, true))
                        EditorUtility.SetDirty(target);
                }
                if (GUILayout.Button(new GUIContent("v"), GUILayout.ExpandWidth(false)))
                {
                    if (Pipeline.Move(category, i, false))
                        EditorUtility.SetDirty(target);
                }

                EditorGUILayout.EndHorizontal();
            }
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