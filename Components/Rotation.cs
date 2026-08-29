using System.Runtime.CompilerServices;
using CodexECS;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CodexFramework.CodexEcsUnityIntegration.Components
{
    [MovedFrom(true, sourceNamespace: "TransformProxy", sourceAssembly: "Assembly-CSharp")]
    public struct Rotation : IComponent
    {
        public Quaternion rotation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Init(ref Rotation instance) => instance.rotation = Quaternion.identity;
    }
}
