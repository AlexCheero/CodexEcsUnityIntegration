using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    /// <summary>
    /// Emits compile-time typeof(ComponentWrapper&lt;T&gt;) anchors for all IComponent types
    /// so IL2CPP does not strip closed generics. Runs on player build and via menu — not a live source generator.
    /// </summary>
    public sealed class ComponentWrapperAotPreserveGenerator : IPreprocessBuildWithReport
    {
        public const string GeneratedAssetPath =
            "Assets/Scripts/Generated/ComponentWrapperAotPreserve.cs";

        private const string MenuPath = "CodexEcsUnityIntegration/Generate ComponentWrapper AOT Preserve";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!TryGenerate(out var changed, out var error))
                throw new BuildFailedException($"ComponentWrapper AOT preserve generation failed: {error}");
            if (changed)
            {
                AssetDatabase.Refresh();
                throw new BuildFailedException(
                    "ComponentWrapper AOT preserve file was outdated and has been regenerated. Build again.");
            }
        }

        [MenuItem(MenuPath)]
        public static void GenerateFromMenu()
        {
            if (!TryGenerate(out var changed, out var error))
            {
                Debug.LogError($"[ComponentWrapperAotPreserve] Failed: {error}");
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log(changed
                ? $"[ComponentWrapperAotPreserve] Updated {GeneratedAssetPath}"
                : $"[ComponentWrapperAotPreserve] Already up to date ({GeneratedAssetPath})");
        }

        public static bool TryGenerate(out bool changed, out string error)
        {
            changed = false;
            error = null;
            try
            {
                var componentTypes = CollectComponentTypes();
                var content = BuildSource(componentTypes);
                var fullPath = Path.GetFullPath(GeneratedAssetPath);
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var previous = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
                if (string.Equals(previous, content, StringComparison.Ordinal))
                    return true;
                File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                changed = true;
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        private static List<Type> CollectComponentTypes()
        {
            var result = new List<Type>();
            var iComponent = typeof(IComponent);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!ShouldScanAssembly(assembly))
                    continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }
                foreach (var type in types)
                {
                    if (type == null || type.IsInterface || type.IsAbstract || type.IsGenericTypeDefinition)
                        continue;
                    if (!iComponent.IsAssignableFrom(type))
                        continue;
                    result.Add(type);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(GetTypeDisplayName(a), GetTypeDisplayName(b)));
            return result;
        }

        private static bool ShouldScanAssembly(Assembly assembly)
        {
            if (assembly.IsDynamic)
                return false;
            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.StartsWith("Unity", StringComparison.Ordinal) ||
                name.StartsWith("System", StringComparison.Ordinal) ||
                name.StartsWith("mscorlib", StringComparison.Ordinal) ||
                name.StartsWith("netstandard", StringComparison.Ordinal) ||
                name.StartsWith("Mono.", StringComparison.Ordinal) ||
                name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Bee", StringComparison.Ordinal) ||
                name.StartsWith("Newtonsoft", StringComparison.Ordinal))
                return false;
            if (name.EndsWith(".Editor", StringComparison.Ordinal) ||
                name.IndexOf("Editor", StringComparison.Ordinal) >= 0)
                return false;
            return true;
        }

        private static string BuildSource(List<Type> componentTypes)
        {
            var sb = new StringBuilder(capacity: 4096 + componentTypes.Count * 80);
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by ComponentWrapperAotPreserveGenerator. Do not edit.");
            sb.AppendLine("// Menu: CodexEcsUnityIntegration/Generate ComponentWrapper AOT Preserve");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using CodexFramework.CodexEcsUnityIntegration.Views;");
            sb.AppendLine();
            sb.AppendLine("namespace CodexFramework.CodexEcsUnityIntegration");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>IL2CPP anchors for closed ComponentWrapper&lt;T&gt; generics.</summary>");
            sb.AppendLine("    static class ComponentWrapperAotPreserve");
            sb.AppendLine("    {");
            sb.AppendLine("        static ComponentWrapperAotPreserve()");
            sb.AppendLine("        {");
            foreach (var type in componentTypes)
            {
                var name = GetTypeDisplayName(type);
                sb.Append("            _ = typeof(ComponentWrapper<");
                sb.Append(name);
                sb.AppendLine(">);");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string GetTypeDisplayName(Type type)
        {
            if (type.IsNested)
                return $"global::{type.FullName.Replace('+', '.')}";
            if (!string.IsNullOrEmpty(type.Namespace))
                return $"global::{type.Namespace}.{type.Name}";
            return $"global::{type.Name}";
        }
    }
}
