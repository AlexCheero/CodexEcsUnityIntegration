using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Tags;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityPreset", menuName = "ECS/New EntityPreset")]
public class EntityPreset : ScriptableObject
{
#if UNITY_EDITOR
    public const string ComponentsPropertyName = nameof(_components);

    // Weak world ownership and one entry per entity slot keep editor provenance bounded.
    // Full entity generations prevent a recycled ID from inheriting a previous preset.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        EcsWorld, Dictionary<int, (Entity Entity, EntityPreset Preset)>> Sources = new();

    public static bool TryGetSourcePreset(EcsWorld world, int id, in Entity entity, out EntityPreset preset)
    {
        preset = null;
        if (world == null || !world.IsIdValid(id) ||
            world.GetById(id).Val != entity.Val || !world.IsEntityValid(entity) ||
            !Sources.TryGetValue(world, out var sources) ||
            !sources.TryGetValue(id, out var source) || source.Entity.Val != entity.Val)
            return false;
        preset = source.Preset;
        return preset != null;
    }
    
    private void OnValidate()
    {
        EntityValidator.ValidateComponents(this, _components);
        _groupMask.Invalidate();
    }

    public void CollectGroupMaskTypes(List<System.Type> types)
    {
        var mask = BuildMask();
        foreach (var id in mask)
            types.Add(ComponentMapping.GetTypeForId(id));
    }
#endif
    
    [SerializeReference]
    private List<ComponentWrapper> _components = new();
    public IReadOnlyList<ComponentWrapper> Components => _components;

    [SerializeField] private EntityGroupMask _groupMask = new();

    public bool TryGetComponentDefaultValue<T>(out T result) where T : IComponent
    {
        result = default;
        var targetType = typeof(T);
        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i].GetComponentType() != targetType)
                continue;
            result = ((ComponentWrapper<T>)_components[i]).Component;
            return true;
        }
        return false;
    }
    
    public int CreatePureEntity(EcsWorld world)
    {
        var mask = BuildMask();
        var groupMask = _groupMask.Resolve(in mask);
        return CreatePureEntity(world, in mask, in groupMask);
    }

    internal BitMask BuildMask()
    {
        var mask = new BitMask().SetTypeId<PureEntity>();
        for (var i = 0; i < _components.Count; i++)
            mask.Set(_components[i].GetComponentId());
        return mask;
    }

    private int CreatePureEntity(EcsWorld world, in BitMask mask, in BitMask groupMask)
    {
        var eid = world.CreateWithComponents(in mask, in groupMask);
        for (var i = 0; i < _components.Count; i++)
            _components[i].AddToWorld(world, eid);
#if UNITY_EDITOR
        Sources.GetOrCreateValue(world)[eid] = (world.GetById(eid), this);
#endif
        return eid;
    }
}
