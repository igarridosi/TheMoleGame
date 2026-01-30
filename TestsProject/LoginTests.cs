using Moq;
using Server.Services;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestsProject
{
    public class LoginTests
    {
        private readonly Mock<IAuthService> _mockAuthService;

        public LoginTests()
        {
            // Simulazioa sortu
            _mockAuthService = new Mock<IAuthService>();
        }

        [Fact] // 1. TESTA: Sarrera hutsik (Login okerra)
        public void Login_EmptyCredentials_ReturnsNull()
        {
            // Arrange (Prestatu)
            string user = "";
            string pass = "";

            // Konfiguratu Mock-a: Hutsik badago, null itzuli behar du
            _mockAuthService.Setup(x => x.ValidateUser("", "")).Returns((User)null);

            // Act (Exekutatu)
            var result = _mockAuthService.Object.ValidateUser(user, pass);

            // Assert (Egiaztatu)
            Assert.Null(result); // Null izan behar du
        }

        [Fact] // 2. TESTA: Kredentzial okerrak
        public void Login_WrongCredentials_ReturnsNull()
        {
            // Arrange
            string user = "pepe";
            string pass = "gaizki";
            _mockAuthService.Setup(x => x.ValidateUser(user, pass)).Returns((User)null);

            // Act
            var result = _mockAuthService.Object.ValidateUser(user, pass);

            // Assert
            Assert.Null(result);
        }

        [Fact] // 3. TESTA: Erabiltzaile normala (Login zuzena)
        public void Login_CorrectUser_ReturnsUserObject()
        {
            // Arrange
            string user = "ibai";
            string pass = "1234";
            var expectedUser = new User { Username = "ibai", Role = "Player" };

            _mockAuthService.Setup(x => x.ValidateUser(user, pass)).Returns(expectedUser);

            // Act
            var result = _mockAuthService.Object.ValidateUser(user, pass);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ibai", result.Username);
            Assert.Equal("Player", result.Role);
        }

        [Fact] // 4. TESTA: Admin logina
        public void Login_AdminUser_ReturnsAdminRole()
        {
            // Arrange
            var expectedAdmin = new User { Username = "admin", Role = "Admin", IsAdmin = true };
            _mockAuthService.Setup(x => x.ValidateUser("admin", "admin123")).Returns(expectedAdmin);

            // Act
            var result = _mockAuthService.Object.ValidateUser("admin", "admin123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Admin", result.Role);
            Assert.True(result.IsAdmin);
        }
    }
}
