using UnityEditor;

//TODO: almost everything is copied from EntityView_Inspector refactor
[CustomEditor(typeof(EntityPreset))]
public class EntityPreset_Inspector : Editor
{
    private bool _addListExpanded;
    private string _addSearch;
    private string _addedSearch;

    private EntityPreset _preset;
    private EntityPreset Preset
    {
        get
        {
            if (_preset == null)
                _preset = (EntityPreset)target;
            return _preset;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        IntegrationHelper.DrawAddComponents(ref _addListExpanded, _addSearch, Preset.Data, target);
        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        IntegrationHelper.DrawComponents(Preset.Data, _addedSearch, target);
    }
}
