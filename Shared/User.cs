using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    [Serializable]
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        // Pasahitza EZ dugu inoiz bidaltzen saretik objektu honetan, segurtasunagatik.
        public bool IsAdmin { get; set; } // Rola kudeatzeko

        // Jokoan erabiltzeko (ez da DBn gordetzen, memorian bakarrik)
        public int Score { get; set; }
    }
}
