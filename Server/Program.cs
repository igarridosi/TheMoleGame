using Server.Data;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

class Program
{
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

        // Bezeroari ID bat esleitu (Hari ID-a erabiliko dugu sinpletasunagatik)
        int clientId = Thread.CurrentThread.ManagedThreadId;

        // ZERRENDARA GEHITU
        _clients.TryAdd(clientId, writer);
        Console.WriteLine($"[SERVER] Bezeroa gehituta zerrendara. Totala: {_clients.Count}");

        try
        {
            string line;
            // Loop honek irakurtzen jarraituko du bezeroa deskonektatu arte
            while ((line = reader.ReadLine()) != null)
            {
                // 1. JSONa Packet bihurtu
                Packet packet = PacketSerializer.Deserialize(line);

                // 2. Zer mezu mota da?
                switch (packet.Type)
                {
                    case PacketType.LoginRequest:
                        var loginReq = PacketSerializer.DeserializeData<LoginRequest>(packet.Message);
                        Console.WriteLine($"[LOGIN] Saiakera: {loginReq.Username}");

                        User user = _dbManager.ValidateUser(loginReq.Username, loginReq.Password);

                        Packet responsePacket = new Packet();

                        if (user != null)
                        {
                            // 1. PAUSOA: Login Response prestatu
                            responsePacket.Type = PacketType.LoginResponse;
                            responsePacket.Message = PacketSerializer.SerializeData(user);

                            // 2. PAUSOA: ERANTZUNA BIDALI (Hau da garrantzitsuena, lehenik egin behar da)
                            writer.WriteLine(PacketSerializer.Serialize(responsePacket));
                            Console.WriteLine($"[LOGIN] ONARTUA: {user.Username}");

                            // 3. PAUSOA: Orain zerrendara gehitu eta denei abisatu
                            _clientNames.TryAdd(clientId, user.Username);
                            _clientRoles.TryAdd(clientId, user.Role);
                            BroadcastPlayerList();
                        }
                        else
                        {
                            // Login okerra
                            responsePacket.Type = PacketType.LoginResponse;
                            responsePacket.Message = null;
                            writer.WriteLine(PacketSerializer.Serialize(responsePacket));
                            Console.WriteLine($"[LOGIN] UKATUA: {loginReq.Username}");
                        }
                        break;

                    case PacketType.ChatMessage:
                        // Norbaitek hitz egiten duenean, DENEI bidali
                        Console.WriteLine($"[CHAT] Mezu berria zabaltzen...");
                        BroadcastPacket(packet);
                        break;

                    case PacketType.GameStart:
                        Console.WriteLine($"[GAME] Partida hasten...");
                        StartGameLogic(); // Metodo berria deitu
                        break;

                    // Jokalariak bere hitza bidali du
                    case PacketType.SubmitGameWord:
                        string word = packet.Message;
                        if (_clientNames.TryGetValue(clientId, out string name))
                        {
                            // Gorde hitza
                            _gameWords.AddOrUpdate(name, word, (k, v) => word);
                            Console.WriteLine($"[GAME] {name}-ek idatzi du: {word}");

                            // Broadcast egin hitza agertzeko zerrendan
                            BroadcastPlayerList();

                            // !!! GARRANTZITSUA: Hurrengo txanda !!!
                            _currentTurnIndex++;
                            NextTurn();
                        }
                        break;

                    case PacketType.RegisterRequest:
                        HandleRegister(packet.Message, writer);
                        break;

                    case PacketType.Vote:
                        // 1. SEGURTASUNA: Kanporatua bada, ez utzi
                        if (_eliminatedPlayers.Contains(clientId))
                        {
                            break;
                        }
                        // 2. SEGURTASUNA: Jada bozkatu badu, ez utzi
                        if (_playersWhoVoted.Contains(clientId))
                        {
                            Console.WriteLine($"[VOTE BLOCKED] Boto errepikatua: {_clientNames[clientId]}");
                            break;
                        }

                        // 3. BOTOA GEHITU
                        string votedName = packet.Message;
                        if (_isVotingPhase)
                        {
                            // Markatu jokalari honek bozkatu duela
                            _playersWhoVoted.Add(clientId);

                            _votes.AddOrUpdate(votedName, 1, (key, count) => count + 1);
                            _playersVotedCount++; // Kontagailua igo

                            string voterName = _clientNames[clientId];
                            Console.WriteLine($"[VOTE] {voterName} -> {votedName} (Totala: {_playersVotedCount})");

                            // 4. KALKULU ZUZENA: Zenbat boto behar ditugu?
                            // Konektatuta daudenak KEN kanporatuta daudenak
                            // Moderatzaileak kendu kalkulutik
                            int totalMods = _clientRoles.Values.Count(r => r == "Moderator");
                            int activePlayers = _clients.Count - _eliminatedPlayers.Count - totalMods;

                            Console.WriteLine($"[DEBUG] Botoak: {_playersVotedCount} / {activePlayers}");

                            if (_playersVotedCount >= activePlayers)
                            {
                                ProcessVotingResults();
                            }
                        }
                        break;

                    case PacketType.RestartGameRequest:
                        // Adminak eskatu du -> Reset eta Gonbidapena
                        ResetGame();
                        break;

                    case PacketType.AddWordRequest:
                        var wordReq = PacketSerializer.DeserializeData<NewWordRequest>(packet.Message);

                        // Logika deitu
                        bool added = _dbManager.AddNewWord(wordReq.Category, wordReq.Word);

                        // Erantzuna prestatu: "OK" edo "EXISTS"
                        Packet resp = new Packet
                        {
                            Type = PacketType.AddWordResponse,
                            Message = added ? "OK" : "EXISTS"
                        };
                        writer.WriteLine(PacketSerializer.Serialize(resp));
                        break;

                    case PacketType.GetCategoriesRequest:
                        // 1. Kategoriak lortu DBtik
                        var cats = _dbManager.GetCategories();

                        // 2. Bidali
                        Packet catResp = new Packet
                        {
                            Type = PacketType.GetCategoriesResponse,
                            Message = PacketSerializer.SerializeData(cats)
                        };
                        writer.WriteLine(PacketSerializer.Serialize(catResp));
                        break;

                    case PacketType.AdminAnnounce:
                        // Moderatzaileak mezu bat bidali du denei
                        string msg = packet.Message;
                        Packet announce = new Packet { Type = PacketType.ChatMessage, Message = $"[MODERATZAILEA]: {msg.ToUpper()}" };
                        BroadcastPacket(announce);
                        break;

                    case PacketType.AdminSkip:
                        // Ronda behartu amaitzera
                        Console.WriteLine("[ADMIN] Ronda saltatzen...");
                        StartVotingPhase(); // Zuzenean bozketara
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Konexioa galdu da: {ex.Message}");
        }
        finally
        {
            // Deskonektatzean, zerrendatik kendu
            _clients.TryRemove(clientId, out _);
            _clientNames.TryRemove(clientId, out _);
            BroadcastPlayerList();
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
                !_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator"
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
            Console.WriteLine("[GAME] Ronda amaitu da! Bozketa garaia...");
            Packet msg = new Packet { Type = PacketType.ChatMessage, Message = "[SISTEMA] Ronda amaitu da! Denek hitz egin dute." };
            BroadcastPacket(msg);

            _currentTurnUser = "";

            StartVotingPhase();
            return;
        }

        // Nori tokatzen zaio?
        int currentClientId = _turnOrder[_currentTurnIndex];

        // Bere izena lortu (Pinturillo zerrendan marka jartzeko)
        if (_clientNames.TryGetValue(currentClientId, out string username))
        {
            _currentTurnUser = username; // Hau gordetzen dugu PlayerList sortzekoan erabiltzeko
            BroadcastPlayerList(); // Denei abisatu zerrenda eguneratzeko (arkatza mugitzeko)
        }

        // Bezeroari Pop-up agertzeko agindua bidali
        if (_clients.TryGetValue(currentClientId, out StreamWriter writer))
        {
            Packet p = new Packet { Type = PacketType.YourTurn };
            try { writer.WriteLine(PacketSerializer.Serialize(p)); } catch { }
        }

        Console.WriteLine($"[GAME] Txanda: {_currentTurnUser} (Index: {_currentTurnIndex})");
    }

    private static void StartVotingPhase()
    {
        _isVotingPhase = true;
        _playersVotedCount = 0;
        _votes.Clear(); // Botoak garbitu
        _playersWhoVoted.Clear();

        Console.WriteLine($"[GAME] {_roundCount}. Ronda amaitu da. BOZKETA HASI DA.");

        Packet msg = new Packet { Type = PacketType.ChatMessage, Message = "[SISTEMA] Ronda amaitu da! Egin klik jokalari baten gainean botatzeko." };
        BroadcastPacket(msg);

        // Zerrenda eguneratu (honek Client-an BOTOIAK aktibatuko ditu IsVotingPhase=true delako)
        BroadcastPlayerList();
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
}