using Server.Data;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Services
{
    public class AuthService : IAuthService
    {
        private readonly DatabaseManager _dbManager;

        public AuthService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public User ValidateUser(string username, string password)
        {
            return _dbManager.ValidateUser(username, password);
        }

        public bool RegisterUser(string username, string password, string role)
        {
            return _dbManager.CreateUserWithRole(username, password, role);
        }
    }
}
