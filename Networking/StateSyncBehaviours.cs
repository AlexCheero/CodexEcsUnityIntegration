using System.Text;
using CodexECS;
using CodexNetworking.History;
using CodexNetworking.Sync.StateSync;
using CodexNetworking.Transport;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Networking
{
    /// <summary>
    /// Server-authoritative endpoint as a scene object. A game subclasses it with its input struct
    /// and supplies the world, the authoritative step and the registries. Runs one server tick per
    /// <see cref="NetworkDriverBehaviour"/> tick and snapshots after each.
    /// </summary>
    public abstract class StateSyncServerBehaviour<TInput> : NetworkDriverBehaviour where TInput : unmanaged
    {
        [SerializeField] private ushort _port = 7777;
        [SerializeField, Range(1, 256)] private int _maxClients = 16;

        public StateSyncServer<TInput> Server { get; private set; }

        protected abstract EcsWorld World { get; }
        protected abstract IServerStep<TInput> CreateStep(EcsWorld world);
        protected abstract SimulationRegistry CreateSimulationRegistry();
        protected abstract ReplicationRegistry CreateReplicationRegistry(SimulationRegistry simulation);

        protected virtual NetConfig CreateNetConfig() => new NetConfig { MaxClients = _maxClients };
        protected virtual StateSyncConfig CreateConfig() => new StateSyncConfig();

        protected virtual void Start()
        {
            SimulationRegistry simulation = CreateSimulationRegistry();
            ReplicationRegistry replication = CreateReplicationRegistry(simulation);
            EcsWorld world = World;
            Server = new StateSyncServer<TInput>(CreateNetConfig(), new UdpSocket(Address.Any(_port)), new CryptoRandom(),
                world, CreateStep(world), simulation, replication, CreateConfig());
            Server.ClientConnected += OnClientConnected;
            Server.ClientDisconnected += OnClientDisconnected;
            Server.Start();
        }

        protected virtual void OnClientConnected(int client) { }

        protected virtual void OnClientDisconnected(int client) { }

        protected override void OnNetworkUpdate(double now) => Server?.Update(now);

        protected override bool OnSimulationTick()
        {
            Server?.Step();
            return true;
        }

        protected virtual void OnDestroy()
        {
            Server?.Dispose();
            Server = null;
        }

        public override void Describe(StringBuilder builder)
        {
            if (Server == null)
            {
                builder.AppendLine("  not started");
                return;
            }
            builder.Append("  server tick ").Append(Server.Tick).Append("  clients ").Append(Server.ConnectedCount).AppendLine();
            for (int client = 0; client < Server.Host.MaxClients; client++)
            {
                if (!Server.IsClientConnected(client))
                    continue;
                builder.Append("  client ").Append(client).Append(" lead ").Append(Server.InputLead(client))
                    .Append(Server.Host.IsCongested(client) ? " congested" : "").AppendLine();
                NetworkDiagnostics.DescribeStats(builder, Server.Host.GetStats(client));
            }
        }
    }

    /// <summary>
    /// Client endpoint as a scene object: connects on start, predicts the owned entities with the
    /// game's prediction step, follows the server's clock feedback and sends inputs every frame.
    /// </summary>
    public abstract class StateSyncClientBehaviour<TInput> : NetworkDriverBehaviour where TInput : unmanaged
    {
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private ushort _port = 7777;

        public StateSyncClient<TInput> Client { get; private set; }

        protected abstract EcsWorld World { get; }
        protected abstract IPredictionStep<TInput> CreatePredictionStep(EcsWorld world);
        protected abstract SimulationRegistry CreateSimulationRegistry();
        protected abstract ReplicationRegistry CreateReplicationRegistry(SimulationRegistry simulation);

        /// <summary>Sample the local input for the tick about to be predicted.</summary>
        protected abstract TInput ReadInput();

        protected virtual NetConfig CreateNetConfig() => new NetConfig();
        protected virtual StateSyncConfig CreateConfig() => new StateSyncConfig();

        protected override double RateScale => Client == null ? 1.0 : Client.RecommendedRateScale;

        protected virtual void Start()
        {
            SimulationRegistry simulation = CreateSimulationRegistry();
            ReplicationRegistry replication = CreateReplicationRegistry(simulation);
            EcsWorld world = World;
            Client = new StateSyncClient<TInput>(CreateNetConfig(), new UdpSocket(Address.Any(0)), new CryptoRandom(),
                world, CreatePredictionStep(world), simulation, replication, CreateConfig());
            if (UdpSocket.TryResolve(_host, _port, out Address server))
                Client.Connect(server, Now);
            else
                Debug.LogError("StateSyncClientBehaviour: cannot resolve " + _host, this);
        }

        protected override void OnNetworkUpdate(double now) => Client?.Update(now);

        protected override bool OnSimulationTick()
        {
            if (Client == null || !Client.IsReady)
                return false;
            Client.SubmitInput(ReadInput());
            Client.Advance();
            return true;
        }

        /// <summary>Sends inputs and refreshes the Interpolated copies for this frame's render time.</summary>
        protected override void OnFrameEnd(double now)
        {
            if (Client == null)
                return;
            Client.SendInputs();
            Client.UpdateInterpolation(Client.RenderTick(Clock.Alpha));
        }

        protected virtual void OnDestroy()
        {
            Client?.Dispose();
            Client = null;
        }

        public override void Describe(StringBuilder builder)
        {
            if (Client == null)
            {
                builder.AppendLine("  not started");
                return;
            }
            builder.Append("  ").Append(Client.Client.State).Append("  client index ").Append(Client.ClientIndex)
                .Append("  tick ").Append(Client.Tick).Append("  server tick ").Append(Client.LastAppliedTick)
                .Append("  lead ").Append(Client.LastInputLead).Append("  rate ").Append(Client.RecommendedRateScale.ToString("0.00"))
                .Append("  reconciliations ").Append(Client.Reconciliations).Append("  hard snaps ").Append(Client.HardSnaps)
                .Append("  ghosts ").Append(Client.Ghosts.Count).AppendLine();
            NetworkDiagnostics.DescribeStats(builder, Client.Client.Stats);
        }
    }
}
