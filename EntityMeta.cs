using System;
using System.Reflection;
using UnityEngine;

[Serializable]
public struct EntityMeta
{
    [SerializeField]
    public ComponentMeta[] Metas;

#if UNITY_EDITOR
    private static readonly Type[] AllowedFiledRefTypes = { typeof(EntityPreset), typeof(string) };

    public void RemoveMetaAt(int idx)
    {
        var newLength = Metas.Length - 1;
        for (int i = idx; i < newLength; i++)
            Metas[i] = Metas[i + 1];
        Array.Resize(ref Metas, newLength);
    }

    private bool HaveComponentWithName(string componentName)
    {
        foreach (var meta in Metas)
        {
            if (meta.ComponentName == componentName)
                return true;
        }

        return false;
    }

    public bool AddComponent(string componentName)
    {
        if (HaveComponentWithName(componentName))
            return false;

        Array.Resize(ref Metas, Metas.Length + 1);
        Metas[Metas.Length - 1] = new ComponentMeta
        {
            ComponentName = componentName,
            Fields = GetEcsComponentTypeFields(componentName),
            IsExpanded = false
        };

        return true;
    }

    public bool AddUnityComponent(Component component, Type asType)
    {
        var fullName = asType.FullName;
        if (HaveComponentWithName(fullName))
            return false;

        Array.Resize(ref Metas, Metas.Length + 1);
        Metas[Metas.Length - 1] = new ComponentMeta
        {
            ComponentName = fullName,
            UnityComponent = component
        };
        return true;
    }

    private bool IsRefFieldTypeAllowed(Type fieldType)
    {
        foreach (var type in AllowedFiledRefTypes)
        {
            if (fieldType == type)
                return true;
        }
        return false;
    }

    private ComponentFieldMeta[] GetEcsComponentTypeFields(string componentName)
    {
        var compType = IntegrationHelper.GetTypeByName(componentName, EGatheredTypeCategory.EcsComponent);
        if (IntegrationHelper.IsUnityComponent(compType) || compType == typeof(EntityPreset))
            return null;

        var fields = compType.GetFields();
        var result = new ComponentFieldMeta[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldType = field.FieldType;

            bool isHidden = field.GetCustomAttribute<HiddenInspector>() != null ||
                (!fieldType.IsValueType && !IntegrationHelper.IsUnityComponent(fieldType) && !IsRefFieldTypeAllowed(fieldType));

            result[i] = new ComponentFieldMeta
            {
                TypeName = fieldType.FullName,
                Name = field.Name,
                ValueRepresentation = string.Empty,
                UnityComponent = null,
                Preset = null,
                IsHiddenInEditor = isHidden
            };
        }
        return result;
    }
#endif
}
