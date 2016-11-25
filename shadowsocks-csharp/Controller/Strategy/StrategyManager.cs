using Shadowsocks.Controller;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Controller.Strategy
{
    sealed class StrategyManager
    {
        public static StrategyManager Instance { get; private set; }

        public static void InitInstance(ShadowsocksController controller)
        {
            if (Instance == null)
            {
                Instance = new StrategyManager(controller);
            }
        }

        public static void ShutdownInstance()
        {
            if (Instance != null)
            {
                Instance.SetCurrentStrategy(null);
                Instance.Dispose();
                Instance = null;
            }
        }


        List<IStrategy> _strategies;
        private StrategyManager(ShadowsocksController controller)
        {
            _strategies = new List<IStrategy>();
            _strategies.Add(new BalancingStrategy(controller));
            _strategies.Add(new HighAvailabilityStrategy(controller));
            _strategies.Add(new StatisticsStrategy(controller));
            // TODO: load DLL plugins
        }
        public IList<IStrategy> GetStrategies()
        {
            return _strategies;
        }

        public IStrategy GetStrategy(string id)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.ID == id)
                {
                    return strategy;
                }
            }
            return null;
        }

        public IStrategy CurrentStrategy { get; private set; }

        public void SetCurrentStrategy(string id)
        {
            var s = GetStrategy(id);

            if (s != CurrentStrategy)
            {
                CurrentStrategy?.Deactivate();
                s?.Activate();
                CurrentStrategy = s;
            }
        }

        private void Dispose()
        {
            foreach (var strategy in _strategies)
            {
                strategy.Dispose();
            }
        }
    }
}
