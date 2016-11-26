using Shadowsocks.Model;
using System;
using System.Net;
using Shadowsocks.Model.Strategy.HighAvailability;

namespace Shadowsocks.Controller.Strategy
{
    class HighAvailabilityStrategy : ManagedStrategy<int, int, int, MaxServer, ServerStatus>
    {

        public HighAvailabilityStrategy(ShadowsocksController controller) : base(controller, false, false, true)
        {
        }

        public override string Name { get; } = I18N.GetString("High Availability");

        public override string ID { get; } = "com.shadowsocks.strategy.ha";

        public override void ReloadServers()
        {
            base.ReloadServers();

            ChooseNewServer();
        }

        public override Server GetAServer(StrategyCallerType type, System.Net.IPEndPoint localIPEndPoint,
            EndPoint destEndPoint)
        {
            if (type == StrategyCallerType.TCP)
            {
                ChooseNewServer();
            }

            using (var d = AquireMemoryExclusive())
            {
                return d.Data.server;
            }
        }

        /**
         * once failed, try after 5 min
         * and (last write - last read) < 5s
         * and (now - last read) <  5s  // means not stuck
         * and latency < 200ms, try after 30s
         */
        private void ChooseNewServer()
        {
            DateTime now = DateTime.Now;

            using (var d = AquireMemoryExclusive())
            {
                ServerStatus max = null;
                Server maxServer = null;

                foreach (var server in CurrentServers)
                {
                    var status = d.GetData(server);
                    // all of failure, latency, (lastread - lastwrite) normalized to 1000, then
                    // 100 * failure - 2 * latency - 0.5 * (lastread - lastwrite)
                    status.score =
                        100*1000*Math.Min(5*60, (now - status.lastFailure).TotalSeconds)
                        -
                        2*5*
                        (Math.Min(2000, status.latency.TotalMilliseconds)/
                         (1 + (now - status.lastTimeDetectLatency).TotalSeconds/30/10) +
                         -0.5*200*Math.Min(5, (status.lastRead - status.lastWrite).TotalSeconds));
                    

                    Logging.Debug($"server: {server.FriendlyName()} latency:{status.latency} score: {status.score}");


                    if (max == null)
                    {
                        max = status;
                        maxServer = server;
                    }
                    else
                    {
                        if (status.score >= max.score)
                        {
                            max = status;
                            maxServer = server;
                        }
                    }
                }
                if (max != null && (d.Data.server == null || max.score - d.Data.score > 200))
                {
                    d.Data.server = maxServer;
                    d.Data.score = max.score;

                    Logging.Info($"HA switching to server: {maxServer.FriendlyName()}");
                }
            }
        }

        public override void UpdateLatency(Model.Server server, TimeSpan latency)
        {
            Logging.Debug($"latency: {server.FriendlyName()} {latency}");


            using (var d = AquireMemoryExclusive())
            {
                ServerStatus status;
                if (d.TryGetData(server, out status))
                {
                    status.latency = latency;
                    status.lastTimeDetectLatency = DateTime.Now;
                }

            }
        }

        public override void UpdateLastRead(Model.Server server)
        {
            Logging.Debug($"last read: {server.FriendlyName()}");

            using (var d = AquireMemoryExclusive())
            {
                ServerStatus status;
                if (d.TryGetData(server, out status))
                {
                    status.lastRead = DateTime.Now;
                }
            }
        }

        public override void UpdateLastWrite(Model.Server server)
        {
            Logging.Debug($"last write: {server.FriendlyName()}");

            using (var d = AquireMemoryExclusive())
            {
                ServerStatus status;
                if (d.TryGetData(server, out status))
                {
                    status.lastWrite = DateTime.Now;
                }
            }
        }

        public override void SetFailure(Model.Server server)
        {
            Logging.Debug($"failure: {server.FriendlyName()}");

            using (var d = AquireMemoryExclusive())
            {
                ServerStatus status;
                if (d.TryGetData(server, out status))
                {
                    status.lastFailure = DateTime.Now;
                }
            }
        }

        public override void Activate()
        {
        }

        public override void Deactivate()
        {
        }

        public override void Dispose()
        {
        }
    }
}
