using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ECSPipeline))]
public class Pipeline_Inspector : Editor
{
    //add more system types if needed
    private const string InitSystemsLabel = "Init Systems";
    private const string UpdateSystemsLabel = "Update Systems";
    private const string LateUpdateSystemsLabel = "Late Update Systems";
    private const string FixedSystemsLabel = "Fixed Update Systems";
    private const string LateFixedSystemsLabel = "Late Fixed Update Systems";
    private const string ReactiveSystemsLabel = "Reactive Systems";

    private static List<string> initSystemTypeNames;
    private static List<string> updateSystemTypeNames;
    private static List<string> lateUpdateSystemTypeNames;
    private static List<string> fixedUpdateSystemTypeNames;
    private static List<string> lateFixedUpdateSystemTypeNames;

    private UnityEngine.Object[] systemScripts;

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

    public override VisualElement CreateInspectorGUI()
    {
        systemScripts = Resources.FindObjectsOfTypeAll(typeof(MonoScript));

        return base.CreateInspectorGUI();
    }

    static Pipeline_Inspector()
    {
        initSystemTypeNames = new List<string>();
        updateSystemTypeNames = new List<string>();
        lateUpdateSystemTypeNames = new List<string>();
        fixedUpdateSystemTypeNames = new List<string>();
        lateFixedUpdateSystemTypeNames = new List<string>();

        foreach (var t in Assembly.GetAssembly(typeof(EcsSystem)).GetTypes())
        {
            if (!t.IsSubclassOf(typeof(EcsSystem)))
                continue;
            var attribute = t.GetCustomAttribute<SystemAttribute>();
            var categories = attribute != null ? attribute.Categories : new[] { ESystemCategory.Update };
            foreach (var category in categories)
            {
                if (category == ESystemCategory.Init)
                    initSystemTypeNames.Add(t.FullName);
                if (category == ESystemCategory.Update)
                    updateSystemTypeNames.Add(t.FullName);
                if (category == ESystemCategory.LateUpdate)
                    lateUpdateSystemTypeNames.Add(t.FullName);
                if (category == ESystemCategory.FixedUpdate)
                    fixedUpdateSystemTypeNames.Add(t.FullName);
                if (category == ESystemCategory.LateFixedUpdate)
                    lateFixedUpdateSystemTypeNames.Add(t.FullName);
            }
        }
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
                DrawAddList(InitSystemsLabel, initSystemTypeNames, Pipeline._initSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.Init));
                DrawAddList(UpdateSystemsLabel, updateSystemTypeNames, Pipeline._updateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.Update));
                DrawAddList(LateUpdateSystemsLabel, lateUpdateSystemTypeNames, Pipeline._lateUpdateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.LateUpdate));
                DrawAddList(FixedSystemsLabel, fixedUpdateSystemTypeNames, Pipeline._fixedUpdateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.FixedUpdate));
                DrawAddList(LateFixedSystemsLabel, lateFixedUpdateSystemTypeNames, Pipeline._lateFixedUpdateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.LateFixedUpdate));
            EditorGUILayout.EndVertical();
        }

        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        DrawSystemCategory(ESystemCategory.Init);
        DrawSystemCategory(ESystemCategory.Update);
        DrawSystemCategory(ESystemCategory.LateUpdate);
        DrawSystemCategory(ESystemCategory.FixedUpdate);
        DrawSystemCategory(ESystemCategory.LateFixedUpdate);
    }

    private static bool ShouldSkipItem(string item, string[] skippedItems)
    {
        foreach (var skippedItem in skippedItems)
        {
            if (item == skippedItem)
                return true;
        }
        return false;
    }

    public void DrawAddList(string label, List<string> systems, string[] except, Action<string> onAdd)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var systemName in systems)
        {
            if (!IntegrationHelper.IsSearchMatch(_addSearch, systemName) || ShouldSkipItem(systemName, except))
                continue;

            EditorGUILayout.BeginHorizontal();

            //TODO: add lines between components for readability
            //      or remove "+" button and make buttons with component names on it
            EditorGUILayout.ObjectField(GetSystemScriptByName(systemName), typeof(MonoScript), false);
            bool tryAdd = GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false));
            if (tryAdd)
                onAdd(systemName);

            EditorGUILayout.EndHorizontal();
        }
        GUILayout.Space(10);
    }

    private MonoScript GetSystemScriptByName(string name)
    {
        MonoScript systemScript = null;
        foreach (var script in systemScripts)
        {
            if (script.name == name)
            {
                systemScript = script as MonoScript;
                break;
            }
        }

        return systemScript;
    }

    private void DrawSystemCategory(ESystemCategory category)
    {
        string[] systems;
        bool[] switches;
        //TODO: this switch duplicated in ECSPipeline, refactor
        switch (category)
        {
            case ESystemCategory.Init:
                systems = Pipeline._initSystemTypeNames;
                switches = Pipeline._initSwitches;
                break;
            case ESystemCategory.Update:
                systems = Pipeline._updateSystemTypeNames;
                switches = Pipeline._updateSwitches;
                break;
            case ESystemCategory.LateUpdate:
                systems = Pipeline._lateUpdateSystemTypeNames;
                switches = Pipeline._lateUpdateSwitches;
                break;
            case ESystemCategory.FixedUpdate:
                systems = Pipeline._fixedUpdateSystemTypeNames;
                switches = Pipeline._fixedUpdateSwitches;
                break;
            case ESystemCategory.LateFixedUpdate:
                systems = Pipeline._lateFixedUpdateSystemTypeNames;
                switches = Pipeline._lateFixedUpdateSwitches;
                break;
            default:
                return;
        }

        if (systems.Length == 0)
            return;

        GUILayout.Space(10);
        EditorGUILayout.LabelField(category.ToString());

        for (int i = 0; i < systems.Length; i++)
        {
            if (!IntegrationHelper.IsSearchMatch(_addedSearch, systems[i]))
                continue;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.ObjectField(GetSystemScriptByName(systems[i]), typeof(MonoScript), false);
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

    private void OnAddSystem(string systemName, ESystemCategory systemCategory)
    {
        //_addListExpanded = false;

        var pipeline = Pipeline;
        if (pipeline.AddSystem(systemName, systemCategory))
            EditorUtility.SetDirty(target);
    }
}
