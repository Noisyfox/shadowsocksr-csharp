using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Model.Strategy.Balancing
{

    [Serializable]
    public class Config
    {
        public bool sameServer; // Use same server for the same target
    }
}
