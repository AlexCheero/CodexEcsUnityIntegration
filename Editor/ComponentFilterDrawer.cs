using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    [CustomPropertyDrawer(typeof(ComponentFilterAttribute))]
    public class ComponentFilterDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw the object field as usual
            //CODEX_TODO: uses typeof(EntityView) but should also work with typeof(PooledEntityView)
            var newValue =
                EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(EntityView), true);

            // Validation
            if (newValue != null)
            {
                var go = ((Component)newValue).gameObject;
                var attr = (ComponentFilterAttribute)attribute;
                foreach (var required in attr.RequiredComponents)
                {
                    if (go.GetComponent(required) != null)
                        continue;
                    Debug.LogError($"Missing component: {required.Name}");
                    
                    //CODEX_TODO: draws only for one frame, try to fix
                    // EditorGUI.HelpBox(position, $"Missing component: {required.Name}", MessageType.Error);
                    
                    EditorUtility.DisplayDialog(
                        $"Missing component: {required.Name}",
                        $"Game object should have: {required.Name}",
                        "ok"
                    );
                    
                    newValue = null; // cancel the assignment
                    goto ValidationFinished;
                }

                if (attr.ExcludedComponents != null)
                {
                    foreach (var excluded in attr.ExcludedComponents)
                    {
                        if (go.GetComponent(excluded) == null)
                            continue;
                        Debug.LogError($"Exclude component: {excluded.Name}");

                        //CODEX_TODO: draws only for one frame, try to fix
                        // EditorGUI.HelpBox(position, $"Exclude component: {excluded.Name}", MessageType.Error);

                        EditorUtility.DisplayDialog(
                            $"Exclude component: {excluded.Name}",
                            $"Game object should have no: {excluded.Name}",
                            "ok"
                        );

                        newValue = null; // cancel the assignment
                        goto ValidationFinished;
                    }
                }
            }

            ValidationFinished:
            property.objectReferenceValue = newValue;

            EditorGUI.EndProperty();
        }
    }
}