using Components;
using ECS;
using System;
using System.Globalization;
using System.Reflection;
using Tags;
using UnityEngine;

[Serializable]
public struct ComponentFieldMeta
{
    //TODO: define different access modifiers for UNITY_EDITOR (and hide some getters)
    public string TypeName;
    public string Name;
    public string ValueRepresentation;
    public Component UnityComponent;

    public object GetValue()
    {
        bool isRepresentationNotEmpty = ValueRepresentation != null && ValueRepresentation.Length > 0;
        //TODO: move all these typeof to single place, possibly to implement code generation in future
        if (TypeName == typeof(int).FullName)
            return isRepresentationNotEmpty ? int.Parse(ValueRepresentation) : 0;
        else if (TypeName == typeof(float).FullName)
            return isRepresentationNotEmpty ? float.Parse(ValueRepresentation, CultureInfo.InvariantCulture) : 0;
        else if (TypeName == typeof(Vector3).FullName)
            return isRepresentationNotEmpty ? ParseVector3(ValueRepresentation) : Vector3.zero;
        else
        {
            var type = IntegrationHelper.GetTypeByName(TypeName, EGatheredTypeCategory.UnityComponent);
            if (typeof(Component).IsAssignableFrom(type))
            {
                return UnityComponent;
            }
            else
            {
                Debug.LogError("Wrong field meta Type");
                return null;
            }
        }
    }

#if UNITY_EDITOR
    public bool SetValue(object value)
    {
        var previousRepresentation = ValueRepresentation;
        var previousComponent = UnityComponent;
        
        if (TypeName == typeof(int).FullName)
        {
            ValueRepresentation = value.ToString();
        }
        else if (TypeName == typeof(float).FullName)
        {
            ValueRepresentation = ((float)value).ToString(CultureInfo.InvariantCulture);
        }
        else if (TypeName == typeof(Vector3).FullName)
        {
            var vec = (Vector3)value;
            ValueRepresentation = vec.x + " " + vec.y + " " + vec.z;
        }
        else
        {
            var type = IntegrationHelper.GetTypeByName(TypeName, EGatheredTypeCategory.UnityComponent);
            if (typeof(Component).IsAssignableFrom(type))
            {
                UnityComponent = (Component)value;
            }
            else
            {
                Debug.LogError("Wrong field meta Type");
            }
        }

        return previousRepresentation != ValueRepresentation || previousComponent != UnityComponent;
    }
#endif

    private Vector3 ParseVector3(string representation)
    {
        var representations = representation.Split(' ');
        if (representations.Length != 3)
        {
            Debug.LogError("wrong number of parameters to init vector3 from string");
            return Vector3.zero;
        }
        var x = float.Parse(representations[0]);
        var y = float.Parse(representations[1]);
        var z = float.Parse(representations[2]);
        return new Vector3(x, y, z);
    }
}

[Serializable]
public struct ComponentMeta
{
    public string ComponentName;
    public ComponentFieldMeta[] Fields;
    public Component UnityComponent;
#if UNITY_EDITOR
    public bool IsExpanded;
#endif
}

public class EntityView : MonoBehaviour
{
    public Entity Entity { get; private set; }
    private EcsWorld _world;
    public int Id { get => Entity.GetId(); }
    public int Version { get => Entity.GetVersion(); }

#if DEBUG
    public bool IsValid { get => _world.IsEntityValid(Entity); }
#endif

    public static bool IsUnityComponent(Type type) => typeof(Component).IsAssignableFrom(type);

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

    public bool AddUnityComponent(Component component)
    {
        var fullName = component.GetType().FullName;
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

    public ComponentFieldMeta[] GetEcsComponentTypeFields(string componentName)
    {
        var compType = IntegrationHelper.GetTypeByName(componentName, EGatheredTypeCategory.EcsComponent);
        if (IsUnityComponent(compType))
            return null;
        
        var fields = compType.GetFields();
        var result = new ComponentFieldMeta[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldType = field.FieldType;
            if (!fieldType.IsValueType && !IsUnityComponent(fieldType))
            {
                Debug.LogError("wrong component field type. fields should only be pods or derives UnityEngine.Component");
                return new ComponentFieldMeta[0];
            }

            result[i] = new ComponentFieldMeta
            {
                TypeName = fieldType.FullName,
                Name = field.Name,
                ValueRepresentation = string.Empty,
                UnityComponent = null
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
