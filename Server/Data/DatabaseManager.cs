using Shared;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Data
{
    public class DatabaseManager
    {
        private const string DbFileName = "TheMoleGame.db";
        private const string ConnectionString = "Data Source=" + DbFileName + ";Version=3;";

        public DatabaseManager()
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(DbFileName))
            {
                Console.WriteLine("Datu-basea ez da existitzen. Sortzen...");
                SQLiteConnection.CreateFile(DbFileName);

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    // 1. Taulak sortu
                    string sql = @"
                        CREATE TABLE IF NOT EXISTS Users (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Username TEXT UNIQUE NOT NULL,
                            PasswordHash TEXT NOT NULL,
                            Role TEXT NOT NULL,
                            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                            IsBanned INTEGER DEFAULT 0
                        );

                        CREATE TABLE IF NOT EXISTS Words (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Category TEXT NOT NULL,
                            WordText TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS Stats (
                            UserId INTEGER PRIMARY KEY, -- Erabiltzaile bakoitzak ilara bakarra
                            GamesPlayed INTEGER DEFAULT 0,
                            GamesWon INTEGER DEFAULT 0,
                            ImpostorCount INTEGER DEFAULT 0, 
                            ImpostorWins INTEGER DEFAULT 0,  
                            CivilianCount INTEGER DEFAULT 0, 
                            CivilianWins INTEGER DEFAULT 0,  
                            FOREIGN KEY(UserId) REFERENCES Users(Id)
                        );
                    ";

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // 2. ADMIN SORTU
                    // Hemen deitzen dugu SecurityHelper.HashPassword
                    string hasheatutakoPasahitza = SecurityHelper.HashPassword("admin123");

                    // MODERATZAILEA SORTU
                    // Ziurtatu lehenik ez dela existitzen
                    string checkMod = "SELECT COUNT(*) FROM Users WHERE Username = 'moderator'";
                    using (var cmd = new SQLiteCommand(checkMod, connection))
                    {
                        long count = (long)cmd.ExecuteScalar();
                        if (count == 0)
                        {
                            string superPass = SecurityHelper.HashPassword("masterkey");
                            // Role zutabea 'Moderator' da
                            string insertSuper = $"INSERT INTO Users (Username, PasswordHash, Role) VALUES ('moderator', '{superPass}', 'Moderator')";
                            using (var cmdInsert = new SQLiteCommand(insertSuper, connection))
                            {
                                cmdInsert.ExecuteNonQuery();
                            }
                            Console.WriteLine("[DB] Moderatzailea sortu da.");
                        }
                    }

                    // KONTUZ HEMEN: 'hasheatutakoPasahitza' aldagaia sartu behar da, EZ 'admin123'
                    string insertAdmin = $"INSERT INTO Users (Username, PasswordHash, Role) VALUES ('admin', '{hasheatutakoPasahitza}', 'Admin')";

                    using (var command = new SQLiteCommand(insertAdmin, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // 3. Hitzak sartu
                    string insertWords = @"INSERT INTO Words (Category, WordText) VALUES 
                        ('Animaliak', 'Katua'), ('Animaliak', 'Txakurra'), ('Animaliak', 'Elefantea'),
                        ('Janaria', 'Pizza'), ('Janaria', 'Hanburgesa'), ('Janaria', 'Sushi'),
                        ('Lekuak', 'Hondartza'), ('Lekuak', 'Mendia'), ('Lekuak', 'Eskola');";

                    using (var command = new SQLiteCommand(insertWords, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    
                }
                Console.WriteLine("Datu-basea ondo sortu da (Hash-arekin)!");
            }
        }

        // Login egiteko metodoa
        public User ValidateUser(string username, string password)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                // Pasahitza Hash bihurtu konparatzeko
                string passwordHash = SecurityHelper.HashPassword(password);

                string sql = "SELECT Id, Username, Role, IsBanned FROM Users WHERE Username = @u AND PasswordHash = @p";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@u", username);
                    command.Parameters.AddWithValue("@p", passwordHash);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bool banned = reader.GetInt32(3) == 1; // 1 bada, True

                            if (banned)
                            {
                                // Trikimailu txikia: User objektu bat itzuli baina "IsBanned" markarekin,
                                // edo null itzuli eta logean jarri.
                                // Hobeto: null itzuli eta Console.WriteLine egin.
                                Console.WriteLine($"[LOGIN DENIED] {username} blokeatuta dago (BANNED).");
                                return null;
                            }

                            // Erabiltzailea existitzen da eta pasahitza zuzena da
                            return new User
                            {
                                Id = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                Role = reader.GetString(2),
                                IsAdmin = reader.GetString(2) == "Admin"
                            };
                        }
                    }
                }
            }
            return null; // Ez da aurkitu edo pasahitza okerra
        }

        public bool RegisterUser(string username, string password)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();

                    // 1. Begiratu ea izena hartuta dagoen
                    string checkSql = "SELECT COUNT(*) FROM Users WHERE Username = @u";
                    using (var cmd = new SQLiteCommand(checkSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        long count = (long)cmd.ExecuteScalar();
                        if (count > 0) return false; // Erabiltzailea existitzen da
                    }

                    // 2. Sortu (Pasahitza HASH eginda!)
                    string passwordHash = SecurityHelper.HashPassword(password);
                    string insertSql = "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@u, @p, 'Player')"; // Defektuz 'Player' rola

                    using (var cmd = new SQLiteCommand(insertSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", passwordHash);
                        cmd.ExecuteNonQuery();
                    }
                    return true; // Ondo sortu da
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Erregistroan: {ex.Message}");
                return false;
            }
        }

        public (string Category, string Word) GetRandomWord()
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                // SQL bat ausazko lerro bat lortzeko
                string sql = "SELECT Category, WordText FROM Words ORDER BY RANDOM() LIMIT 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (reader.GetString(0), reader.GetString(1));
                    }
                }
            }
            return ("Ezezaguna", "???"); // Zerbait gaizki badoa
        }

        // Metodo laguntzailea konexioa lortzeko
        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }


        public bool AddNewWord(string category, string word)
        {
            try
            {
                // 1. Garbitu inputa (espazioak kendu)
                string cleanWord = word.Trim();
                string cleanCategory = category.Trim();

                using (var connection = GetConnection())
                {
                    connection.Open();

                    // 2. EGIAZTAPENA: Begiratu ea existitzen den (LOWER erabiliz)
                    // Honek "Pizza", "pizza", "PIZZA" berdinak direla ziurtatzen du
                    string checkSql = "SELECT COUNT(*) FROM Words WHERE LOWER(WordText) = LOWER(@w)";

                    using (var cmd = new SQLiteCommand(checkSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@w", cleanWord);
                        long count = (long)cmd.ExecuteScalar();

                        if (count > 0)
                        {
                            Console.WriteLine($"[DB] Hitza existitzen da: {cleanWord}");
                            return false; // !!! EXISTITZEN DA, EZ GEHITU !!!
                        }
                    }

                    // 3. GEHITU: Ez bada existitzen, txertatu
                    string sql = "INSERT INTO Words (Category, WordText) VALUES (@c, @w)";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@c", cleanCategory);
                        // Gorde jatorrizko formatuan (Adib: "Pizza" letra larriarekin)
                        cmd.Parameters.AddWithValue("@w", cleanWord);
                        cmd.ExecuteNonQuery();
                    }
                    Console.WriteLine($"[DB] Hitz berria gordeta: {cleanWord}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Hitz berria gehitzean: {ex.Message}");
                return false;
            }
        }


        public List<string> GetCategories()
        {
            List<string> categories = new List<string>();
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    string sql = "SELECT DISTINCT Category FROM Words ORDER BY Category";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categories.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch { }
            return categories;
        }

        public bool SetUserBanStatus(string username, bool isBanned)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE Users SET IsBanned = @b WHERE Username = @u";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@b", isBanned ? 1 : 0);
                        cmd.Parameters.AddWithValue("@u", username);
                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch { return false; }
        }

        // Erabiltzaile guztien zerrenda lortzeko (Admin Panelerako)
        public List<User> GetAllUsers()
        {
            var list = new List<User>();
            using (var conn = GetConnection())
            {
                conn.Open();
                string sql = "SELECT Id, Username, Role, IsBanned FROM Users";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new User
                        {
                            Id = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            Role = reader.GetString(2),
                            // User ereduan "IsBanned" propietatea gehitu beharko dugu Shared-en!
                        });
                    }
                }
            }
            return list;
        }

        public void UpdateStats(int userId, bool isImpostor, bool isWinner)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // 1. Ziurtatu ilara existitzen dela
                    string initSql = "INSERT OR IGNORE INTO Stats (UserId) VALUES (@u)";
                    using (var cmd = new SQLiteCommand(initSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Datuak eguneratu
                    string updateSql = "UPDATE Stats SET GamesPlayed = GamesPlayed + 1";

                    if (isWinner) updateSql += ", GamesWon = GamesWon + 1";

                    if (isImpostor)
                    {
                        updateSql += ", ImpostorCount = ImpostorCount + 1";
                        if (isWinner) updateSql += ", ImpostorWins = ImpostorWins + 1";
                    }
                    else
                    {
                        updateSql += ", CivilianCount = CivilianCount + 1";
                        if (isWinner) updateSql += ", CivilianWins = CivilianWins + 1";
                    }

                    updateSql += " WHERE UserId = @u";

                    using (var cmd = new SQLiteCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("[DB ERROR] Stats update: " + ex.Message); }
        }

        public UserStats GetUserStats(int userId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                string sql = "SELECT * FROM Stats WHERE UserId = @u";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserStats
                            {
                                GamesPlayed = reader.GetInt32(1),
                                GamesWon = reader.GetInt32(2),
                                ImpostorCount = reader.GetInt32(3),
                                ImpostorWins = reader.GetInt32(4),
                                CivilianCount = reader.GetInt32(5),
                                CivilianWins = reader.GetInt32(6)
                            };
                        }
                    }
                }
            }
            return new UserStats(); // Hutsa itzuli ezer ez badago
        }

        // Izenetik IDa lortzeko metodoa
        public int GetUserIdByName(string username)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    string sql = "SELECT Id FROM Users WHERE Username = @u";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@u", username);

                        // ExecuteScalar-ek lehenengo zutabea itzultzen du (Id)
                        object result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] GetUserIdByName: {ex.Message}");
            }

            return 0; // Ez bada aurkitzen
        }
    } 
}