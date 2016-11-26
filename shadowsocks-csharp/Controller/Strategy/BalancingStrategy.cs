using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Model.Strategy.Balancing;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Shadowsocks.Controller.Strategy
{
    class BalancingStrategy : ManagedStrategy<Config, int, int, int, int>
    {
        Random _random;

        private MenuItem _sameServerMenuItem;

        public BalancingStrategy(ShadowsocksController controller) : base(controller, true)
        {
            _random = new Random();

            SubMenuItems = new[] {
                _sameServerMenuItem = ViewUtils.CreateMenuItem("Same Server for Same Site", SameServerClick)
            };

            UpdateMenuStatus();
        }

        public override string Name { get; } = I18N.GetString("Load Balance");

        public override string ID { get; } = "com.shadowsocks.strategy.balancing";

        public override MenuItem[] SubMenuItems { get; }

        public override Server GetAServer(StrategyCallerType type, IPEndPoint localIPEndPoint, EndPoint destEndPoint)
        {
            var configs = CurrentServers;
            int index;
            if (type == StrategyCallerType.TCP)
            {
                index = _random.Next();
            }
            else
            {
                index = localIPEndPoint.GetHashCode();
            }
            return configs[index % configs.Count];
        }

        public override void UpdateLatency(Server server, TimeSpan latency)
        {
        }

        public override void UpdateLastRead(Server server)
        {
        }

        public override void UpdateLastWrite(Server server)
        {
        }

        public override void SetFailure(Server server)
        {
        }

        public override void Activate()
        {
        }

        public override void Dispose()
        {
        }


        #region Menu & Config

        private void UpdateMenuStatus()
        {
            using (AquireConfigExclusive())
            {
                _sameServerMenuItem.Checked = Config.sameServer;
            }
        }

        private void SameServerClick(object sender, EventArgs eventArgs)
        {
            using (AquireConfigExclusive())
            {
                Config.sameServer = !Config.sameServer;

                SaveConfig();
            }

            UpdateMenuStatus();
        }

        #endregion
    }
}
