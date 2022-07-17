using Components;
using ECS;
using System;
using System.Reflection;
using Tags;
using UnityEngine;

public class EntityView : MonoBehaviour
{
    public Entity Entity { get; private set; }
    private EcsWorld _world;
    public int Id { get => Entity.GetId(); }
    public int Version { get => Entity.GetVersion(); }

#if DEBUG
    public bool IsValid { get => _world.IsEntityValid(Entity); }
#endif

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

    public bool AddUnityComponent(Component component, Type asType)
    {
        var fullName = asType.FullName;
        if (HaveComponentWithName(fullName))
            return false;

        Array.Resize(ref _metas, _metas.Length + 1);
        _metas[_metas.Length - 1] = new ComponentMeta
        {
            ComponentName = fullName,
            UnityComponent = component
        };
        return true;
    }

    public static bool IsUnityComponent(Type type) => typeof(Component).IsAssignableFrom(type);

    public ComponentFieldMeta[] GetEcsComponentTypeFields(string componentName)
    {
        var compType = IntegrationHelper.GetTypeByName(componentName, EGatheredTypeCategory.EcsComponent);
        if (IsUnityComponent(compType) || compType == typeof(EntityPreset))
            return null;
        
        var fields = compType.GetFields();
        var result = new ComponentFieldMeta[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldType = field.FieldType;
            if (!fieldType.IsValueType && !IsUnityComponent(fieldType) && (fieldType != typeof(EntityPreset)))
            {
                Debug.LogError("wrong component field type. fields should only be pods, derives UnityEngine.Component or be EntityPresets");
                return new ComponentFieldMeta[0];
            }

            result[i] = new ComponentFieldMeta
            {
                TypeName = fieldType.FullName,
                Name = field.Name,
                ValueRepresentation = string.Empty,
                UnityComponent = null,
                Preset = null
            };
        }
        return result;
    }
#endif

    private static readonly object[] AddParams = { null, null };
    public int InitAsEntity(EcsWorld world)
    {
        _world = world;

        var entityId = _world.Create();
        Entity = _world.GetById(entityId);

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
            AddParams[0] = Id;
            AddParams[1] = componentObj;

            MethodInfo genAddMethodInfo = addMethodInfo.MakeGenericMethod(compType);
            genAddMethodInfo.Invoke(_world, AddParams);
        }

        return entityId;
    }

    public bool Have<T>() => _world.Have<T>(Id);
    public ref T AddAndReturnRef<T>(T component = default) => ref _world.AddAndReturnRef(Id, component);
    public void Add<T>(T component = default) => _world.Add<T>(Id, component);
    public T GetEcsComponent<T>() => _world.GetComponent<T>(Id);
    public ref T GetEcsComponentByRef<T>() => ref _world.GetComponentByRef<T>(Id);
    public void CopyFromEntity(Entity from) => _world.CopyComponents(from, Entity);

    public void DeleteSelf()
    {
        _world.Delete(Id);
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        var collidedView = collision.gameObject.GetComponent<EntityView>();
        ProcessCollision(collidedView);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessCollision(other.GetComponent<EntityView>());
    }

    private void ProcessCollision(EntityView collidedView)
    {
        if (collidedView != null)
        {
            AddCollisionComponents(this, collidedView.Entity);
            AddCollisionComponents(collidedView, Entity);
        }
        else
        {
            AddCollisionComponents(this, EntityExtension.NullEntity);
        }
    }

    private static void AddCollisionComponents(EntityView view, Entity otherEntity)
    {
        if (view.Have<CollisionWith>())
        {
            if (view.Have<OverrideCollision>())
                view.GetEcsComponentByRef<CollisionWith>().entity = otherEntity;
        }
        else
        {
            view.Add(new CollisionWith { entity = otherEntity });
        }
    }
}
