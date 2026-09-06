using System;
using CodexECS;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CodexFramework.CodexEcsUnityIntegration.Components
{
    [MovedFrom(true, sourceNamespace: "TransformProxy", sourceAssembly: "Assembly-CSharp")]
    [Serializable]
    public struct Position : IComponent
    {
        public Vector3 position;
    }
}
