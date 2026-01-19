using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class GlobalStats
    {
        public int TotalMatches { get; set; }      // Partidak guztira
        public string TopImpostor { get; set; }    // Jokalari onena
        public int TopImpostorWins { get; set; }   // Bere garaipenak
        public double AvgRounds { get; set; }      // Batezbesteko rondak
        public double ImpostorWinRate { get; set; } // Inpostoreen %
    }
}
