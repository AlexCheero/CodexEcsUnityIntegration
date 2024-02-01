using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{

#if UNITY_EDITOR
    public class ComponentViewConverter : MonoBehaviour
    {
        private static readonly string ComponentViewTemplate =
            "using CodexECS;\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Components;\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Tags;\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Views;\n" +
            "using UnityEngine;\n" +
            "[DisallowMultipleComponent]\n" +
            "public class <ComponentName>View : ComponentView<<ComponentName>>{}";

        private static readonly string ViewRegistratorTemplate =
            "using System;\n" +
            "using System.Collections.Generic;\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Components;\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Tags;\n" +
            "using CodexECS;\n" +
            "public static class ViewRegistrator\n" +
            "{\n" +
            "\tprivate static Dictionary<Type, Type> ViewsByCompTypes = new();\n" +
            "\tpublic static Type GetViewTypeByCompType(Type compType) => ViewsByCompTypes[compType];\n\n" +
            "\tpublic static void Register()\n" +
            "\t{\n" +
            "\t\tint id;\n" +
            "<RegisterHere>" +
            "\t}\n" +
            "}";

        private static readonly string ViewsPath = "Assets/Scripts/Monobehaviours/ComponentViews/";

        [MenuItem("ECS/Generate component views", false, -1)]
        private static void GenerateComponentViews()
        {
            var dir = new DirectoryInfo(ViewsPath);
            foreach (FileInfo file in dir.GetFiles())
                file.Delete();

            foreach (var type in IntegrationHelper.EcsComponentTypes)
            {
                var viewCode = ComponentViewTemplate.Replace("<ComponentName>", type.Name);
                using (StreamWriter writer = new StreamWriter(ViewsPath + type.Name + "View.cs"))
                {
                    writer.WriteLine(viewCode);
                }
            }

            var registrationBody = "";
            foreach (var type in IntegrationHelper.EcsComponentTypes)
                registrationBody += "\t\tViewsByCompTypes[typeof(" + type.Name + ")] = typeof(" + type.Name + "View);\n" +
                    "\t\tid = ComponentMeta<" + type.Name + ">.Id;\n";
            var registratorCode = ViewRegistratorTemplate.Replace("<RegisterHere>", registrationBody);
            using (StreamWriter writer = new StreamWriter(ViewsPath + "ViewRegistrator.cs"))
            {
                writer.WriteLine(registratorCode);
            }
        }
    }
#endif
}