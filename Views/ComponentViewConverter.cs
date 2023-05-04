using System.IO;
using UnityEditor;
using UnityEngine;

public class ComponentViewConverter : MonoBehaviour
{
    private static readonly string ComponentViewTemplate =
        "using Components;\n" +
        "using Tags;\n" +
        "public class <ComponentName>View : ComponentView<<ComponentName>>{}";

    private static readonly string ViewRegistratorTemplate =
        "using System;\n" +
        "using System.Collections.Generic;\n" +
        "using Components;\n" +
        "using Tags;\n" +
        "public static class ViewRegistrator\n" +
        "{\n" +
        "\tprivate static Dictionary<Type, Type> ViewsByCompTypes = new();\n" +
        "\tpublic static Type GetViewTypeByCompType(Type compType) => ViewsByCompTypes[compType];\n\n" +
        "\tpublic static void Register()\n" +
        "\t{\n" +
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

        foreach (var type in IntegrationHelper.EcsTypes)
        {
            var viewCode = ComponentViewTemplate.Replace("<ComponentName>", type.Name);
            using (StreamWriter writer = new StreamWriter(ViewsPath + type.Name + "View.cs"))
            {
                writer.WriteLine(viewCode);
            }
        }

        var registrationBody = "";
        foreach (var type in IntegrationHelper.EcsTypes)
            registrationBody += "\t\tViewsByCompTypes[typeof(" + type.Name + ")] = typeof(" + type.Name + "View);\n";
        var registratorCode = ViewRegistratorTemplate.Replace("<RegisterHere>", registrationBody);
        using (StreamWriter writer = new StreamWriter(ViewsPath + "ViewRegistrator.cs"))
        {
            writer.WriteLine(registratorCode);
        }
    }
}
