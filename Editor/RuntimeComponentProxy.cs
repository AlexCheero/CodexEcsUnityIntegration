using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexUnityFramework.CodexEcsUnityIntegration.Editor
{
    internal sealed class RuntimeComponentProxy : ScriptableObject
    {
        [SerializeReference]
        public ComponentWrapper Value;
    }
}
