using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    [CustomEditor(typeof(EntityView))]
    public class EntityViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _componentsProp;
        private SerializedProperty _forceInitProp;
        
        private EntityView _view;
        private RuntimeEntityInspector _runtimeInspector;
        
        private void OnEnable()
        {
            _componentsProp = serializedObject.FindProperty(EntityView.ComponentsPropertyName);
            _forceInitProp = serializedObject.FindProperty(EntityView.ForceInitPropertyName);
            
            _runtimeInspector = new RuntimeEntityInspector();
            _view = (EntityView)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_forceInitProp);
            
            if (!_view.IsViewValid())
            {
                _runtimeInspector.Dispose();
                EntityEditorHelper.DrawComponentsInspector(_componentsProp, _view.Components, _view);
            }
            else
                _runtimeInspector.Draw(_view.World, _view.Id, _view);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnDisable()
        {
            _runtimeInspector.Dispose();
        }

        public override bool RequiresConstantRepaint() => _view != null && _view.IsViewValid();
    }
}
