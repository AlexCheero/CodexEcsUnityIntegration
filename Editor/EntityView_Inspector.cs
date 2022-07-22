using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


//TODO: hide some components fields that shouldn't be visible in inspector such as AttackComponent.previousAttackTime
//TODO: implement runtime fileds update
//TODO: implement search bar for components
[CustomEditor(typeof(EntityView))]
public class EntityView_Inspector : Editor
{
    private const string UnityComponents = "UnityComponents";

    private static string[] componentTypeNames;
    private static string[] tagTypeNames;

    private List<List<string>> _viewComponentTypeNames;
    private List<bool> _viewComponentFoldouts;
    private bool _addListExpanded;
    private string _addSearch;
    private string _addedSearch;

    private EntityView _view;
    private EntityView View
    {
        get
        {
            if (_view == null)
                _view =(EntityView)target;
            return _view;
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        //TODO: it doesn't get all the components. to get most of them EntityView script should
        //      be the last script of GO, and still it can't get the EntityView script itself
        var viewComponents = View.GetComponents<Component>();
        var length = viewComponents.Length - 1;
        _viewComponentTypeNames = new List<List<string>>(length);
        _viewComponentFoldouts = new List<bool>(length);
        for (int i = 0, j = 0; i < viewComponents.Length && j < length; i++, j++)
        {
            _viewComponentFoldouts.Add(false);
            var subTypesList = new List<string>();
            var subType = viewComponents[i].GetType();
            while (subType != typeof(Component))
            {
                subTypesList.Add(subType.FullName);
                subType = subType.BaseType;
            }
            _viewComponentTypeNames.Add(subTypesList);
        }

        return base.CreateInspectorGUI();
    }

    static EntityView_Inspector()
    {
        componentTypeNames = IntegrationHelper.GetTypeNames<EntityView>((t) => t.Namespace == IntegrationHelper.Components);
        tagTypeNames = IntegrationHelper.GetTypeNames<EntityView>((t) => t.Namespace == IntegrationHelper.Tags);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var listText = _addListExpanded ? "Shrink components list" : "Expand components list";
        if (GUILayout.Button(new GUIContent(listText), GUILayout.ExpandWidth(false)))
            _addListExpanded = !_addListExpanded;
        if (_addListExpanded)
        {
            _addSearch = EditorGUILayout.TextField(_addSearch);
            EditorGUILayout.BeginVertical();
                DrawAddList(IntegrationHelper.Components, componentTypeNames, OnAddComponent, _addSearch);
                GUILayout.Space(10);
                DrawAddList(IntegrationHelper.Tags, tagTypeNames, OnAddComponent, _addSearch);
                GUILayout.Space(10);
                DrawAddUnityComponentList(UnityComponents, _viewComponentTypeNames, _viewComponentFoldouts, OnAddComponent, _addSearch);
                GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        for (int i = 0; i < View.MetasLength; i++)
        {
            if (!IntegrationHelper.IsSearchMatch(_addedSearch, View.GetMeta(i).ComponentName))
                continue;

            EditorGUILayout.BeginHorizontal();

            DrawComponent(ref View.GetMeta(i));

            //TODO: delete button moves outside of the screen when foldout is expanded
            //component delete button
            if (GUILayout.Button(new GUIContent("-"), GUILayout.ExpandWidth(false)))
            {
                View.RemoveMetaAt(i);
                i--;
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawAddList(string label, string[] components, Action<string> onAdd, string search)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        foreach (var componentName in components)
        {
            if (!IntegrationHelper.IsSearchMatch(search, componentName) || IsComponentAlreadyAdded(componentName))
                continue;

            EditorGUILayout.BeginHorizontal();

            //TODO: add lines between components for readability
            //      or remove "+" button and make buttons with component names on it
            EditorGUILayout.LabelField(IntegrationHelper.GetTypeUIName(componentName));
            if (GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                onAdd(componentName);

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawAddUnityComponentList(string label, List<List<string>> components, List<bool> foldouts, Action<string> onAdd, string search)
    {
        EditorGUILayout.LabelField(label + ':');
        GUILayout.Space(10);
        for (int i = 0; i < components.Count; i++)
        {
            var compSubTypes = components[i];
            if (compSubTypes.Count == 0)
                continue;


            EditorGUILayout.BeginHorizontal();

            foldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(foldouts[i], IntegrationHelper.GetTypeUIName(compSubTypes[0]));
            bool canAddFirstComponent =
                IntegrationHelper.IsSearchMatch(search, compSubTypes[0]) && !IsComponentAlreadyAdded(compSubTypes[0]);
            if (canAddFirstComponent && GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                onAdd(compSubTypes[0]);

            EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                for (int j = 1; j < compSubTypes.Count; j++)
                {
                    var componentName = compSubTypes[j];
                    if (!IntegrationHelper.IsSearchMatch(search, componentName) || IsComponentAlreadyAdded(componentName))
                        continue;

                    EditorGUILayout.BeginHorizontal();

                    //TODO: add lines between components for readability
                    //      or remove "+" button and make buttons with component names on it
                    EditorGUILayout.LabelField("add as " + IntegrationHelper.GetTypeUIName(componentName));
                    if (GUILayout.Button(new GUIContent("+"), GUILayout.ExpandWidth(false)))
                        onAdd(componentName);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

    private bool IsComponentAlreadyAdded(string component)
    {
        for (int i = 0; i < View.MetasLength; i++)
        {
            if (component == View.GetMeta(i).ComponentName)
                return true;
        }
        return false;
    }

    private void OnAddComponent(string componentName)
    {
        _addListExpanded = false;
        var type = IntegrationHelper.GetTypeByName(componentName, EGatheredTypeCategory.UnityComponent);
        if (EntityView.IsUnityComponent(type))
        {
            MethodInfo getComponentInfo = typeof(EntityView).GetMethod("GetComponent", new Type[] { }).MakeGenericMethod(type);
            var component = (Component)getComponentInfo.Invoke(View, null);
            if (View.AddUnityComponent(component, type))
                EditorUtility.SetDirty(target);
        }
        else
        {
            if (View.AddComponent(componentName))
                EditorUtility.SetDirty(target);
        }
    }

    //TODO: implement drag'n'drop for components
    private void DrawComponent(ref ComponentMeta meta)
    {
        EditorGUILayout.BeginVertical();
        {
            //TODO: draw tags without arrow
            meta.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(meta.IsExpanded, IntegrationHelper.GetTypeUIName(meta.ComponentName));
            if (meta.IsExpanded && meta.Fields != null)
            {
                for (int i = 0; i < meta.Fields.Length; i++)
                {
                    if (!meta.Fields[i].IsHiddenInEditor)
                        DrawField(ref meta.Fields[i]);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawField(ref ComponentFieldMeta fieldMeta)
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField(fieldMeta.Name);
            var valueObject = fieldMeta.GetValue();

            bool setDirty;
            if (fieldMeta.TypeName == typeof(int).FullName)
            {
                var intValue = valueObject != null ? (int)valueObject : default(int);
                setDirty = fieldMeta.SetValue(EditorGUILayout.IntField(intValue));
            }
            else if (fieldMeta.TypeName == typeof(float).FullName)
            {
                var floatValue = valueObject != null ? (float)valueObject : default(float);
                setDirty = fieldMeta.SetValue(EditorGUILayout.FloatField(floatValue));
            }
            else if (fieldMeta.TypeName == typeof(Vector3).FullName)
            {
                var vec3Value = valueObject != null ? (Vector3)valueObject : default(Vector3);
                setDirty = fieldMeta.SetValue(EditorGUILayout.Vector3Field("", vec3Value));
            }
            else if (nameof(EntityPreset) == fieldMeta.TypeName)
            {
                var obj = valueObject != null ? (EntityPreset)valueObject : null;
                setDirty = fieldMeta.SetValue(EditorGUILayout.ObjectField("", obj, typeof(EntityPreset), true));
            }
            else if (fieldMeta.TypeName == typeof(string).FullName)
            {
                var str = valueObject as string;
                setDirty = fieldMeta.SetValue(EditorGUILayout.TextField(str));
            }
            else
            {
                var type = IntegrationHelper.GetTypeByName(fieldMeta.TypeName, EGatheredTypeCategory.UnityComponent);
                var obj = valueObject != null ? (Component)valueObject : null;
                setDirty = fieldMeta.SetValue(EditorGUILayout.ObjectField("", obj, type, true));
            }

            if (setDirty)
                EditorUtility.SetDirty(target);
        }
        EditorGUILayout.EndHorizontal();
    }
}
