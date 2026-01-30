using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Services
{
    public interface IAuthService
    {
        // Erabiltzailea balioztatu (Login)
        User ValidateUser(string username, string password);

        // Erregistratu
        bool RegisterUser(string username, string password, string role);
    }
}
