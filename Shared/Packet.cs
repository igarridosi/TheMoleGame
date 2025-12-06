using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class Packet
    {
        public PacketType Type { get; set; }
        public string Message { get; set; } // Hemen JSON bat joango da datuekin
    }
}
