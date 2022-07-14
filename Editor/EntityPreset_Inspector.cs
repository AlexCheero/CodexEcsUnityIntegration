using UnityEditor;
using UnityEngine;

//TODO: almost everything is copied from EntityView_Inspector refactor
[CustomEditor(typeof(EntityPreset))]
public class EntityPreset_Inspector : Editor
{
    private static string[] componentTypeNames;
    private static string[] tagTypeNames;

    private bool _addListExpanded;
    private string _addSearch;
    private string _addedSearch;

    private EntityPreset View { get => (EntityPreset)target; }

    static EntityPreset_Inspector()
    {
        componentTypeNames = IntegrationHelper.GetTypeNames<EntityPreset>((t) => t.Namespace == IntegrationHelper.Components);
        tagTypeNames = IntegrationHelper.GetTypeNames<EntityPreset>((t) => t.Namespace == IntegrationHelper.Tags);
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
            IntegrationHelper.DrawAddList(IntegrationHelper.Components, componentTypeNames, OnAddComponent, _addSearch);
            GUILayout.Space(10);
            IntegrationHelper.DrawAddList(IntegrationHelper.Tags, tagTypeNames, OnAddComponent, _addSearch);
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        var view = View;
        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        for (int i = 0; i < view.MetasLength; i++)
        {
            if (!IntegrationHelper.IsSearchMatch(_addedSearch, view.GetMeta(i).ComponentName))
                continue;

            EditorGUILayout.BeginHorizontal();

            DrawComponent(ref view.GetMeta(i));

            //TODO: delete button moves outside of the screen when foldout is expanded
            //component delete button
            if (GUILayout.Button(new GUIContent("-"), GUILayout.ExpandWidth(false)))
            {
                view.RemoveMetaAt(i);
                i--;
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void OnAddComponent(string componentName)
    {
        _addListExpanded = false;
        if (View.AddComponent(componentName))
            EditorUtility.SetDirty(target);
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
                    DrawField(ref meta.Fields[i]);
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
