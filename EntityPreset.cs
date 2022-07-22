using ECS;
using System;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityPreset", menuName = "ECS/New entity preset", order = -1)]
public class EntityPreset : ScriptableObject
{
    [SerializeField]
    public EntityMeta Data = new EntityMeta { Metas = new ComponentMeta[0] };

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        var entityId = world.Create();

        MethodInfo addMethodInfo = typeof(EcsWorld).GetMethod("Add");

        foreach (var meta in Data.Metas)
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
                    var fieldInfo = compType.GetField(field.Name);
                    var defaultValueAttribute = fieldInfo.GetCustomAttribute<DefaultValue>();
                    object defaultValue = defaultValueAttribute?.Value;
                    var value = field.IsHiddenInEditor ? defaultValue : field.GetValue();
                    if (value == null)
                        continue;

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
