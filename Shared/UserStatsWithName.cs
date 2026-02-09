using System;

namespace Shared
{
    [Serializable]
    public class UserStatsWithName
    {
        public string Username { get; set; }
        public UserStats Stats { get; set; }
    }
}
