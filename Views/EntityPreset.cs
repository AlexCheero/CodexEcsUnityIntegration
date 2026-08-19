using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityPreset", menuName = "ECS/New EntityPreset")]
public class EntityPreset : ScriptableObject
{
#if UNITY_EDITOR
    public const string ComponentsPropertyName = nameof(_components);
    
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
        var eid = world.Create();
        for (var i = 0; i < _components.Count; i++)
            _components[i].AddToWorld(world, eid);
        return eid;
    }
}
