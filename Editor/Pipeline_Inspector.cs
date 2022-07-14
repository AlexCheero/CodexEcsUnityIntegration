using ECS;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ECSPipeline))]
public class Pipeline_Inspector : Editor
{
    //add more system types if needed
    private const string InitSystemsLabel = "Init Systems";
    private const string UpdateSystemsLabel = "Update Systems";
    private const string FixedSystemsLabel = "Fixed Update Systems";
    private const string ReactiveSystemsLabel = "Reactive Systems";

    private static string[] initSystemTypeNames;
    private static string[] updateSystemTypeNames;
    private static string[] fixedUpdateSystemTypeNames;
    private static string[] reactiveSystemTypeNames;

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
        initSystemTypeNames = IntegrationHelper.GetTypeNames<ECSPipeline>(
            (t) => IsSystemType(t) && IntegrationHelper.HaveAttribute<InitSystemAttribute>(t));

        updateSystemTypeNames = IntegrationHelper.GetTypeNames<ECSPipeline>(
            //basically consider system without attribute as update system is added only for consistency
            (t) =>
            {
                if (!IsSystemType(t))
                    return false;

                if (IntegrationHelper.HaveAttribute<UpdateSystemAttribute>(t))
                    return true;

                var haveNoOtherAttributes = !IntegrationHelper.HaveAttribute<InitSystemAttribute>(t);
                haveNoOtherAttributes &= !IntegrationHelper.HaveAttribute<FixedUpdateSystemAttribute>(t);
                return haveNoOtherAttributes;
            });

        fixedUpdateSystemTypeNames = IntegrationHelper.GetTypeNames<ECSPipeline>(
            (t) => IsSystemType(t) && IntegrationHelper.HaveAttribute<FixedUpdateSystemAttribute>(t));

        reactiveSystemTypeNames = IntegrationHelper.GetTypeNames<ECSPipeline>(
            (t) => IntegrationHelper.HaveAttribute<ReactiveSystemAttribute>(t));
    }

    private static bool IsSystemType(Type type) => type != typeof(EcsSystem) && typeof(EcsSystem).IsAssignableFrom(type);

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
                GUILayout.Space(10);
                DrawAddList(UpdateSystemsLabel, updateSystemTypeNames, Pipeline._updateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.Update));
                GUILayout.Space(10);
                DrawAddList(FixedSystemsLabel, fixedUpdateSystemTypeNames, Pipeline._fixedUpdateSystemTypeNames,
                    (name) => OnAddSystem(name, ESystemCategory.FixedUpdate));
                GUILayout.Space(10);
                DrawAddList(ReactiveSystemsLabel, reactiveSystemTypeNames, Pipeline._reactiveSystemTypeNames,
                        (name) => OnAddSystem(name, ESystemCategory.Reactive));
                GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        DrawSystemCategory(ESystemCategory.Init);
        DrawSystemCategory(ESystemCategory.Update);
        DrawSystemCategory(ESystemCategory.FixedUpdate);
        DrawSystemCategory(ESystemCategory.Reactive);
    }

    public void DrawAddList(string label, string[] systems, string[] except, Action<string> onAdd)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var systemName in systems)
        {
            if (!IntegrationHelper.IsSearchMatch(_addSearch, systemName) || IntegrationHelper.ShouldSkipItem(systemName, except))
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
            case ESystemCategory.FixedUpdate:
                systems = Pipeline._fixedUpdateSystemTypeNames;
                switches = Pipeline._fixedUpdateSwitches;
                break;
            case ESystemCategory.Reactive:
                systems = Pipeline._reactiveSystemTypeNames;
                switches = Pipeline._reactiveSwitches;
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
        _addListExpanded = false;

        var pipeline = Pipeline;
        if (pipeline.AddSystem(systemName, systemCategory))
            EditorUtility.SetDirty(target);
    }
}
