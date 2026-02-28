using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(EntityView))]
    public class EntityViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        private SerializedProperty _forceInitProp;
        
        private void OnEnable()
        {
            _componentsProp = serializedObject.FindProperty(EntityView.ComponentsPropertyName);
            _forceInitProp = serializedObject.FindProperty(EntityView.ForceInitPropertyName);
        }

        public override void OnInspectorGUI()
        {
            var view = (EntityView)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(_forceInitProp);
            
            EntityEditorHelper.DrawComponentsInspector(_componentsProp, view.Components);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
