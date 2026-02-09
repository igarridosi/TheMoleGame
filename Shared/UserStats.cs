using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class UserStats
    {
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int ImpostorCount { get; set; }
        public int ImpostorWins { get; set; }
        public int CivilianCount { get; set; }
        public int CivilianWins { get; set; }
        public int TotalVotesCast { get; set; }
        public int CorrectVotes { get; set; }
        public int TimesEjectedAsCivilian { get; set; }
        public int ImpostorRoundsSurvived { get; set; }
        public int FirstRoundEjections { get; set; }
    }
}
