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
    
    private void OnValidate() => EntityValidator.ValidateComponents(_components);
#endif
    
    [SerializeReference]
    private List<ComponentWrapper> _components;
    public IReadOnlyList<ComponentWrapper> Components => _components;

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
        var mask = new BitMask().SetTypeId<PureEntity>();
        for (var i = 0; i < _components.Count; i++)
            mask.Set(_components[i].GetComponentId());

        var eid = world.CreateWithComponents(mask);
        for (var i = 0; i < _components.Count; i++)
            _components[i].AddToWorld(world, eid);
#if UNITY_EDITOR
        Sources.GetOrCreateValue(world)[eid] = (world.GetById(eid), this);
#endif
        return eid;
    }
}
