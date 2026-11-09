using System.Text;
using CodexNetworking.History;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration.Networking
{
    /// <summary>
    /// Fixed-tick driver for a networking endpoint: pumps the transport every frame, converts frame
    /// time into simulation ticks through a <see cref="TickClock"/> (whose rate the subclass may
    /// nudge for clock sync) and runs the per-tick and per-frame hooks. Uses unscaled real time, so
    /// pausing the game's time scale does not stall the network.
    /// </summary>
    public abstract class NetworkDriverBehaviour : MonoBehaviour, INetworkDiagnosticsSource
    {
        [SerializeField, Min(1)] private int _ticksPerSecond = 60;
        [SerializeField, Range(1, 32)] private int _maxTicksPerFrame = 8;

        private TickClock _clock;

        public TickClock Clock => _clock ??= new TickClock(1.0 / _ticksPerSecond, _maxTicksPerFrame);

        public int TicksPerSecond => _ticksPerSecond;

        /// <summary>Monotonic real time in seconds, the transport's clock.</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>Speed of the local tick clock relative to nominal; override to follow server feedback.</summary>
        protected virtual double RateScale => 1.0;

        protected virtual void OnEnable() => NetworkDiagnostics.Register(this);

        protected virtual void OnDisable() => NetworkDiagnostics.Unregister(this);

        protected virtual void Update()
        {
            double now = Now;
            OnNetworkUpdate(now);
            Clock.RateScale = RateScale;
            int ticks = Clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++)
            {
                if (!OnSimulationTick())
                    break;
            }
            OnFrameEnd(now);
        }

        /// <summary>Receive packets, run timeouts.</summary>
        protected abstract void OnNetworkUpdate(double now);

        /// <summary>One simulation tick. Return false to stop ticking this frame (a lockstep stall).</summary>
        protected abstract bool OnSimulationTick();

        /// <summary>Send what the ticks produced.</summary>
        protected virtual void OnFrameEnd(double now) { }

        public virtual string DiagnosticsName => name;

        public virtual void Describe(StringBuilder builder) { }
    }
}
