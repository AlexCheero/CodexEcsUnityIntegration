using CodexFramework.CodexEcsUnityIntegration.Views;
using CodexFramework.Utils.Pools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    internal abstract class EntityVariantPropertyDrawer<TView> : PropertyDrawer
        where TView : Component
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var usePresetProperty = property.FindPropertyRelative("_usePreset");
            var viewProperty = property.FindPropertyRelative("_view");
            var presetProperty = property.FindPropertyRelative("_preset");
            var currentValue = usePresetProperty.boolValue
                ? presetProperty.objectReferenceValue
                : viewProperty.objectReferenceValue;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var assignedValue = EditorGUI.ObjectField(position, label, currentValue, typeof(Object), true);
            if (EditorGUI.EndChangeCheck())
                Assign(assignedValue, usePresetProperty, viewProperty, presetProperty);
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private static void Assign(
            Object assignedValue,
            SerializedProperty usePresetProperty,
            SerializedProperty viewProperty,
            SerializedProperty presetProperty)
        {
            if (assignedValue == null)
            {
                viewProperty.objectReferenceValue = null;
                presetProperty.objectReferenceValue = null;
                return;
            }

            if (assignedValue is EntityPreset preset)
            {
                usePresetProperty.boolValue = true;
                presetProperty.objectReferenceValue = preset;
                viewProperty.objectReferenceValue = null;
                return;
            }

            var view = assignedValue as TView;
            if (view == null && assignedValue is GameObject gameObject)
                view = gameObject.GetComponent<TView>();
            if (view != null)
            {
                usePresetProperty.boolValue = false;
                viewProperty.objectReferenceValue = view;
                presetProperty.objectReferenceValue = null;
                return;
            }

            Debug.LogError(
                $"{assignedValue.name} is not an {typeof(TView).Name} or {nameof(EntityPreset)}.",
                assignedValue);
        }
    }

    [CustomPropertyDrawer(typeof(EntityVariant))]
    internal sealed class EntityVariantPropertyDrawer : EntityVariantPropertyDrawer<EntityView>
    {
    }

    [CustomPropertyDrawer(typeof(PooledEntityVariant))]
    internal sealed class PooledEntityVariantPropertyDrawer : EntityVariantPropertyDrawer<PooledEntityView>
    {
    }
}
