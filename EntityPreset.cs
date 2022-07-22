using ECS;
using System;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityPreset", menuName = "ECS/New entity preset", order = -1)]
public class EntityPreset : ScriptableObject
{
    [SerializeField]
    private ComponentMeta[] _metas = new ComponentMeta[0];

#if UNITY_EDITOR
    public int MetasLength { get => _metas.Length; }
    public ref ComponentMeta GetMeta(int i) => ref _metas[i];
    public void RemoveMetaAt(int idx)
    {
        var newLength = _metas.Length - 1;
        for (int i = idx; i < newLength; i++)
            _metas[i] = _metas[i + 1];
        Array.Resize(ref _metas, newLength);
    }
    private bool HaveComponentWithName(string componentName)
    {
        foreach (var meta in _metas)
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

        Array.Resize(ref _metas, _metas.Length + 1);
        _metas[_metas.Length - 1] = new ComponentMeta
        {
            ComponentName = componentName,
            Fields = GetEcsComponentTypeFields(componentName),
            IsExpanded = false
        };

        return true;
    }

    private bool IsRefFieldTypeAllowed(Type fieldType)
    {
        foreach (var type in EntityView.AllowedFiledRefTypes)
        {
            if (fieldType == type)
                return true;
        }
        return false;
    }

    public ComponentFieldMeta[] GetEcsComponentTypeFields(string componentName)
    {
        var compType = IntegrationHelper.GetTypeByName(componentName, EGatheredTypeCategory.EcsComponent);
        if (compType == typeof(EntityPreset))
            return null;

        var fields = compType.GetFields();
        var result = new ComponentFieldMeta[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldType = field.FieldType;
            bool isHidden = fieldType.GetCustomAttribute<HiddenInspector>() != null ||
                (!fieldType.IsValueType && !IsRefFieldTypeAllowed(fieldType));

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

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        var entityId = world.Create();

        MethodInfo addMethodInfo = typeof(EcsWorld).GetMethod("Add");

        foreach (var meta in _metas)
        {
            Type compType;
            object componentObj;
            if (meta.UnityComponent != null)
            {
                compType = meta.UnityComponent.GetType();
                componentObj = meta.UnityComponent;
            }
            else
            {
                compType = IntegrationHelper.GetTypeByName(meta.ComponentName, EGatheredTypeCategory.EcsComponent);
#if DEBUG
                if (compType == null)
                    throw new Exception("can't find component type");
#endif
                componentObj = Activator.CreateInstance(compType);

                foreach (var field in meta.Fields)
                {
                    var value = field.GetValue();
                    if (value == null)
                        continue;

                    var fieldInfo = compType.GetField(field.Name);
                    fieldInfo.SetValue(componentObj, value);
                }
            }
            AddParams[0] = entityId;
            AddParams[1] = componentObj;

            MethodInfo genAddMethodInfo = addMethodInfo.MakeGenericMethod(compType);
            genAddMethodInfo.Invoke(world, AddParams);
        }

        return entityId;
    }
}
