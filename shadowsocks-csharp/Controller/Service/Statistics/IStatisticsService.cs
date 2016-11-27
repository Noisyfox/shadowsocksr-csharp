using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shadowsocks.Model;

namespace Shadowsocks.Controller
{
    public interface IStatisticsService
    {
        string ID { get; }

        void UpdateLatency(Server server, TimeSpan latency);

        void UpdateInboundCounter(Server server, long n);

        void UpdateOutboundCounter(Server server, long n);
    }
}
