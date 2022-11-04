using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
[Serializable]
public class BaseFieldMeta
{
    private delegate BaseFieldMeta FactoryDelegate(string name);
    private static Dictionary<Type, FactoryDelegate> Factories;

    private static readonly object[] FieldMetaCtorTypes = { null };
    static BaseFieldMeta()
    {
        Factories = new Dictionary<Type, FactoryDelegate>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.BaseType != null && type.BaseType.IsSubclassOf(typeof(BaseFieldMeta)) && !type.IsGenericType)
                {
                    var key = type.BaseType.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance).FieldType;
                    Factories.Add(key, name =>
                    {
                        FieldMetaCtorTypes[0] = name;
                        return (BaseFieldMeta)Activator.CreateInstance(type, FieldMetaCtorTypes);
                    });
                }
            }
        }
    }

    public static BaseFieldMeta CreateMeta(Type type, string name) => Factories[type](name);
    
    [SerializeField]
    private readonly string _name;
    
#if UNITY_EDITOR
    [SerializeField]
    public bool IsHiddenInEditor;
#endif
    
    public string Name => _name;

    public virtual object GetValue() => null;
    public virtual bool SetValue(object value) => false;
    protected virtual bool IsEquals(object a, object b) => false;
    public virtual string GetFieldTypeName() => string.Empty;
    public virtual Type GetFieldType() => null;
    
    protected BaseFieldMeta(string name) => _name = name;
}

[Serializable]
public abstract class FieldMetaObject<T> : BaseFieldMeta, ISerializationCallbackReceiver
{
    [SerializeField]
    private T _value;//TODO: check if field can be serializable

    public override object GetValue() => _value;

    public override bool SetValue(object value)
    {
        T oldValue = _value;
        _value = (T)value;
        return !IsEquals(oldValue, _value);
    }

    protected override bool IsEquals(object a, object b) => a.Equals(b);
    
    public override string GetFieldTypeName() => typeof(T).FullName;
    public override Type GetFieldType() => typeof(T);

    protected FieldMetaObject(string name) : base(name) { }
    public void OnBeforeSerialize()
    {
        throw new NotImplementedException();
    }

    public void OnAfterDeserialize()
    {
        throw new NotImplementedException();
    }
}

[Serializable] public class IntFieldMeta : FieldMetaObject<int> { public IntFieldMeta(string name) : base(name) { } }
[Serializable] public class FloatFieldMeta : FieldMetaObject<float> { public FloatFieldMeta(string name) : base(name) { } }
[Serializable] public class Vec3FieldMeta : FieldMetaObject<Vector3> { public Vec3FieldMeta(string name) : base(name) { } }
[Serializable] public class StrFieldMeta : FieldMetaObject<string> { public StrFieldMeta(string name) : base(name) { } }
[Serializable] public class UnityObjectFieldMeta : FieldMetaObject<Object> { public UnityObjectFieldMeta(string name) : base(name) { } }