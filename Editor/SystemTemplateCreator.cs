using UnityEditor;

static class SystemTemplateCreator
{
    private const string IntegrationFolderName = "EcsUnityIntegration";
    private const string PathToTemplatesLocalToIntegration = "/Editor/SystemTemplates/";

    private const string Extension = ".cs.txt";

    private static readonly string SystemTemplatePath;

    static SystemTemplateCreator()
    {
        var pathToEcsUnityIntegration = GetPathToEcsUnityIntegration();
        SystemTemplatePath = pathToEcsUnityIntegration + PathToTemplatesLocalToIntegration + "System" + Extension;
    }

    [MenuItem("Assets/Create/ECS/Systems/New system", false, -1)]
    private static void NewInitSystem()
    {
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(SystemTemplatePath, "NewSystem.cs");
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
