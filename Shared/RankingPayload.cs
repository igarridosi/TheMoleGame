using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class RankingPayload
    {
        public List<RankingEntry> List { get; set; }
        public GlobalStats Stats { get; set; }
    }
}
