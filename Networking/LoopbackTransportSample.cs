using System;
using System.Text;
using CodexNetworking.Transport;
using CodexNetworking.Transport.Simulator;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Networking
{
    /// <summary>
    /// Play-mode sample: a host and a client in one process over the deterministic network
    /// simulator, with the link conditions editable in the inspector while playing. Sends one payload
    /// per frame each way and shows the transport statistics in the Network Stats window. Exercises
    /// the transport only; it carries no simulation.
    /// </summary>
    public sealed class LoopbackTransportSample : MonoBehaviour, INetworkDiagnosticsSource
    {
        private sealed class Counter : NetListener
        {
            public int Packets;
            public int Acked;
            public int Lost;
            public int Connections;

            public override void OnConnected(int connection) => Connections++;
            public override void OnDisconnected(int connection, DisconnectReason reason) => Connections--;
            public override void OnPacket(int connection, ushort sequence, ReadOnlySpan<byte> payload) => Packets++;
            public override void OnPacketAcked(int connection, ushort sequence) => Acked++;
            public override void OnPacketLost(int connection, ushort sequence) => Lost++;
        }

        [SerializeField, Range(0f, 0.5f)] private float _latencySeconds = 0.05f;
        [SerializeField, Range(0f, 0.2f)] private float _jitterSeconds = 0.01f;
        [SerializeField, Range(0f, 1f)] private float _lossChance = 0.05f;
        [SerializeField, Range(0f, 1f)] private float _duplicateChance = 0f;
        [SerializeField, Range(1, 1100)] private int _payloadBytes = 200;

        private static readonly Address HostAddress = Address.IPv4(10, 0, 0, 1, 7777);
        private static readonly Address ClientAddress = Address.IPv4(10, 0, 0, 2, 40000);

        private SimulatedNetwork _network;
        private NetHost _host;
        private NetClient _client;
        private readonly Counter _hostCounter = new Counter();
        private readonly Counter _clientCounter = new Counter();
        private byte[] _payload = Array.Empty<byte>();

        private void OnEnable()
        {
            var config = new NetConfig();
            _network = new SimulatedNetwork((ulong)Environment.TickCount);
            _host = new NetHost(config, _network.CreateSocket(HostAddress), new XorShiftRandom(1), _hostCounter);
            _client = new NetClient(config, _network.CreateSocket(ClientAddress), new XorShiftRandom(2), _clientCounter);
            _client.Connect(HostAddress, NetworkDriverBehaviour.Now);
            NetworkDiagnostics.Register(this);
        }

        private void OnDisable()
        {
            NetworkDiagnostics.Unregister(this);
            _client?.Dispose();
            _host?.Dispose();
            _client = null;
            _host = null;
        }

        private void Update()
        {
            double now = NetworkDriverBehaviour.Now;
            _network.Conditions = NetworkConditions.Create(_latencySeconds, _jitterSeconds, _lossChance, _duplicateChance);
            _network.Update(now);
            _host.Update(now);
            _client.Update(now);
            if (_payload.Length != _payloadBytes)
                _payload = new byte[_payloadBytes];
            if (_client.IsConnected)
                _client.TrySend(_payload, out _);
            if (_host.ConnectedCount > 0)
                _host.TrySend(0, _payload, out _);
        }

        public string DiagnosticsName => name + " (loopback sample)";

        public void Describe(StringBuilder builder)
        {
            if (_client == null)
                return;
            builder.Append("  client ").Append(_client.State).Append("  received ").Append(_clientCounter.Packets)
                .Append("  acked ").Append(_clientCounter.Acked).Append("  lost ").Append(_clientCounter.Lost)
                .Append(_client.IsCongested ? "  congested" : "").AppendLine();
            NetworkDiagnostics.DescribeStats(builder, _client.Stats);
            builder.Append("  host clients ").Append(_host.ConnectedCount).Append("  received ").Append(_hostCounter.Packets)
                .Append("  acked ").Append(_hostCounter.Acked).Append("  lost ").Append(_hostCounter.Lost).AppendLine();
            if (_host.ConnectedCount > 0)
                NetworkDiagnostics.DescribeStats(builder, _host.GetStats(0));
            builder.Append("  simulator in flight ").Append(_network.InFlight).Append("  dropped ").Append(_network.Dropped).AppendLine();
        }
    }
}
