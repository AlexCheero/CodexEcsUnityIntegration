using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

//TODO: hide some components fields that shouldn't be visible in inspector such as AttackComponent.previousAttackTime
//TODO: implement runtime fileds update
//TODO: implement search bar for components
[CustomEditor(typeof(EntityView))]
public class EntityView_Inspector : Editor
{
    private EntityViewComponentsData _componentsData;
    private EntityInspectorCommonData _commonData;

    private EntityView _view;
    private EntityView View
    {
        get
        {
            if (_view == null)
                _view =(EntityView)target;
            return _view;
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        //TODO: it doesn't get all the components. to get most of them EntityView script should
        //      be the last script of GO, and still it can't get the EntityView script itself
        var viewComponents = View.GetComponents<Component>();
        var length = viewComponents.Length - 1;
        _componentsData = new EntityViewComponentsData(length);
        for (int i = 0, j = 0; i < viewComponents.Length && j < length; i++, j++)
        {
            _componentsData.ViewComponentFoldouts.Add(false);
            var subTypesList = new List<string>();
            var subType = viewComponents[i].GetType();
            while (subType != typeof(Component))
            {
                subTypesList.Add(subType.FullName);
                subType = subType.BaseType;
            }
            _componentsData.ViewComponentTypeNames.Add(subTypesList);
        }

        return base.CreateInspectorGUI();
    }

    public override void OnInspectorGUI() =>
        IntegrationHelper.OnEntityInspectorGUI(serializedObject, target, ref _commonData, ref View.Data, _componentsData);
}
