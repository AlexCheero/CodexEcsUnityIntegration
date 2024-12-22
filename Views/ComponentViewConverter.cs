using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Views
{

#if UNITY_EDITOR
    static class ComponentViewConverter
    {
        private static readonly string ComponentViewTemplate =
            "using CodexECS;\r\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Components;\r\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Tags;\r\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Views;\r\n" +
            "using UnityEngine;\r\n" +
            "[DisallowMultipleComponent]\r\n" +
            "public class <ComponentName>View : ComponentView<<ComponentName>>{}";

        private static readonly string ViewRegistratorTemplate =
            "using System;\r\n" +
            "using System.Collections.Generic;\r\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Components;\r\n" +
            "using CodexFramework.CodexEcsUnityIntegration.Tags;\r\n" +
            "using CodexECS;\r\n" +
            "public static class ViewRegistrator\r\n" +
            "{\r\n" +
            "\tprivate static Dictionary<Type, Type> _viewsByCompTypes = new();\r\n" +
            "\tpublic static Type GetViewTypeByCompType(Type compType) => _viewsByCompTypes[compType];\r\n" +
            "\tpublic static bool IsTypeHaveView(Type compType) => _viewsByCompTypes.ContainsKey(compType);\r\n\r\n" +
            "\tpublic static void Register()\r\n" +
            "\t{\r\n" +
            "\t\tint id;\r\n" +
            "<RegisterHere>" +
            "\t}\r\n" +
            "}";

        private static readonly string ViewsPath = "Assets/Scripts/Monobehaviours/ComponentViews/";

        private static StringBuilder _registratorBuilder;

        static ComponentViewConverter()
        {
            _registratorBuilder = new();
        }
        
        [MenuItem("CodeGen/ECS/Generate component views", false, -1)]
        public static void GenerateComponentViews()
        {
            var dir = new DirectoryInfo(ViewsPath);
            foreach (FileInfo file in dir.GetFiles())
                file.Delete();

            _registratorBuilder.Clear();
            foreach (var type in IntegrationHelper.EcsComponentTypes)
            {
                if (type.GetCustomAttribute<SkipViewGenerationAttribute>() != null)
                    continue;
                
                if (type.IsGenericType)
                {
                    Debug.Log($"Can't generate view for generic type component {type.FullName}");
                    continue;
                }
                
                var viewCode = ComponentViewTemplate.Replace("<ComponentName>", type.Name);
                using (StreamWriter writer = new StreamWriter(ViewsPath + type.Name + "View.cs"))
                {
                    writer.WriteLine(viewCode);
                }
                
                _registratorBuilder.Append("\t\t_viewsByCompTypes[typeof(" + type.Name + ")] = typeof(" + type.Name + "View);\r\n" +
                                       "\t\tid = ComponentMeta<" + type.Name + ">.Id;\r\n");
            }
            
            var registratorCode = ViewRegistratorTemplate.Replace("<RegisterHere>", _registratorBuilder.ToString());
            using (StreamWriter writer = new StreamWriter(ViewsPath + "ViewRegistrator.cs"))
            {
                writer.WriteLine(registratorCode);
            }
        }
    }
#endif
}