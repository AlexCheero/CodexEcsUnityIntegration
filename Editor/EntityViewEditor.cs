using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    public class RuntimeComponentProxy : ScriptableObject
    {
        [SerializeReference]
        public ComponentWrapper Value;
    }
    
    [CustomEditor(typeof(EntityView))]
    public class EntityViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        private SerializedProperty _forceInitProp;
        
        private EntityView _view;
        
        private void OnEnable()
        {
            EditorApplication.update += Repaint;
            
            _componentsProp = serializedObject.FindProperty(EntityView.ComponentsPropertyName);
            _forceInitProp = serializedObject.FindProperty(EntityView.ForceInitPropertyName);
            
            EntityEditorHelper.CleanProxiesCache();
            _view = (EntityView)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_forceInitProp);
            
            if (!_view.IsViewValid())
                EntityEditorHelper.DrawComponentsInspector(_componentsProp, _view.Components);
            else
                EntityEditorHelper.DrawRuntimeInspector(_view);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            EntityEditorHelper.CleanProxiesCache();
        }
    }
}
