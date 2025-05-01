using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    static class EcsTemplateCreator
    {
        private const string IntegrationFolderName = "EcsUnityIntegration";
        private const string PathToTemplatesLocalToIntegration = "/Editor/EcsTemplates/";

        private const string Extension = ".cs.txt";

        private static readonly string SystemTemplatePath;
        private static readonly string ComponentTemplatePath;

        static EcsTemplateCreator()
        {
            var pathToTemplates = GetPathToEcsUnityIntegration() + PathToTemplatesLocalToIntegration;
            SystemTemplatePath = pathToTemplates + "System" + Extension;
            ComponentTemplatePath = pathToTemplates + "Component" + Extension;
        }

        [MenuItem("Assets/Create/ECS/New system", false, -1)]
        private static void NewSystem() => ProjectWindowUtil.CreateScriptAssetFromTemplateFile(SystemTemplatePath, "NewSystem.cs");

        [MenuItem("Assets/Create/ECS/New component", false, -1)]
        private static void NewComponent() => ProjectWindowUtil.CreateScriptAssetFromTemplateFile(ComponentTemplatePath, "NewComponent.cs");

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
}