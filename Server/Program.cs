using Server;
using Server.Data;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

class Program
{
    // Sinkronizaziorako sarraila
    private static readonly object _turnLock = new object();

    private static TcpListener _listener;
    private static bool _isRunning = false;
    private static DatabaseManager _dbManager;
    private static ConcurrentDictionary<int, StreamWriter> _clients = new ConcurrentDictionary<int, StreamWriter>();
    private static ConcurrentDictionary<int, string> _clientNames = new ConcurrentDictionary<int, string>();
    
    // Txanden kudeaketa
    private static List<int> _turnOrder = new List<int>(); // Jokalarien ID zerrenda ordenatua
    private static int _currentTurnIndex = 0; // Zenbatgarren jokalaria den

    // Bozketa eta Ronda kudeaketa
    private static int _roundCount = 1;         // Uneko ronda (1, 2 edo 3)
    private static int _maxRounds = 3;      // Gehienezko rondak
    private static ConcurrentDictionary<string, int> _votes = new ConcurrentDictionary<string, int>(); // Nor -> Zenbat boto
    private static int _playersVotedCount = 0;  // Zenbatek bozkatu dute?
    private static bool _isVotingPhase = false; // Fasea kontrolatzeko
    private static int _impostorId = -1;        // Inpostorea nor den jakiteko (StartGameLogic-en beteko dugu)
    private static HashSet<int> _playersWhoVoted = new HashSet<int>(); // Nork bozkatu du ronda honetan? (Boto bikoitzak ekiditeko)

    private static List<int> _eliminatedPlayers = new List<int>();

    // Jokoaren egoera gordetzeko (Nork zer esan duen)
    // Key: Username, Value: Esandako hitza
    private static ConcurrentDictionary<string, string> _gameWords = new ConcurrentDictionary<string, string>();

    // Noren txanda da? (Username)
    private static string _currentTurnUser = "";

    // Rolak
    private static ConcurrentDictionary<int, string> _clientRoles = new ConcurrentDictionary<int, string>();

    // Timer-a kontrolatzeko (Gelditu ahal izateko)
    private static CancellationTokenSource _timerCts;

    // Gela guztien hiztegia (Kodea -> Gela)
    private static ConcurrentDictionary<string, GameRoom> _activeRooms = new ConcurrentDictionary<string, GameRoom>();

    // Bezero bakoitza zein gelatan dagoen
    private static ConcurrentDictionary<int, string> _clientRoomMap = new ConcurrentDictionary<int, string>();

    private static ConcurrentDictionary<int, string> _tempClientNames = new ConcurrentDictionary<int, string>();

    static void Main(string[] args)
    {
        SQLitePCL.Batteries.Init();

        Console.Title = "The Mole Game - Server";

        // 1. Datu-basea hasieratu
        _dbManager = new DatabaseManager();
        Console.WriteLine("[DB] Datu-basea prest.");

        // 2. Zerbitzaria martxan jarri
        StartServer(8080); // 8080 portuan entzungo du
    }

