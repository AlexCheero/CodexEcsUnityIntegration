using System.Text;
using CodexECS;
using CodexNetworking.History;
using CodexNetworking.Sync.InputSync;
using CodexNetworking.Transport;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Networking
{
    /// <summary>Shared pieces of the input-sync host and client behaviours.</summary>
    public abstract class InputSyncBehaviour<TInput> : NetworkDriverBehaviour where TInput : unmanaged
    {
        [SerializeField, Range(1, 64)] private int _peerCount = 2;
        [SerializeField, Range(0, 60)] private int _inputDelay = 2;
        [SerializeField, Range(0, 60)] private int _maxRollbackTicks = 8;

        protected abstract EcsWorld World { get; }
        protected abstract IInputStep<TInput> CreateStep(EcsWorld world, int peerCount);
        protected abstract SimulationRegistry CreateSimulationRegistry();

        /// <summary>Sample the local input for the tick it will apply to (<see cref="InputSyncSession{TInput}.LocalInputTick"/>).</summary>
        protected abstract TInput ReadInput(int tick);

        protected virtual NetConfig CreateNetConfig() => new NetConfig();

        protected virtual InputSyncConfig CreateConfig() => new InputSyncConfig
        {
            PeerCount = _peerCount,
            InputDelay = _inputDelay,
            MaxRollbackTicks = _maxRollbackTicks,
        };

        protected InputSyncSession<TInput> CreateSession(int localPeer)
        {
            EcsWorld world = World;
            InputSyncConfig config = CreateConfig();
            var session = new InputSyncSession<TInput>(world, CreateStep(world, config.PeerCount), CreateSimulationRegistry(), config, localPeer);
            session.Desynced += OnDesynced;
            return session;
        }

        protected virtual void OnDesynced(int tick, int peer) =>
            Debug.LogError("Input sync desync at tick " + tick + " with peer " + peer, this);

        protected bool TickSession(InputSyncSession<TInput> session)
        {
            if (session == null)
                return false;
            session.SubmitLocalInput(ReadInput(session.LocalInputTick));
            return session.TryAdvance();
        }

        protected static void DescribeSession(StringBuilder builder, InputSyncSession<TInput> session)
        {
            if (session == null)
            {
                builder.AppendLine("  no session yet");
                return;
            }
            builder.Append("  peer ").Append(session.LocalPeer).Append('/').Append(session.PeerCount)
                .Append("  tick ").Append(session.Tick).Append("  confirmed ").Append(session.ConfirmedTick)
                .Append("  rollbacks ").Append(session.RollbackCount).Append("  resimulated ").Append(session.ResimulatedTicks)
                .Append("  stalls ").Append(session.Stalls).AppendLine();
        }
    }

    /// <summary>Peer 0 and relay of an input-sync session, listening on a port.</summary>
    public abstract class InputSyncHostBehaviour<TInput> : InputSyncBehaviour<TInput> where TInput : unmanaged
    {
        [SerializeField] private ushort _port = 7777;

        public InputSyncRelayHost<TInput> Relay { get; private set; }

        public InputSyncSession<TInput> Session => Relay?.Session;

        protected virtual void Start()
        {
            InputSyncSession<TInput> session = CreateSession(0);
            session.Start();
            NetConfig netConfig = CreateNetConfig();
            netConfig.MaxClients = Mathf.Max(netConfig.MaxClients, session.PeerCount);
            Relay = new InputSyncRelayHost<TInput>(netConfig, new UdpSocket(Address.Any(_port)), new CryptoRandom(), session);
        }

        protected override void OnNetworkUpdate(double now) => Relay?.Update(now);

        /// <summary>Does not advance until every peer has connected; afterwards a stall ends the frame's ticking.</summary>
        protected override bool OnSimulationTick()
        {
            if (Relay == null || Relay.ConnectedPeers < Session.PeerCount)
                return false;
            return TickSession(Session);
        }

        protected override void OnFrameEnd(double now) => Relay?.Pump();

        protected virtual void OnDestroy()
        {
            Relay?.Dispose();
            Relay = null;
        }

        public override void Describe(StringBuilder builder)
        {
            if (Relay == null)
            {
                builder.AppendLine("  not started");
                return;
            }
            builder.Append("  connected peers ").Append(Relay.ConnectedPeers).AppendLine();
            DescribeSession(builder, Session);
            for (int client = 0; client < Relay.Host.MaxClients; client++)
            {
                if (Relay.Host.IsConnected(client))
                    NetworkDiagnostics.DescribeStats(builder, Relay.Host.GetStats(client));
            }
        }
    }

    /// <summary>A client peer of an input-sync session; the session is created when the host assigns the peer index.</summary>
    public abstract class InputSyncClientBehaviour<TInput> : InputSyncBehaviour<TInput> where TInput : unmanaged
    {
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private ushort _port = 7777;

        public InputSyncClientLink<TInput> Link { get; private set; }

        public InputSyncSession<TInput> Session => Link?.Session;

        protected virtual void Start()
        {
            Link = new InputSyncClientLink<TInput>(CreateNetConfig(), new UdpSocket(Address.Any(0)), new CryptoRandom(), CreateSession);
            if (UdpSocket.TryResolve(_host, _port, out Address host))
                Link.Connect(host, Now);
            else
                Debug.LogError("InputSyncClientBehaviour: cannot resolve " + _host, this);
        }

        protected override void OnNetworkUpdate(double now) => Link?.Update(now);

        protected override bool OnSimulationTick() => TickSession(Session);

        protected override void OnFrameEnd(double now) => Link?.Pump();

        protected virtual void OnDestroy()
        {
            Link?.Dispose();
            Link = null;
        }

        public override void Describe(StringBuilder builder)
        {
            if (Link == null)
            {
                builder.AppendLine("  not started");
                return;
            }
            builder.Append("  ").Append(Link.Client.State).AppendLine();
            DescribeSession(builder, Session);
            NetworkDiagnostics.DescribeStats(builder, Link.Client.Stats);
        }
    }
}
