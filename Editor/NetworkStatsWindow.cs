using System.Collections.Generic;
using System.Text;
using CodexFramework.CodexEcsUnityIntegration.Networking;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Editor
{
    /// <summary>Live statistics of every registered network endpoint in the running scene.</summary>
    public sealed class NetworkStatsWindow : EditorWindow
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private Vector2 _scroll;

        [MenuItem("Codex/Network Stats")]
        private static void Open() => GetWindow<NetworkStatsWindow>("Network Stats");

        private void Update()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            IReadOnlyList<INetworkDiagnosticsSource> sources = NetworkDiagnostics.All;
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode to see network endpoints.", MessageType.Info);
                return;
            }
            if (sources.Count == 0)
            {
                EditorGUILayout.HelpBox("No network endpoint registered. Add a StateSync, InputSync or LoopbackTransportSample behaviour.", MessageType.Info);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < sources.Count; i++)
            {
                _builder.Clear();
                sources[i].Describe(_builder);
                EditorGUILayout.LabelField(sources[i].DiagnosticsName, EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_builder.ToString(), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
