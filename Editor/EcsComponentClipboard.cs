using System;
using System.Collections.Generic;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal sealed class EcsComponentClipboardProxy : ScriptableObject
    {
        [SerializeReference]
        internal ComponentWrapper Value;
    }

    internal static class EcsComponentClipboard
    {
        private static Type _componentType;
        private static string _serializedWrapper;

        internal static bool HasComponent =>
            _componentType != null && !string.IsNullOrEmpty(_serializedWrapper);

        internal static Type ComponentType => _componentType;

        internal static void Clear()
        {
            _componentType = null;
            _serializedWrapper = null;
        }

        internal static bool TryCopy(ComponentWrapper source, out string error)
        {
            error = null;
            if (source == null)
            {
                error = "the source component wrapper is null.";
                return false;
            }

            var sourceType = source.GetComponentType();
            var proxy = CreateProxy();
            try
            {
                proxy.Value = source;
                var json = EditorJsonUtility.ToJson(proxy);
                if (string.IsNullOrEmpty(json))
                {
                    error = $"Unity could not serialize ECS component '{sourceType.Name}'.";
                    return false;
                }

                _componentType = sourceType;
                _serializedWrapper = json;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unity could not serialize ECS component '{sourceType.Name}': {exception.Message}";
                return false;
            }
            finally
            {
                proxy.Value = null;
                Object.DestroyImmediate(proxy);
            }
        }

        internal static bool TryCreateWrapperClone(out ComponentWrapper clone, out string error)
        {
            clone = null;
            error = null;
            if (!HasComponent)
            {
                error = "the ECS component clipboard is empty.";
                return false;
            }

            var proxy = CreateProxy();
            try
            {
                proxy.Value = CreateEmptyWrapper(_componentType);
                EditorJsonUtility.FromJsonOverwrite(_serializedWrapper, proxy);
                if (proxy.Value == null)
                {
                    error = $"Unity could not deserialize ECS component '{_componentType.Name}'.";
                    return false;
                }
                if (proxy.Value.GetComponentType() != _componentType)
                {
                    error =
                        $"The ECS clipboard contains '{proxy.Value.GetComponentType().Name}', " +
                        $"but '{_componentType.Name}' was expected.";
                    return false;
                }

                clone = proxy.Value;
                proxy.Value = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unity could not deserialize ECS component '{_componentType.Name}': {exception.Message}";
                return false;
            }
            finally
            {
                proxy.Value = null;
                Object.DestroyImmediate(proxy);
            }
        }

        internal static bool CopyComponent(Object owner, Type componentType)
        {
            if (!TryGetOwnerComponents(
                    owner,
                    out _,
                    out var componentsProperty,
                    out _,
                    out var ownerError))
            {
                Debug.LogError($"Cannot copy ECS component: {ownerError}", owner);
                return false;
            }
            if (!TryFindComponent(componentsProperty, componentType, out _, out var wrapper))
            {
                Debug.LogError(
                    $"Cannot copy ECS component '{componentType?.Name}': it was not found on '{owner.name}'.",
                    owner);
                return false;
            }
            if (TryCopy(wrapper, out var copyError))
                return true;

            Debug.LogError($"Cannot copy ECS component: {copyError}", owner);
            return false;
        }

        internal static bool PasteComponentValues(Object owner, Type destinationType)
        {
            if (!HasComponent)
            {
                Debug.LogError("Cannot paste ECS component values: the clipboard is empty.", owner);
                return false;
            }
            if (_componentType != destinationType)
            {
                Debug.LogError(
                    $"Cannot paste '{_componentType.Name}' values into '{destinationType?.Name}'.",
                    owner);
                return false;
            }
            if (!TryGetOwnerComponents(
                    owner,
                    out var serializedOwner,
                    out var componentsProperty,
                    out _,
                    out var ownerError))
            {
                Debug.LogError($"Cannot paste ECS component values: {ownerError}", owner);
                return false;
            }
            if (!TryFindComponent(
                    componentsProperty,
                    destinationType,
                    out var destinationProperty,
                    out var destinationWrapper))
            {
                Debug.LogError(
                    $"Cannot paste ECS component values: '{destinationType.Name}' was not found on '{owner.name}'.",
                    owner);
                return false;
            }
            if (!TryCreateWrapperClone(out var sourceClone, out var cloneError))
            {
                Debug.LogError($"Cannot paste ECS component values: {cloneError}", owner);
                return false;
            }

            // Mutate the existing wrapper rather than replacing its SerializeReference.
            // EntityView caches wrappers by type, so preserving this identity is required.
            Undo.RecordObject(owner, $"Paste {destinationType.Name} ECS Component Values");
            destinationWrapper.InitFromComponent(sourceClone.GetBoxedDefaultValue());
            destinationProperty.managedReferenceValue = destinationWrapper;
            serializedOwner.ApplyModifiedPropertiesWithoutUndo();
            MarkOwnerChanged(owner);
            return true;
        }

        internal static bool PasteComponentAsNew(Object owner)
        {
            if (!TryGetOwnerComponents(
                    owner,
                    out var serializedOwner,
                    out var componentsProperty,
                    out var existingComponents,
                    out var ownerError))
            {
                Debug.LogError($"Cannot paste ECS component as new: {ownerError}", owner);
                return false;
            }

            if (!TryPasteComponentAsNew(
                    componentsProperty,
                    existingComponents,
                    owner))
            {
                return false;
            }

            serializedOwner.ApplyModifiedProperties();
            MarkOwnerChanged(owner);
            return true;
        }

        internal static bool TryPasteComponentAsNew(
            SerializedProperty componentsProperty,
            IReadOnlyList<ComponentWrapper> existingComponents,
            Object owner)
        {
            if (!HasComponent)
            {
                Debug.LogError("Cannot paste ECS component as new: the clipboard is empty.", owner);
                return false;
            }
            if (ContainsComponentType(componentsProperty, existingComponents, _componentType))
            {
                Debug.LogError(
                    $"Cannot paste ECS component '{_componentType.Name}' as new: " +
                    $"'{owner.name}' already contains that component.",
                    owner);
                return false;
            }
            if (!TryCreateWrapperClone(out var sourceClone, out var cloneError))
            {
                Debug.LogError($"Cannot paste ECS component as new: {cloneError}", owner);
                return false;
            }

            return EcsComponentInspectorUtility.TryAddSerializedComponents(
                componentsProperty,
                existingComponents,
                owner,
                _componentType,
                sourceClone);
        }

        internal static bool ContainsComponentType(
            SerializedProperty componentsProperty,
            IReadOnlyList<ComponentWrapper> fallbackComponents,
            Type componentType)
        {
            if (componentsProperty != null && componentsProperty.isArray)
            {
                for (var i = 0; i < componentsProperty.arraySize; i++)
                {
                    if (componentsProperty.GetArrayElementAtIndex(i).managedReferenceValue is ComponentWrapper wrapper &&
                        wrapper.GetComponentType() == componentType)
                    {
                        return true;
                    }
                }
            }

            if (fallbackComponents == null)
                return false;
            for (var i = 0; i < fallbackComponents.Count; i++)
            {
                var wrapper = fallbackComponents[i];
                if (wrapper != null && wrapper.GetComponentType() == componentType)
                    return true;
            }
            return false;
        }

        private static bool TryGetOwnerComponents(
            Object owner,
            out SerializedObject serializedOwner,
            out SerializedProperty componentsProperty,
            out IReadOnlyList<ComponentWrapper> existingComponents,
            out string error)
        {
            serializedOwner = null;
            componentsProperty = null;
            existingComponents = null;
            error = null;
            if (owner == null)
            {
                error = "the destination owner is null.";
                return false;
            }

            existingComponents = owner switch
            {
                EntityView view => view.Components,
                EntityPreset preset => preset.Components,
                _ => null
            };
            if (owner is not EntityView && owner is not EntityPreset)
            {
                error = $"'{owner.GetType().Name}' is not an EntityView or EntityPreset.";
                return false;
            }

            serializedOwner = new SerializedObject(owner);
            serializedOwner.Update();
            componentsProperty = serializedOwner.FindProperty(EntityView.ComponentsPropertyName);
            if (componentsProperty != null && componentsProperty.isArray)
                return true;

            error = $"the serialized '{EntityView.ComponentsPropertyName}' list was not found.";
            return false;
        }

        private static bool TryFindComponent(
            SerializedProperty componentsProperty,
            Type componentType,
            out SerializedProperty element,
            out ComponentWrapper wrapper)
        {
            element = null;
            wrapper = null;
            if (componentsProperty == null || componentType == null)
                return false;

            for (var i = 0; i < componentsProperty.arraySize; i++)
            {
                var current = componentsProperty.GetArrayElementAtIndex(i);
                if (current.managedReferenceValue is not ComponentWrapper currentWrapper ||
                    currentWrapper.GetComponentType() != componentType)
                {
                    continue;
                }

                element = current;
                wrapper = currentWrapper;
                return true;
            }
            return false;
        }

        private static ComponentWrapper CreateEmptyWrapper(Type componentType)
        {
            var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
            return (ComponentWrapper)Activator.CreateInstance(wrapperType);
        }

        private static EcsComponentClipboardProxy CreateProxy()
        {
            var proxy = ScriptableObject.CreateInstance<EcsComponentClipboardProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            return proxy;
        }

        private static void MarkOwnerChanged(Object owner)
        {
            EditorUtility.SetDirty(owner);
            if (owner is Component component)
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}
