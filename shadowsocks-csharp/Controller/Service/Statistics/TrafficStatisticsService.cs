using System;
using System.Collections.Generic;
using System.Threading;
using Shadowsocks.Model;

namespace Shadowsocks.Controller
{
    public class TrafficStatisticsService : IStatisticsService
    {
        public static event EventHandler<QueueLast<TrafficPerSecond>> TrafficChanged;

        public static long InboundCounter => Interlocked.Read(ref Instance._inboundCounter);

        public static long OutboundCounter => Interlocked.Read(ref Instance._outboundCounter);

        public static void StartTrafficStatistics(ShadowsocksController controller, int queueMaxSize)
        {
            Instance.StartTrafficStatistics(queueMaxSize);

            controller.RegisterStatisticsService(Instance);
        }

        private static TrafficStatisticsService Instance = new TrafficStatisticsService();

        private TrafficStatisticsService()
        {
            
        }


        public class TrafficPerSecond
        {
            public long inboundCounter;
            public long outboundCounter;
            public long inboundIncreasement;
            public long outboundIncreasement;
        }

        public class QueueLast<T> : Queue<T>
        {
            public T Last { get; private set; }
            public new void Enqueue(T item)
            {
                Last = item;
                base.Enqueue(item);
            }
        }

        private Thread _trafficThread;

        private long _inboundCounter = 0;
        private long _outboundCounter = 0;
        private QueueLast<TrafficPerSecond> _traffic;

        public string ID { get; } = typeof(TrafficStatisticsService).FullName;

        public void UpdateLatency(Server server, TimeSpan latency)
        {
        }

        public void UpdateInboundCounter(Server server, long n)
        {
            Interlocked.Add(ref _inboundCounter, n);
        }

        public void UpdateOutboundCounter(Server server, long n)
        {
            Interlocked.Add(ref _outboundCounter, n);
        }

        private void StartTrafficStatistics(int queueMaxSize)
        {
            _traffic = new QueueLast<TrafficPerSecond>();
            for (int i = 0; i < queueMaxSize; i++)
            {
                _traffic.Enqueue(new TrafficPerSecond());
            }
            _trafficThread = new Thread(() => TrafficStatistics(queueMaxSize));
            _trafficThread.IsBackground = true;
            _trafficThread.Start();
        }

        private void TrafficStatistics(int queueMaxSize)
        {
            while (true)
            {
                TrafficPerSecond previous = _traffic.Last;
                TrafficPerSecond current = new TrafficPerSecond();

                var inbound = current.inboundCounter = InboundCounter;
                var outbound = current.outboundCounter = OutboundCounter;
                current.inboundIncreasement = inbound - previous.inboundCounter;
                current.outboundIncreasement = outbound - previous.outboundCounter;

                _traffic.Enqueue(current);
                if (_traffic.Count > queueMaxSize)
                    _traffic.Dequeue();

                TrafficChanged?.Invoke(null, _traffic);

                Thread.Sleep(1000);
            }
        }
    }
}
