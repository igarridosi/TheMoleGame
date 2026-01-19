using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class RankingEntry
    {
        public string Username { get; set; }
        public int GamesPlayed { get; set; }
        public int TotalWins { get; set; }
        public int ImpostorWins { get; set; }
        public string WinRate { get; set; } // "%60" formatuan
    }
}
