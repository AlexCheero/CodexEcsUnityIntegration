using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public struct ComponentFieldMeta
{
    //TODO: define different access modifiers for UNITY_EDITOR (and hide some getters)
    public string TypeName;
    public string Name;
    public string ValueRepresentation;
    public Component UnityComponent;
    public EntityPreset Preset;

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
            else if (nameof(EntityPreset) == TypeName)
            {
                return Preset;
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
        var previousPreset = Preset;

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
            else if (nameof(EntityPreset) == TypeName)
            {
                Preset = (EntityPreset)value;
            }
            else
            {
                Debug.LogError("Wrong field meta Type");
            }
        }

        return previousRepresentation != ValueRepresentation || previousComponent != UnityComponent || previousPreset != Preset;
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
