using System.Collections.Generic;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

[CreateAssetMenu(fileName = "EntitySO", menuName = "ECS/New EntitySO")]
public class EntitySO : ScriptableObject
{
#if UNITY_EDITOR
    public const string ComponentsPropertyName = nameof(_components);
#endif
    
    [SerializeReference]
    private List<ComponentWrapper> _components;
    public IReadOnlyList<ComponentWrapper> Components => _components;
    
    public int CreatePureEntity(EcsWorld world)
    {
        var eid = world.Create();
        for (var i = 0; i < _components.Count; i++)
            _components[i].AddToWorld(world, eid);
        return eid;
    }
}
