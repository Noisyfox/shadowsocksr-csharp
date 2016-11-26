using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Model.Strategy.HighAvailability
{
    public class ServerStatus
    {
        // time interval between SYN and SYN+ACK
        public TimeSpan latency;
        public DateTime lastTimeDetectLatency;

        // last time anything received
        public DateTime lastRead;

        // last time anything sent
        public DateTime lastWrite;

        // connection refused or closed before anything received
        public DateTime lastFailure;

        //public Server server;

        public double score;

        public ServerStatus()
        {
            lastFailure = DateTime.MinValue;
            lastRead = DateTime.Now;
            lastWrite = DateTime.Now;
            latency = new TimeSpan(0, 0, 0, 0, 10);
            lastTimeDetectLatency = DateTime.Now;
        }
    }

    public class MaxServer
    {
        public Server server;
        public double score;
    }
}
