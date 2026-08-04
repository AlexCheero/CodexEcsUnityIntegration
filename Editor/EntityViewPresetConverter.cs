using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    public static class EntityViewPresetConverter
    {
        private const string MenuRoot = "Assets/ECS/";
        private const string ComponentsProperty = "_components";

        [MenuItem(MenuRoot + "Create EntityPreset from EntityView", false, 20)]
        private static void CreatePresetFromSelection()
        {
            if (!TryGetSelectedEntityView(out var view))
                return;
            SaveViewAsPreset(view);
        }

        [MenuItem(MenuRoot + "Create EntityPreset from EntityView", true)]
        private static bool ValidateCreatePresetFromSelection() => TryGetSelectedEntityView(out _);

        [MenuItem(MenuRoot + "Create EntityView from EntityPreset", false, 21)]
        private static void CreateViewFromSelection()
        {
            if (!TryGetSelectedPreset(out var preset))
                return;
            CreateEntityViewFromPreset(preset);
        }

        [MenuItem(MenuRoot + "Create EntityView from EntityPreset", true)]
        private static bool ValidateCreateViewFromSelection() => TryGetSelectedPreset(out _);

        [MenuItem(MenuRoot + "Apply EntityPreset to selected EntityView", false, 22)]
        private static void ApplyPresetToSelectedView()
        {
            if (!TryGetSelectedPreset(out var preset) || !TryGetSelectedEntityView(out var view))
            {
                EditorUtility.DisplayDialog(
                    "Apply EntityPreset",
                    "Select an EntityPreset and an EntityView (GameObject or prefab).",
                    "OK");
                return;
            }
            ApplyPresetToView(preset, view);
        }

        [MenuItem(MenuRoot + "Apply EntityPreset to selected EntityView", true)]
        private static bool ValidateApplyPresetToSelectedView() =>
            TryGetSelectedPreset(out _) && TryGetSelectedEntityView(out _);

        private static void SaveViewAsPreset(EntityView view)
        {
            if (view == null)
                return;

            var defaultName = string.IsNullOrEmpty(view.name) ? "EntityPreset" : view.name;
            var defaultFolder = GetAssetFolder(view);
            var path = EditorUtility.SaveFilePanelInProject(
                "Save EntityPreset",
                defaultName,
                "asset",
                "Choose where to save the EntityPreset",
                defaultFolder);
            if (string.IsNullOrEmpty(path))
                return;

            var existing = AssetDatabase.LoadAssetAtPath<EntityPreset>(path);
            EntityPreset preset;
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Overwrite EntityPreset?",
                        $"Replace components on existing preset?\n{path}",
                        "Overwrite",
                        "Cancel"))
                {
                    return;
                }
                preset = existing;
                Undo.RecordObject(preset, "Overwrite EntityPreset from EntityView");
            }
            else
            {
                preset = ScriptableObject.CreateInstance<EntityPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            var copied = CopyComponents(view, preset);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(preset);
            Selection.activeObject = preset;
            Debug.Log($"Created EntityPreset from EntityView '{view.name}' ({copied} components) at {path}", preset);
        }

        private static void ApplyPresetToView(EntityPreset preset, EntityView view)
        {
            if (preset == null || view == null)
                return;

            Undo.RecordObject(view, "Apply EntityPreset to EntityView");
            var copied = CopyComponents(preset, view);
            view.RebuildUnityComponentsCache();
            EditorUtility.SetDirty(view);
            Debug.Log($"Applied EntityPreset '{preset.name}' to EntityView '{view.name}' ({copied} components)", view);
        }

        private static void CreateEntityViewFromPreset(EntityPreset preset)
        {
            if (preset == null)
                return;

            var go = new GameObject(preset.name);
            Undo.RegisterCreatedObjectUndo(go, "Create EntityView from EntityPreset");
            var view = Undo.AddComponent<EntityView>(go);
            var copied = CopyComponents(preset, view);
            view.RebuildUnityComponentsCache();
            EditorUtility.SetDirty(view);
            Selection.activeGameObject = go;
            Debug.Log($"Created EntityView from EntityPreset '{preset.name}' ({copied} components)", view);
        }

        private static int CopyComponents(Object source, Object destination)
        {
            var sourceWrappers = ReadComponentWrappers(source);
            var destSo = new SerializedObject(destination);
            destSo.Update();
            var destProp = destSo.FindProperty(ComponentsProperty);
            if (destProp == null)
            {
                Debug.LogError("Failed to find _components on destination.");
                return 0;
            }

            destProp.ClearArray();
            for (int i = 0; i < sourceWrappers.Count; i++)
            {
                var wrapper = sourceWrappers[i];
                if (wrapper == null)
                    continue;

                var clone = CloneWrapper(wrapper);
                if (clone == null)
                    continue;

                var index = destProp.arraySize;
                destProp.InsertArrayElementAtIndex(index);
                destProp.GetArrayElementAtIndex(index).managedReferenceValue = clone;
            }

            destSo.ApplyModifiedPropertiesWithoutUndo();
            return destProp.arraySize;
        }

        private static List<ComponentWrapper> ReadComponentWrappers(Object source)
        {
            var result = new List<ComponentWrapper>();
            var sourceSo = new SerializedObject(source);
            sourceSo.Update();
            var sourceProp = sourceSo.FindProperty(ComponentsProperty);
            if (sourceProp == null || !sourceProp.isArray)
            {
                Debug.LogError($"Failed to read _components from {source}.", source);
                return result;
            }

            for (int i = 0; i < sourceProp.arraySize; i++)
            {
                var element = sourceProp.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue is ComponentWrapper wrapper)
                    result.Add(wrapper);
            }

            if (result.Count == 0)
            {
                // Fallback for cases where managed refs aren't exposed via SerializedProperty yet.
                IReadOnlyList<ComponentWrapper> direct = source switch
                {
                    EntityView view => view.Components,
                    EntityPreset preset => preset.Components,
                    _ => null
                };
                if (direct != null)
                {
                    for (int i = 0; i < direct.Count; i++)
                    {
                        if (direct[i] != null)
                            result.Add(direct[i]);
                    }
                }
            }

            return result;
        }

        private static ComponentWrapper CloneWrapper(ComponentWrapper source)
        {
            var componentType = source.GetComponentType();
            var wrapperType = typeof(ComponentWrapper<>).MakeGenericType(componentType);
            var clone = (ComponentWrapper)Activator.CreateInstance(wrapperType);
            var field = wrapperType.GetField(
                ComponentWrapper.ComponentPropertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return clone;

            var value = field.GetValue(source);
            if (value is IComponent component)
                clone.InitFromComponent(component);
            return clone;
        }

        private static string GetAssetFolder(Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath) && obj is Component component)
                assetPath = AssetDatabase.GetAssetPath(component.gameObject);
            if (string.IsNullOrEmpty(assetPath))
                return "Assets";

            var folder = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
        }

        private static bool TryGetSelectedEntityView(out EntityView view)
        {
            view = Selection.activeObject as EntityView;
            if (view != null)
                return true;

            var go = Selection.activeGameObject;
            if (go != null)
            {
                view = go.GetComponent<EntityView>();
                if (view != null)
                    return true;
            }

            var selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                view = selected[i] as EntityView;
                if (view != null)
                    return true;

                if (selected[i] is GameObject selectedGo)
                {
                    view = selectedGo.GetComponent<EntityView>();
                    if (view != null)
                        return true;
                }
            }

            view = null;
            return false;
        }

        private static bool TryGetSelectedPreset(out EntityPreset preset)
        {
            preset = Selection.activeObject as EntityPreset;
            if (preset != null)
                return true;

            var selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                preset = selected[i] as EntityPreset;
                if (preset != null)
                    return true;
            }

            preset = null;
            return false;
        }
    }
}
