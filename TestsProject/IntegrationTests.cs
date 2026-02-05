using Server.Data;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestsProject
{
    // [Trait] etiketa erabiltzen dugu gero iragazteko ("dotnet test --filter Type=Integration")
    [Trait("Type", "Integration")]
    public class IntegrationTests
    {
        private const string TestDbName = "Test_TheMoleGame.db";

        public IntegrationTests()
        {
            // KONSTRUKTOREA: Test bakoitzaren aurretik exekutatzen da
            // Ziurtatu DB garbi bat daukagula
            if (File.Exists(TestDbName)) File.Delete(TestDbName);
        }

        // Helper: DB Managerra sortu test fitxategiarekin
        private DatabaseManager CreateTestDatabase()
        {
            var db = new DatabaseManager();
            // (Hemen zure kodeak "TheMoleGame.db" sortuko du automatikoki)
            return db;
        }

        [Fact]
        public void Integration_Login_ValidUser_ReturnsTrue()
        {
            // 1. ARRANGE (Prestatu)
            // Test DB sortu (zure InitializeDatabase metodoak egingo du)
            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db"); // Garbitu

            var db = new DatabaseManager(); // Honek admin/admin123 sortzen du defektuz

            // Erabiltzaile berri bat sortu probatzeko
            db.CreateUserWithRole("testuser", "pass123", "Player");

            // 2. ACT (Exekutatu)
            User user = db.ValidateUser("testuser", "pass123");

            // 3. ASSERT (Egiaztatu)
            Assert.NotNull(user);
            Assert.Equal("testuser", user.Username);
            Assert.Equal("Player", user.Role);

            // Garbitu
            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db");
        }

        [Fact]
        public void Integration_Login_InvalidPassword_ReturnsNull()
        {
            // 1. ARRANGE
            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db");
            var db = new DatabaseManager();
            db.CreateUserWithRole("testuser", "pass123", "Player");

            // 2. ACT (Pasahitz okerra)
            User user = db.ValidateUser("testuser", "wrongpass");

            // 3. ASSERT
            Assert.Null(user);

            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db");
        }

        [Fact]
        public void Integration_Register_DuplicateUser_ReturnsFalse()
        {
            // 1. ARRANGE
            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db");
            var db = new DatabaseManager();

            // Lehenengo aldia: Ondo
            bool first = db.CreateUserWithRole("errepikatua", "123", "Player");

            // 2. ACT (Bigarren aldia izen berdinarekin)
            bool second = db.CreateUserWithRole("errepikatua", "456", "Player");

            // 3. ASSERT
            Assert.True(first);  // Sortu da
            Assert.False(second); // Ez da sortu (izena okupatuta)

            if (File.Exists("TheMoleGame.db")) File.Delete("TheMoleGame.db");
        }
    }
}