    private static void StartServer(int port)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"[SERVER] Zerbitzaria entzuten portuan: {port}");
            Console.WriteLine("[SERVER] Bezeroen zain...");

            // Begizta infinitua konexioak onartzeko
            while (_isRunning)
            {
                // Honek programa gelditzen du bezero bat konektatu arte (Blocking)
                TcpClient client = _listener.AcceptTcpClient();

                Console.WriteLine("[SERVER] Bezero berri bat konektatu da!");

                // Hari (Thread) berri bat sortu bezero hau kudeatzeko
                // Horrela zerbitzariak beste bezero batzuk onartzen jarraitu dezake
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                clientThread.Start(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Zerbitzariaren errorea: {ex.Message}");
        }
    }

    // Metodo hau hari bereizi batean exekutatuko da bezero bakoitzarentzat
    private static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream);
        StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

        int clientId = Thread.CurrentThread.ManagedThreadId;
        Console.WriteLine($"[SERVER] Konexio berria ID: {clientId}");

        try
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Packet packet = PacketSerializer.Deserialize(line);

                // 1. ESTRATEGIA: Begiratu ea bezeroa jada GELA batean dagoen
                if (_clientRoomMap.TryGetValue(clientId, out string roomCode))
                {
                    if (_activeRooms.TryGetValue(roomCode, out GameRoom room))
                    {
                        if (packet.Type == PacketType.LeaveRoomRequest)
                        {
                            Console.WriteLine($"[ROOM] {clientId} gelatik atera da (Exit botoia).");

                            // ALDAKETA: Zerrenda jaso eta prozesatu
                            var kickedIds = room.RemovePlayer(clientId);

                            // Ni mapatik kendu
                            _clientRoomMap.TryRemove(clientId, out _);

                            // Besteak (Host bada) mapatik kendu
                            foreach (int id in kickedIds)
                            {
                                _clientRoomMap.TryRemove(id, out _);
                            }

                            // Gela hutsik badago, ezabatu
                            if (room.PlayerCount == 0)
                            {
                                _activeRooms.TryRemove(roomCode, out _);
                                Console.WriteLine($"[ROOM] Gela ezabatu da: {roomCode}");
                            }
                        }
                        else
                        {
                            // Mezu normala bada, gelara bideratu
                            room.HandlePacket(clientId, packet);
                        }
                        // ------------------------
                    }
                    else
                    {
                        // Gela ez bada existitzen (errorea)
                        _clientRoomMap.TryRemove(clientId, out _);
                    }
                }
                else
                {
                    // 2. ESTRATEGIA: Ez dago gelan (Menuan edo Loginan dago)
                    // Hemen kudeatzen dira: Login, Register, CreateRoom, JoinRoom, Ranking, Stats...

                    switch (packet.Type)
                    {
                        // --- KONTUAK ---
                        case PacketType.LoginRequest:
                            var loginReq = PacketSerializer.DeserializeData<LoginRequest>(packet.Message);
                            User user = _dbManager.ValidateUser(loginReq.Username, loginReq.Password);

                            if (user != null)
                            {
                                // Gorde izena aldi baterako (Gela sortu/sartu arte)
                                _tempClientNames.TryAdd(clientId, user.Username);

                                writer.WriteLine(PacketSerializer.Serialize(new Packet
                                {
                                    Type = PacketType.LoginResponse,
                                    Message = PacketSerializer.SerializeData(user)
                                }));
                                Console.WriteLine($"[LOGIN] Onartua: {user.Username}");
                            }
                            else
                            {
                                writer.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.LoginResponse, Message = null }));
                            }
                            break;

                        case PacketType.RegisterRequest:
                            var regReq = PacketSerializer.DeserializeData<RegisterRequest>(packet.Message);
                            // Hemen defektuz 'Player' jartzen dugu
                            bool regOk = _dbManager.CreateUserWithRole(regReq.Username, regReq.Password, "Player");
                            writer.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.RegisterResponse,
                                Message = regOk ? "OK" : "ERROR"
                            }));
                            break;

                        // --- GELA KUDEAKETA ---
                        case PacketType.CreateRoomRequest:
                            string newCode = GenerateRoomCode();
                            string hostName = _tempClientNames.ContainsKey(clientId) ? _tempClientNames[clientId] : "Host";

                            // Sortu gela berria
                            GameRoom newRoom = new GameRoom(newCode, clientId, writer, hostName);

                            _activeRooms.TryAdd(newCode, newRoom);
                            _clientRoomMap.TryAdd(clientId, newCode); // Lotu bezeroa gelarekin

                            Console.WriteLine($"[ROOM] Gela sortu da: {newCode} (Host: {hostName})");

                            writer.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.CreateRoomResponse,
                                Message = newCode
                            }));
                            break;

                        case PacketType.JoinRoomRequest:
                            string codeToJoin = packet.Message.ToUpper();
                            // Izen hau lortzea oso garrantzitsua da!
                            string joinerName = _tempClientNames.ContainsKey(clientId) ? _tempClientNames[clientId] : "Player";

                            if (_activeRooms.TryGetValue(codeToJoin, out GameRoom roomToJoin))
                            {
                                roomToJoin.AddPlayer(clientId, writer, joinerName);
                                _clientRoomMap.TryAdd(clientId, codeToJoin);

                                // GARRANTZITSUA: Erantzuna bidali
                                writer.WriteLine(PacketSerializer.Serialize(new Packet
                                {
                                    Type = PacketType.JoinRoomResponse,
                                    Message = "OK"
                                }));
                            }
                            else
                            {
                                // Gela ez da existitzen
                                writer.WriteLine(PacketSerializer.Serialize(new Packet
                                {
                                    Type = PacketType.JoinRoomResponse,
                                    Message = "Ez da gela aurkitu"
                                }));
                            }
                            break;

                        // --- DATU GLOBALAK (Ranking, Stats, Admin Users) ---
                        // Hauek ez dute gela behar, DBtik irakurtzen dira zuzenean

                        case PacketType.GetRankingRequest:
                            var ranking = _dbManager.GetGlobalRanking();
                            var gStats = _dbManager.GetGlobalStats();
                            writer.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.GetRankingResponse,
                                Message = PacketSerializer.SerializeData(new RankingPayload { List = ranking, Stats = gStats })
                            }));
                            break;

                        case PacketType.GetStatsRequest:
                            try
                            {
                                string myName = _tempClientNames.ContainsKey(clientId) ? _tempClientNames[clientId] : "";
                                int myDbId = _dbManager.GetUserIdByName(myName);
                                var stats = _dbManager.GetUserStats(myDbId);
                                writer.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.GetStatsResponse, Message = PacketSerializer.SerializeData(stats) }));
                            }
                            catch { }
                            break;

                        case PacketType.GetRoomsRequest:
                            // Formatua: "KODEA|HOST|COUNT"
                            List<string> roomDetails = new List<string>();

                            foreach (var kvp in _activeRooms)
                            {
                                string code = kvp.Key;
                                GameRoom room = kvp.Value;

                                // Izenak lortu
                                string roomHostName = "Unknown";

                                string info = $"{code}|{room.roomHostName}|{room.PlayerCount}";
                                roomDetails.Add(info);
                            }

                            writer.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.GetRoomsResponse,
                                Message = PacketSerializer.SerializeData(roomDetails)
                            }));
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {clientId} deskonektatu da: {ex.Message}");
        }
        finally
        {
            // DESKONEXIOA GARBITU

            // GARBIKETA
            if (_clientRoomMap.TryGetValue(clientId, out string code))
            {
                if (_activeRooms.TryGetValue(code, out GameRoom room))
                {
                    // ALDAKETA: Zerrenda jaso eta prozesatu
                    var kickedIds = room.RemovePlayer(clientId);

                    // Besteak mapatik kendu (Host bazen)
                    foreach (int id in kickedIds)
                    {
                        _clientRoomMap.TryRemove(id, out _);
                    }

                    if (room.PlayerCount == 0)
                    {
                        _activeRooms.TryRemove(code, out _);
                    }
                }
                _clientRoomMap.TryRemove(clientId, out _);
            }

            _tempClientNames.TryRemove(clientId, out _);
            client.Close();
            Console.WriteLine("[THREAD] Bezeroa deskonektatu da.");
        }
    
}

    // Mezu bat denei bidaltzeko metodoa
    public static void BroadcastPacket(Packet packet)
    {
        string json = PacketSerializer.Serialize(packet);

        foreach (var clientWriter in _clients.Values)
        {
            try
            {
                clientWriter.WriteLine(json);
            }
            catch
            {
                // Bezero hau agian deskonektatu da bidaltzen ari ginen bitartean
            }
        }
    }

    // Zerrenda osatu eta bidali
    public static void BroadcastPlayerList()
    {
        List<PlayerState> list = new List<PlayerState>();

        foreach (var entry in _clientNames)
        {
            int id = entry.Key;
            string name = entry.Value;

            // 1. IRAGAZKI BERRIA: Moderatzailea bada, EZ gehitu zerrendara
            if (_clientRoles.ContainsKey(id) && _clientRoles[id] == "Moderator")
            {
                continue; // Saltatu hurrengora
            }

            // Begiratu ea ID hori kanporatuen zerrendan dagoen
            bool isEliminated = _eliminatedPlayers.Contains(id);

            list.Add(new PlayerState
            {
                Username = name,
                SubmittedWord = _gameWords.ContainsKey(name) ? _gameWords[name] : "",
                IsTurn = (name == _currentTurnUser),
                IsEliminated = isEliminated, // <--- HEMEN EGIAZTATU
                                             // Kanporatua badago, EZIN du bozkatu
                IsVotingPhase = _isVotingPhase && !isEliminated
            });
        }

        Packet p = new Packet
        {
            Type = PacketType.PlayerList,
            Message = PacketSerializer.SerializeData(list)
        };
        BroadcastPacket(p);
    }

    private static void HandleLogin(string jsonMessage, StreamWriter writer)
    {
        // Login datuak atera
        var loginReq = PacketSerializer.DeserializeData<LoginRequest>(jsonMessage);
        Console.WriteLine($"[LOGIN] Saiakera: {loginReq.Username}");

        // DBn begiratu
        User user = _dbManager.ValidateUser(loginReq.Username, loginReq.Password);

        Packet responsePacket = new Packet();

        if (user != null)
        {
            // ONGI: Login zuzena
            responsePacket.Type = PacketType.LoginResponse;
            responsePacket.Message = PacketSerializer.SerializeData(user); // Erabiltzailearen datuak itzuli
            Console.WriteLine($"[LOGIN] ONARTUA: {user.Username}");
        }
        else
        {
            // GAIZKI: Login okerra
            responsePacket.Type = PacketType.LoginResponse;
            responsePacket.Message = null; // Edo errore mezu bat
            Console.WriteLine($"[LOGIN] UKATUA: {loginReq.Username}");
        }

        // Erantzuna bidali bezeroari
        writer.WriteLine(PacketSerializer.Serialize(responsePacket));
    }

    private static void HandleRegister(string jsonMessage, StreamWriter writer)
    {
        var regReq = PacketSerializer.DeserializeData<RegisterRequest>(jsonMessage);
        Console.WriteLine($"[REGISTER] Saiakera: {regReq.Username}");

        bool success = _dbManager.RegisterUser(regReq.Username, regReq.Password);

        Packet response = new Packet
        {
            Type = PacketType.RegisterResponse,
            Message = success ? "OK" : "FAIL" // Sinplea: OK edo FAIL testua
        };

        writer.WriteLine(PacketSerializer.Serialize(response));
        Console.WriteLine($"[REGISTER] Emaitza: {(success ? "Sortua" : "Errorea/Existitzen da")}");
    }

    private static void StartGameLogic()
    {
        // 1. HASIERATZEAK
        _roundCount = 1;
        _isVotingPhase = false;
        _currentTurnIndex = 0;
        _gameWords.Clear();
        _votes.Clear();
        _eliminatedPlayers.Clear();
        _playersWhoVoted.Clear(); // Hau ere garbitu

        // 2. JOKALARIAK IRAGAZI (Moderatzaileak kendu joko-listatik)
        // 'playingClients' dira bakarrik jolastuko dutenak (Botatu beharrekoak)
        var playingClients = _clients.Keys.Where(id =>
                _clientNames.ContainsKey(id) &&  // <--- KONPONKETA: Logeatuta egon behar du!
                (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
            ).ToList();

        int activePlayerCount = playingClients.Count;

        // 3. BALIDAZIOA: Minimo 3 jokalari ERREAL (Moderatzailea kontatu gabe)
        if (activePlayerCount < 3)
        {
            string msg = $"[SISTEMA] Gutxienez 3 jokalari behar dira partida hasteko. (Orain daudenak: {activePlayerCount})";
            Console.WriteLine($"[GAME ERROR] {msg}");

            Packet err = new Packet { Type = PacketType.ChatMessage, Message = msg };
            BroadcastPacket(err);

            return;
        }

        // 4. RONDA KOPURUA KALKULATU
        if (activePlayerCount == 3) _maxRounds = 1;
        else if (activePlayerCount <= 5) _maxRounds = 2;
        else _maxRounds = 3;

        Console.WriteLine($"[GAME CONFIG] Jokalariak: {activePlayerCount} -> Rondak: {_maxRounds}");

        // 5. Hitz bat aukeratu DBtik
        var randomWordData = _dbManager.GetRandomWord();
        Console.WriteLine($"[GAME] Hitza aukeratuta: {randomWordData.Word} ({randomWordData.Category})");

        // 6. Inpostorea aukeratu (Bakarrik JOKALARIEN artean)
        Random rnd = new Random();
        int impostorIndex = rnd.Next(playingClients.Count);
        _impostorId = playingClients[impostorIndex]; // Gorde ID globala

        // Inpostorearen izena lortu (Moderatzaileari esateko)
        string impostorName = _clientNames.ContainsKey(_impostorId) ? _clientNames[_impostorId] : "Ezezaguna";
        Console.WriteLine($"[GAME] Inpostorea ID: {_impostorId} ({impostorName})");

        // 7. MEZUAK PRESTATU ETA BIDALI (Denei, Moderatzailea barne)
        foreach (var clientId in _clients.Keys)
        {
            if (!_clients.TryGetValue(clientId, out StreamWriter writer)) continue;

            GameInfo info = new GameInfo();
            string role = _clientRoles.ContainsKey(clientId) ? _clientRoles[clientId] : "Player";

            // A) MODERATZAILEA BADA -> GOD MODE (Dena daki)
            if (role == "Moderator")
            {
                info.IsImpostor = false;
                info.Category = randomWordData.Category;
                // Sekretua erakutsi: Hitza + Nor den inpostorea
                info.Word = $"{randomWordData.Word} || INPOSTOREA: {impostorName}";
            }
            // B) INPOSTOREA BADA -> Ez du hitza ikusten
            else if (clientId == _impostorId)
            {
                info.IsImpostor = true;
                info.Category = randomWordData.Category;
                info.Word = "???";
            }
            // C) HERRITARRA BADA -> Hitza ikusten du
            else
            {
                info.IsImpostor = false;
                info.Category = randomWordData.Category;
                info.Word = randomWordData.Word;
            }

            // Bidali
            try
            {
                Packet packet = new Packet
                {
                    Type = PacketType.GameInfo,
                    Message = PacketSerializer.SerializeData(info)
                };
                writer.WriteLine(PacketSerializer.Serialize(packet));
            }
            catch { }
        }

        // 8. TXANDEN ORDENA (Bakarrik jokalariak)
        _turnOrder = playingClients; // Moderatzailea ez da hemen sartzen
        _currentTurnIndex = 0;

        // UI EGUNERATU
        SendRoundUpdate();

        // Mezua zabaldu
        Packet startMsg = new Packet { Type = PacketType.GameStart, Message = "Partida hasi da!" };
        BroadcastPacket(startMsg);

        // 9. HASI LEHENENGO TXANDA
        NextTurn();
    }

    // METODO LAGUNTZAILEA
    private static void SendRoundUpdate()
    {
        var info = new RoundInfo { CurrentRound = _roundCount, TotalRounds = _maxRounds };
        Packet p = new Packet
        {
            Type = PacketType.RoundUpdate, // Ziurtatu PacketType-n gehitu duzula!
            Message = PacketSerializer.SerializeData(info)
        };
        BroadcastPacket(p);
    }

    private static void NextTurn()
    {
        // Begiratu ea jokalari guztiek hitz egin duten
        if (_currentTurnIndex >= _turnOrder.Count)
        {
            _currentTurnUser = ""; // Garbitu "active player"
            StartVotingPhase();    // Joan bozketara
            return;
        }

        // ORAINGO TXANDA PRESTATU
        int currentClientId = _turnOrder[_currentTurnIndex];

        // UI Eguneratu (Arkatza jarri)
        if (_clientNames.TryGetValue(currentClientId, out string username))
        {
            _currentTurnUser = username;
            BroadcastPlayerList();
        }

        // Pop-up leihoa bidali bezeroari
        if (_clients.TryGetValue(currentClientId, out StreamWriter writer))
        {
            try
            {
                writer.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.YourTurn }));
            }
            catch { }
        }

        Console.WriteLine($"[GAME] Txanda: {_currentTurnUser} (Index: {_currentTurnIndex})");

        // TIMERRA HASI (20 segundu)
        StartTimer(20, () =>
        {
            // Hau exekutatzen da denbora agortzen bada
            lock (_turnLock)
            {
                // Segurtasuna: Ziurtatu oraindik indize bera dela
                // (Batzuetan Timerra saltatzen da jokalariak justu idatzi duenean)
                if (_currentTurnIndex >= _turnOrder.Count) return;

                Console.WriteLine($"[TIMER] {_currentTurnUser}-k denbora agortu du.");

                // --- HEMEN DAGO GAKOA ---
                // "Hutsa" idaztea hitz normal bat bezala tratatu behar dugu.

                if (_clientNames.ContainsKey(currentClientId))
                {
                    string name = _clientNames[currentClientId];
                    // GORDE HITZA (Honek ziurtatzen du "idatzi" duela kontatzen duela)
                    _gameWords.AddOrUpdate(name, "Hutsa (Time)", (k, v) => "Hutsa (Time)");

                    Packet msg = new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {name}-k ez du garaiz idatzi." };
                    BroadcastPacket(msg);
                }

                // INDIZEA IGO (Hurrengora pasatzeko)
                _currentTurnIndex++;

                // DEITU NEXTTURN (Berak ikusiko du ea hurrengo jokalaria den edo bozketa den)
                NextTurn();
            }
        });
    }

    private static void StartVotingPhase()
    {
        _isVotingPhase = true;
        _playersVotedCount = 0;
        _votes.Clear(); // Botoak garbitu
        _playersWhoVoted.Clear();

        Console.WriteLine($"[GAME] {_roundCount}. Ronda amaitu da. BOZKETA HASI DA.");

        // 2. MEZUA BIDALI
        Packet msg = new Packet { Type = PacketType.ChatMessage, Message = "[SISTEMA] Ronda amaitu da! Egin klik jokalari baten gainean botatzeko." };
        BroadcastPacket(msg);

        // 3. ZERRENDA EGUNERATU (Orain _isVotingPhase = true denez, botoiak agertuko dira)
        BroadcastPlayerList();  // <--- ETA GERO HAU!

        Console.WriteLine($"[GAME] 60 segundu bozkatzeko...");

        // --- TIMERRA HASI (60s) ---
        StartTimer(60, () =>
        {
            Console.WriteLine("[TIMER] Bozketa denbora agortu da!");

            // DENBORA AGORTU BADA:
            // Bilatu nori falta zaion bozkatzea eta KANPORATU
            // (Bakarrik bizirik daudenak eta bozkatu ez dutenak)

            var nonVoters = _clients.Keys
                .Where(id =>
                    !_eliminatedPlayers.Contains(id) && // Bizirik
                    !_playersWhoVoted.Contains(id) &&   // Ez du bozkatu
                    (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator") // Ez da mod
                ).ToList();

            if (nonVoters.Count > 0)
            {
                foreach (var id in nonVoters)
                {
                    _eliminatedPlayers.Add(id);
                    string name = _clientNames.ContainsKey(id) ? _clientNames[id] : "Ezezaguna";
                    Console.WriteLine($"[AFK] {name} kanporatua bozkatu ez duelako.");

                    Packet msg = new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {name} kanporatua izan da denboraz kanpo bozkatzeagatik." };
                    BroadcastPacket(msg);
                }
            }

            // Kontatu bizirik daudenak (Moderatzaileak kenduta)
            int survivors = _clients.Keys.Count(id =>
                !_eliminatedPlayers.Contains(id) &&
                (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
            );

            if (survivors < 3) // Edo < 2, baina inpostore jokoetan 3 da minimo logikoa
            {
                Console.WriteLine("[GAME END] Jokalari gutxiegi.");
                EndGame("PARTIDA BERTAN BEHERA (Jokalari gutxiegi)");
                return; // Garrantzitsua
            }

            // Emaitzak prozesatu (dauden botoekin)
            ProcessVotingResults();
        });
    }

    private static void ProcessVotingResults()
    {
        _isVotingPhase = false;
        Console.WriteLine("[GAME] Botoak zenbatzen...");

        // 1. Bilatu boto gehien dituena
        string mostVotedUser = null;
        int maxVotes = 0;
        bool isTie = false;

        foreach (var entry in _votes)
        {
            if (entry.Value > maxVotes)
            {
                maxVotes = entry.Value;
                mostVotedUser = entry.Key;
                isTie = false;
            }
            else if (entry.Value == maxVotes)
            {
                isTie = true;
            }
        }

        // --- DEBUG EGITEKO (KONTSOLAN IKUSTEKO ZER GERTATZEN DEN) ---
        if (_clientNames.TryGetValue(_impostorId, out string realImpostorName))
        {
            Console.WriteLine($"[DEBUG] Bozkatuena: '{mostVotedUser}' | Benetako Inpostorea: '{realImpostorName}'");
        }
        else
        {
            Console.WriteLine("[ERROR] Ezin da inpostorearen izena aurkitu ID-tik abiatuta!");
        }
        // -------------------------------------------------------------

        // 2. Erabakiak hartu
        if (mostVotedUser != null && !isTie)
        {
            // NORBAIT KANPORATU DUTE
            // ID-a bilatu izenetik abiatuta
            int kickedId = _clientNames.FirstOrDefault(x => x.Value == mostVotedUser).Key;
            if (kickedId != 0)
            {
                _eliminatedPlayers.Add(kickedId);
            }

            Packet msg = new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] Bozketa amaitu da. {mostVotedUser} kanporatua izan da!" };
            BroadcastPacket(msg);

            // --- ALDAKETA NAGUSIA HEMEN ---
            // Izenak konparatzean, ziurtatu biak existitzen direla
            if (!string.IsNullOrEmpty(realImpostorName) && mostVotedUser == realImpostorName)
            {
                // INPOSTOREA HARRAPATUA -> HERRITARREK IRABAZI
                Console.WriteLine("[WIN] Inpostorea harrapatu dute! Herritarrek irabazi."); // Debug
                EndGame("HERRITARREK");
                return; // <--- GARRANTZITSUA: Atera hemendik
            }
            else
            {
                Console.WriteLine("[GAME] Kanporatua ez zen inpostorea. Jokoak jarraitzen du.");
            }
            // BALIDAZIO BERRIA HEMEN ERE:
            int survivors = _clients.Keys.Count(id =>
               !_eliminatedPlayers.Contains(id) &&
               (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
            );

            if (survivors < 3)
            {
                // Inpostoreak irabazi duela esan dezakegu, edo bertan behera utzi.
                // Normalean: 2 geratzen badira eta inpostorea bizirik badago -> Inpostoreak irabazi du.
                Console.WriteLine("[WIN] Inpostorea irabazi (Jokalariak < 3)");
                EndGame("INPOSTOREA");
                return;
            }
        }
        else
        {
            // BERDINKETA
            Packet msg = new Packet
            {
                Type = PacketType.ChatMessage,
                Message = "[SISTEMA] BERDINKETA! Ez da inor kanporatu. Ronda errepikatuko da."
            };
            BroadcastPacket(msg);

            // Ronda errepikatu
            _gameWords.Clear();
            _currentTurnIndex = 0;
            _turnOrder = _clients.Keys.Where(id => !_eliminatedPlayers.Contains(id)).ToList();
            BroadcastPlayerList();
            NextTurn();
            return;
        }

        // 3. JOKOA JARRAITU EDO AMAITU
        // Inpostorea ez bada harrapatu eta rondak amaitu badira -> Inpostoreak irabazi
        if (_roundCount >= _maxRounds)
        {
            Console.WriteLine("[WIN] Rondak amaitu dira. Inpostoreak irabazi."); // Debug
            EndGame("INPOSTOREAK");
        }
        else
        {
            _roundCount++;
            SendRoundUpdate();
            StartNextRound();
        }
    }

    private static void EndGame(string winner)
    {
        Packet p = new Packet { Type = PacketType.GameEnd, Message = winner };
        BroadcastPacket(p);

        // Begiratu ea mezuan "BERTAN BEHERA" edo "gutxiegi" jartzen duen.
        // Hala bada, ez gorde ezer eta atera metodotik.
        if (winner.Contains("BERTAN BEHERA") || winner.Contains("gutxiegi"))
        {
            Console.WriteLine("[STATS] Partida baliogabea (Jokalari gutxiegi). Ez da ezer gordeko.");

            // Garbiketa orokorra egin hurrengo partidarako
            _gameWords.Clear();
            _votes.Clear();
            _eliminatedPlayers.Clear();
            _playersWhoVoted.Clear();
            _roundCount = 1;

            return; // <--- GARRANTZITSUA: ATERA HEMENDIK!
        }

        bool impostorWon = (winner.Contains("INPOSTOREA"));

        Console.WriteLine("[STATS] Estatistikak gordetzen...");

        foreach (var clientId in _clientNames.Keys)
        {
            // 1. Izenak lortu
            string username = _clientNames[clientId];

            // Moderatzaileak ez du estatistikarik
            if (_clientRoles.ContainsKey(clientId) && _clientRoles[clientId] == "Moderator") continue;

            // 2. IDa lortu DBtik
            int dbId = _dbManager.GetUserIdByName(username);

            if (dbId > 0)
            {
                // 3. Emaitza kalkulatu
                bool isThisUserImpostor = (clientId == _impostorId);
                bool isThisUserWinner = (isThisUserImpostor && impostorWon) || (!isThisUserImpostor && !impostorWon);

                // 4. Gorde
                _dbManager.UpdateStats(dbId, isThisUserImpostor, isThisUserWinner);
                Console.WriteLine($"[STATS] {username} eguneratuta. (Winner: {isThisUserWinner})");
            }
            else
            {
                Console.WriteLine($"[STATS ERROR] Ez da IDrik aurkitu {username}-rentzat.");
            }
        }
        // Reset logika...
    }

    private static void StartNextRound()
    {
        // Hitzak garbitu ronda berrirako
        _gameWords.Clear();
        _currentTurnIndex = 0;

        Packet msg = new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {_roundCount}. Ronda hasten da! Hitza berriro idatzi behar duzue." };
        BroadcastPacket(msg);

        // Bakarrik kanporatu GABEKOAK sartu txandan
        _turnOrder = _clients.Keys.Where(id => !_eliminatedPlayers.Contains(id)).ToList();

        // Turnoak berriro hasi
        BroadcastPlayerList();

        if (_turnOrder.Count > 0)
        {
            NextTurn();
        }
        else
        {
            EndGame("HERRITARREK"); // Inpostorea bakarrik geratu bada agian logika hau landu behar da
        }
    }

    private static void ResetGame()
    {
        Console.WriteLine("[SERVER] Partida berrabiarazten...");

        // 1. Aldagai guztiak reset
        _roundCount = 1;
        _eliminatedPlayers.Clear();
        _gameWords.Clear();
        _votes.Clear();
        _isVotingPhase = false;

        // 2. Bezeroei abisatu (Gonbidapena)
        Packet p = new Packet { Type = PacketType.RestartGameInvite };
        BroadcastPacket(p);

        // Oharra: Hemen ez dugu "StartGameLogic" zuzenean deitzen.
        // Bezeroek onartu ahala UI garbituko dute, eta gero Adminak "HASI PARTIDA" 
        // emango du berriro jendea prest dagoenean.
    }

    private static async void StartTimer(int seconds, Action onTimeout)
    {
        // Aurreko timerra badago, bertan behera utzi
        if (_timerCts != null) _timerCts.Cancel();

        _timerCts = new CancellationTokenSource();
        CancellationToken token = _timerCts.Token;

        try
        {
            for (int i = seconds; i > 0; i--)
            {
                // 1. Eguneratu bezeroak
                Packet p = new Packet { Type = PacketType.TimeUpdate, Message = i.ToString() };
                BroadcastPacket(p);

                // 2. Itxaron segundu bat (tokenarekin, ezeztatu ahal izateko)
                await Task.Delay(1000, token);
            }

            // Denbora agortu da!
            Packet timeOutMsg = new Packet { Type = PacketType.TimeUpdate, Message = "0" };
            BroadcastPacket(timeOutMsg);

            // Exekutatu timeout logika (Hutsa jarri edo kanporatu)
            onTimeout?.Invoke();
        }
        catch (TaskCanceledException)
        {
            // Timerra gelditu dugu (Jokalariak garaiz erantzun du). Ez egin ezer.
        }
    }

    // Timerra gelditzeko metodo laguntzailea
    private static void StopTimer()
    {
        if (_timerCts != null) _timerCts.Cancel();
        // Garbitu UIko erlojua
        Packet p = new Packet { Type = PacketType.TimeUpdate, Message = "--" };
        BroadcastPacket(p);
    }

    private static void KickPlayerByName(string username)
    {
        // Bilatu ID-a izenaren bidez
        // FirstOrDefault erabiltzen dugu izena existitzen ez bada errorea ez emateko
        var entry = _clientNames.FirstOrDefault(x => x.Value == username);
        int targetId = entry.Key;

        // IDa aurkitu bada (0 ez bada) eta konexioa existitzen bada
        if (targetId != 0 && _clients.TryGetValue(targetId, out StreamWriter writer))
        {
            try
            {
                // Abisua bidali bezeroari (adeitsua izateko)
                Packet p = new Packet { Type = PacketType.YouAreKicked, Message = "Administratzaileak zerbitzaritik bota zaitu (Kick/Ban)." };
                writer.WriteLine(PacketSerializer.Serialize(p));
                writer.Flush();
            }
            catch { }

            // Pixka bat itxaron mezua iristeko, eta gero itxi
            // (Hau ez da guztiz beharrezkoa baina laguntzen du)
            // Thread.Sleep(100); 

            // Konexioa itxi zerbitzariaren aldetik
            // Oharra: StreamWriter ixteak azpian dagoen Socket-a ere ixten du.
            try { writer.Close(); } catch { }

            Console.WriteLine($"[ADMIN] {username} kanporatua izan da.");

            // Garbiketa (HandleClient-en finally blokeak egingo luke, baina behartu dezakegu hemen ere)
            _clients.TryRemove(targetId, out _);
            _clientNames.TryRemove(targetId, out _);
            _clientRoles.TryRemove(targetId, out _);

            // Zerrenda eguneratua bidali denei
            BroadcastPlayerList();
        }
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Random random = new Random();
        return new string(Enumerable.Repeat(chars, 5).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}