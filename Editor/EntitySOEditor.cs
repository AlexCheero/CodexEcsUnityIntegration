using UnityEditor;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(EntitySO))]
    public class EntitySOEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        
        private void OnEnable() => _componentsProp = serializedObject.FindProperty(EntitySO.ComponentsPropertyName);

        public override void OnInspectorGUI()
        {
            var so = (EntitySO)target;
            serializedObject.Update();

            EntityEditorHelper.DrawComponentsInspector(_componentsProp, so.Components);

            serializedObject.ApplyModifiedProperties();
        }
    }
}