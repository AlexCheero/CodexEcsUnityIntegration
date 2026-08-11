using UnityEditor;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(EntityPreset))]
    public class EntityPresetEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        
        private void OnEnable() => _componentsProp = serializedObject.FindProperty(EntityPreset.ComponentsPropertyName);

        public override void OnInspectorGUI()
        {
            var so = (EntityPreset)target;
            serializedObject.Update();

            EntityEditorHelper.DrawComponentsInspector(_componentsProp, so.Components, so);

            serializedObject.ApplyModifiedProperties();
        }
    }
}