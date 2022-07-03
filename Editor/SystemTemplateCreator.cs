using UnityEditor;

static class SystemTemplateCreator
{
    private const string IntegrationFolderName = "EcsUnityIntegration";
    private const string PathToTemplatesLocalToIntegration = "/Editor/SystemTemplates/";
    private const string InitSystem = "InitSystem";
    private const string UpdateSystem = "UpdateSystem";
    private const string FixedUpdateSystem = "FixedUpdateSystem";
    private const string Extension = ".cs.txt";

    private static readonly string InitSystemTemplatePath;
    private static readonly string UpdateSystemTemplatePath;
    private static readonly string FixedUpdateSystemTemplatePath;

    static SystemTemplateCreator()
    {
        var pathToEcsUnityIntegration = GetPathToEcsUnityIntegration();
        InitSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + InitSystem + Extension;
        UpdateSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + UpdateSystem + Extension;
        FixedUpdateSystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + FixedUpdateSystem + Extension;
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
