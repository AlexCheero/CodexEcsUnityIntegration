using System.Collections.Generic;
using System.Text;
using CodexNetworking.Transport;

namespace CodexFramework.CodexEcsUnityIntegration.Networking
{
    /// <summary>Something the network stats window can describe.</summary>
    public interface INetworkDiagnosticsSource
    {
        string DiagnosticsName { get; }

        void Describe(StringBuilder builder);
    }

    /// <summary>Registry of live endpoints for the editor stats window. No LINQ: read every frame.</summary>
    public static class NetworkDiagnostics
    {
        private static readonly List<INetworkDiagnosticsSource> Sources = new List<INetworkDiagnosticsSource>();

        public static IReadOnlyList<INetworkDiagnosticsSource> All => Sources;

        public static void Register(INetworkDiagnosticsSource source)
        {
            if (source != null && !Sources.Contains(source))
                Sources.Add(source);
        }

        public static void Unregister(INetworkDiagnosticsSource source) => Sources.Remove(source);

        public static void DescribeStats(StringBuilder builder, in ConnectionStats stats)
        {
            builder.Append("  rtt ").Append((stats.SmoothedRtt * 1000).ToString("0")).Append(" ms")
                .Append("  loss ").Append((stats.PacketLossFraction * 100).ToString("0.0")).Append('%')
                .Append("  up ").Append((stats.SendRateBytesPerSecond / 1024).ToString("0.0")).Append(" KB/s")
                .Append("  down ").Append((stats.ReceiveRateBytesPerSecond / 1024).ToString("0.0")).Append(" KB/s")
                .Append("  sent ").Append(stats.PacketsSent).Append("  acked ").Append(stats.PacketsAcked)
                .Append("  lost ").Append(stats.PacketsLost).AppendLine();
        }
    }
}
