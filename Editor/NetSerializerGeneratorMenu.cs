using System;
using System.Collections.Generic;
using System.IO;
using CodexNetworking.Serialization.Generation;
using UnityEditor;
using UnityEngine;

namespace CodexEcsUnityIntegration.Editor
{
    /// <summary>
    /// Generates field-aware network serializers for every [Replicated] struct in the project's
    /// script assemblies and writes them to one file. Call the generated
    /// <c>GeneratedNetSerializers.RegisterAll()</c> once at startup, before any session is created.
    /// Regenerate after changing a replicated component or its attributes; the file is plain code.
    /// </summary>
    public static class NetSerializerGeneratorMenu
    {
        private const string OutputPath = "Assets/Generated/NetSerializers.g.cs";
        private const string OutputNamespace = "Game.Generated";

        [MenuItem("Codex/Networking/Generate Net Serializers")]
        public static void Generate()
        {
            var types = new List<Type>();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                // Only the project's own script assemblies; engine, editor and package code never holds game components.
                if (name.StartsWith("Unity", StringComparison.Ordinal) || name.StartsWith("System", StringComparison.Ordinal) ||
                    name.StartsWith("mscorlib", StringComparison.Ordinal) || name.StartsWith("netstandard", StringComparison.Ordinal) ||
                    name.EndsWith("-Editor", StringComparison.Ordinal) || name.Contains(".Editor") || name.Contains("Tests"))
                    continue;
                types.AddRange(NetSerializerGenerator.FindReplicated(assembly));
            }
            if (types.Count == 0)
            {
                Debug.LogWarning("Net serializer generator: no [Replicated] structs found.");
                return;
            }
            string source = NetSerializerGenerator.Generate(types, new GeneratorOptions { Namespace = OutputNamespace });
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, source);
            AssetDatabase.ImportAsset(OutputPath);
            Debug.Log("Net serializer generator: wrote " + types.Count + " serializers to " + OutputPath);
        }
    }
}
