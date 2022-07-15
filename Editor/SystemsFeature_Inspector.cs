
using ECS;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(SystemsFeature))]
public class SystemsFeature_Inspector : Editor
{
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

    private SystemsFeature _feature;
    private SystemsFeature Feature
    {
        get
        {
            if (_feature == null)
                _feature = (SystemsFeature)target;
            return _feature;
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        systemScripts = Resources.FindObjectsOfTypeAll(typeof(MonoScript));

        return base.CreateInspectorGUI();
    }

    static SystemsFeature_Inspector()
    {
        initSystemTypeNames = IntegrationHelper.GetTypeNames<SystemsFeature>(
            (t) => IsSystemType(t) && IntegrationHelper.HaveAttribute<InitSystemAttribute>(t));

        updateSystemTypeNames = IntegrationHelper.GetTypeNames<SystemsFeature>(
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

        fixedUpdateSystemTypeNames = IntegrationHelper.GetTypeNames<SystemsFeature>(
            (t) => IsSystemType(t) && IntegrationHelper.HaveAttribute<FixedUpdateSystemAttribute>(t));

        reactiveSystemTypeNames = IntegrationHelper.GetTypeNames<SystemsFeature>(
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
            string label = null;
            string[] systemNames = null;
            switch (Feature._category)
            {
                case ESystemCategory.Init:
                    label = InitSystemsLabel;
                    systemNames = initSystemTypeNames;
                    break;
                case ESystemCategory.Update:
                    label = UpdateSystemsLabel;
                    systemNames = updateSystemTypeNames;
                    break;
                case ESystemCategory.FixedUpdate:
                    label = FixedSystemsLabel;
                    systemNames = fixedUpdateSystemTypeNames;
                    break;
                case ESystemCategory.Reactive:
                    label = ReactiveSystemsLabel;
                    systemNames = reactiveSystemTypeNames;
                    break;
                default:
                    return;
            }

            _addSearch = EditorGUILayout.TextField(_addSearch);
            EditorGUILayout.BeginVertical();
            DrawAddList(label, systemNames, Feature._systems,
                (name) => OnAddSystem(name));
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        var newCategory = (ESystemCategory)EditorGUILayout.EnumPopup("Category", Feature._category);
        if (Feature._category != newCategory)
        {
            for (int i = Feature._systems.Length - 1; i >= 0; i--)
                Feature.RemoveMetaAt(i);
        }
        Feature._category = newCategory;

        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        DrawSystems();
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

    private void DrawSystems()
    {
        if (Feature._systems.Length == 0)
            return;

        GUILayout.Space(10);
        EditorGUILayout.LabelField(Feature._category.ToString());

        for (int i = 0; i < Feature._systems.Length; i++)
        {
            if (!IntegrationHelper.IsSearchMatch(_addedSearch, Feature._systems[i]))
                continue;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.ObjectField(GetSystemScriptByName(Feature._systems[i]), typeof(MonoScript), false);
            bool newState = EditorGUILayout.Toggle(Feature._switches[i]);
            if (newState != Feature._switches[i])
                EditorUtility.SetDirty(target);
            Feature._switches[i] = newState;
            if (GUILayout.Button(new GUIContent("-"), GUILayout.ExpandWidth(false)))
            {
                Feature.RemoveMetaAt(i);
                i--;
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button(new GUIContent("^"), GUILayout.ExpandWidth(false)))
            {
                if (Feature.Move(i, true))
                    EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button(new GUIContent("v"), GUILayout.ExpandWidth(false)))
            {
                if (Feature.Move(i, false))
                    EditorUtility.SetDirty(target);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void OnAddSystem(string systemName)
    {
        //_addListExpanded = false;

        if (Feature.AddSystem(systemName))
            EditorUtility.SetDirty(target);
    }
}
