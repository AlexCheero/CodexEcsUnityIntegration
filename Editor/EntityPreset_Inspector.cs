using UnityEditor;

//TODO: almost everything is copied from EntityView_Inspector refactor
[CustomEditor(typeof(EntityPreset))]
public class EntityPreset_Inspector : Editor
{
    private EntityInspectorCommonData _commonData;

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

    public override void OnInspectorGUI() =>
        IntegrationHelper.OnEntityInspectorGUI(serializedObject, target, ref _commonData, ref Preset.Data);
}
