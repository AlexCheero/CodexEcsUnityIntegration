using UnityEditor;

static class SystemTemplateCreator
{
    private const string IntegrationFolderName = "EcsUnityIntegration";
    private const string PathToTemplatesLocalToIntegration = "/Editor/SystemTemplates/";

    private const string AddReactiveSystem = "AddReactiveSystem";
    private const string RemoveReactiveSystem = "RemoveReactiveSystem";
    private const string ChangeReactiveSystem = "ChangeReactiveSystem";

    private const string Extension = ".cs.txt";

    private static readonly string SystemTemplatePath;

    private static readonly string AddReactiveSystemTemplatePath;
    private static readonly string RemoveReactiveSystemTemplatePath;
    private static readonly string ChangeReactiveSystemTemplatePath;

    static SystemTemplateCreator()
    {
        var pathToEcsUnityIntegration = GetPathToEcsUnityIntegration();
        SystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + "System" + Extension;
        AddReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + AddReactiveSystem + Extension;
        RemoveReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + RemoveReactiveSystem + Extension;
        ChangeReactiveSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + ChangeReactiveSystem + Extension;
    }

    [MenuItem("Assets/Create/ECS/Systems/New system", false, -1)]
    private static void NewInitSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(SystemTemplatePath, "NewSystem.cs");
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
