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
    private List<List<string>> _viewComponentTypeNames;
    private List<bool> _viewComponentFoldouts;
    private bool _addListExpanded;
    private string _addSearch;
    private string _addedSearch;

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
        _viewComponentTypeNames = new List<List<string>>(length);
        _viewComponentFoldouts = new List<bool>(length);
        for (int i = 0, j = 0; i < viewComponents.Length && j < length; i++, j++)
        {
            _viewComponentFoldouts.Add(false);
            var subTypesList = new List<string>();
            var subType = viewComponents[i].GetType();
            while (subType != typeof(Component))
            {
                subTypesList.Add(subType.FullName);
                subType = subType.BaseType;
            }
            _viewComponentTypeNames.Add(subTypesList);
        }

        return base.CreateInspectorGUI();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        IntegrationHelper.DrawAddComponents(ref _addListExpanded, _addSearch, View.Data, target,
            _viewComponentTypeNames, _viewComponentFoldouts);
        _addedSearch = EditorGUILayout.TextField(_addedSearch);
        IntegrationHelper.DrawComponents(View.Data, _addedSearch, target);
    }
}
