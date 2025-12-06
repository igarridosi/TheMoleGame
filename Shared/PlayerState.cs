using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class PlayerState
    {
        public string Username { get; set; }
        public string SubmittedWord { get; set; } // Jokalariak esan duen hitza
        public bool IsTurn { get; set; }          // Bere txanda al da?
        public bool IsEliminated { get; set; }    // Kanporatua dagoen?
        public bool IsVotingPhase { get; set; }   // Bozketa fasean gaude? (Botoia erakusteko)
    }
}
