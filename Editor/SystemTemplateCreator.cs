using UnityEditor;

static class SystemTemplateCreator
{
    private const string IntegrationFolderName = "EcsUnityIntegration";
    private const string PathToTemplatesLocalToIntegration = "/Editor/SystemTemplates/";
    private const string InitSystem = "InitSystem";
    private const string UpdateSystem = "UpdateSystem";
    private const string FixedUpdateSystem = "FixedUpdateSystem";
    private const string AddReactiveSystem = "AddReactiveSystem";
    private const string RemoveReactiveSystem = "RemoveReactiveSystem";
    private const string ChangeReactiveSystem = "ChangeReactiveSystem";
    private const string Extension = ".cs.txt";

    private static readonly string InitSystemTemplatePath;
    private static readonly string UpdateSystemTemplatePath;
    private static readonly string FixedUpdateSystemTemplatePath;
    private static readonly string AddReactiveSystemTemplatePath;
    private static readonly string RemoveReactiveSystemTemplatePath;
    private static readonly string ChangeReactiveSystemTemplatePath;

    static SystemTemplateCreator()
    {
        var pathToEcsUnityIntegration = GetPathToEcsUnityIntegration();
        InitSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + InitSystem + Extension;
        UpdateSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + UpdateSystem + Extension;
        FixedUpdateSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + FixedUpdateSystem + Extension;
        AddReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + AddReactiveSystem + Extension;
        RemoveReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + RemoveReactiveSystem + Extension;
        ChangeReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + ChangeReactiveSystem + Extension;
    }

    [MenuItem("Assets/Create/ECS/Systems/New init system", false, -1)]
    private static void NewInitSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(InitSystemTemplatePath, "NewInitSystem.cs");
    }

    [MenuItem("Assets/Create/ECS/Systems/New update system", false, -1)]
    private static void NewUpdateSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(UpdateSystemTemplatePath, "NewUpdateSystem.cs");
    }

    [MenuItem("Assets/Create/ECS/Systems/New fixed update system", false, -1)]
    private static void NewFixedUpdateSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(FixedUpdateSystemTemplatePath, "NewFixedUpdateSystem.cs");
    }

    [MenuItem("Assets/Create/ECS/Systems/New add reactive system", false, -1)]
    private static void NewAddReactiveSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(AddReactiveSystemTemplatePath, "NewAddReactiveSystem.cs");
    }

    [MenuItem("Assets/Create/ECS/Systems/New remove reactive system", false, -1)]
    private static void NewRemoveReactiveSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(RemoveReactiveSystemTemplatePath, "NewRemoveReactiveSystem.cs");
    }

    [MenuItem("Assets/Create/ECS/Systems/New change reactive system", false, -1)]
    private static void NewChangeReactiveSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(ChangeReactiveSystemTemplatePath, "NewChangeReactiveSystem.cs");
    }

    private static string GetPathToEcsUnityIntegration(string startFolder = "Assets")
    {
        var folders = AssetDatabase.GetSubFolders(startFolder);
        foreach (var folder in folders)
        {
            if (folder.Contains(IntegrationFolderName))
                return folder;
            var inner = GetPathToEcsUnityIntegration(folder);
            if (inner.Contains(IntegrationFolderName))
                return inner;
        }

        return string.Empty;
    }
}
