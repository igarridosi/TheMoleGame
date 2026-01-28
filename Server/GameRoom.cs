using Server.Data;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class GameRoom
    {
        public string RoomCode { get; private set; }
        public int HostId { get; private set; }
        public int PlayerCount => _clients.Count;
        public string roomHostName { get; private set; }

        // BEZEROAK
        private ConcurrentDictionary<int, StreamWriter> _clients = new ConcurrentDictionary<int, StreamWriter>();
        private ConcurrentDictionary<int, string> _clientNames = new ConcurrentDictionary<int, string>();
        private ConcurrentDictionary<int, string> _clientRoles = new ConcurrentDictionary<int, string>();

        // JOKO EGOERA
        private ConcurrentDictionary<string, string> _gameWords = new ConcurrentDictionary<string, string>();
        private List<int> _turnOrder = new List<int>();
        private int _currentTurnIndex = 0;
        private string _currentTurnUser = "";

        private bool _isVotingPhase = false;
        private int _roundCount = 1;
        private int _maxRounds = 3;
        private int _impostorId = -1;

        // BOZKETA ETA KANPORAKETA
        private ConcurrentDictionary<string, int> _votes = new ConcurrentDictionary<string, int>();
        private int _playersVotedCount = 0;
        private HashSet<int> _playersWhoVoted = new HashSet<int>();
        private List<int> _eliminatedPlayers = new List<int>();

        // TIMERRAK
        private CancellationTokenSource _timerCts;
        private readonly object _turnLock = new object();

        // DATU-BASEA (Estatistiketarako)
        private DatabaseManager _dbManager = new DatabaseManager();

        public GameRoom(string code, int hostId, StreamWriter hostWriter, string hostName)
        {
            RoomCode = code;
            HostId = hostId;
            roomHostName = hostName;
            AddPlayer(hostId, hostWriter, hostName);
        }

        public void AddPlayer(int clientId, StreamWriter writer, string username)
        {
            _clients.TryAdd(clientId, writer);
            _clientNames.TryAdd(clientId, username);

            // Defektuz 'Player' (Host-ari ere Player jartzen diogu logikarako, baina UI-an Host da)
            // Moderatzailea bada (izena 'moderator'), rola aldatu
            string role = (username.ToLower() == "moderator") ? "Moderator" : "Player";
            _clientRoles.TryAdd(clientId, role);

            BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {username} gelara sartu da." });
            BroadcastPlayerList();
        }

        public List<int> RemovePlayer(int clientId)
        {
            List<int> kickedUsers = new List<int>();

            // 1. Abisatu denei atera dela
            if (_clientNames.TryGetValue(clientId, out string name))
            {
                BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {name} gelatik atera da." });
            }

            // 2. Erabiltzailea bera ezabatu
            _clients.TryRemove(clientId, out _);
            _clientNames.TryRemove(clientId, out _);
            _clientRoles.TryRemove(clientId, out _);
            if (!_eliminatedPlayers.Contains(clientId)) _eliminatedPlayers.Add(clientId);

            // --- LOGIKA BERRIA: HOST-A BADA ---
            if (clientId == HostId)
            {
                Console.WriteLine($"[ROOM {RoomCode}] Host atera da. Gela ixten...");

                // Beste jokalari guztiak lortu
                foreach (var otherClient in _clients)
                {
                    int otherId = otherClient.Key;
                    StreamWriter writer = otherClient.Value;

                    // 1. Abisua bidali (Kicked)
                    try
                    {
                        Packet p = new Packet { Type = PacketType.YouAreKicked, Message = "Host-a atera da. Gela itxi egin da." };
                        writer.WriteLine(PacketSerializer.Serialize(p));
                    }
                    catch { }

                    // 2. Zerrendara gehitu (Program.cs-ek mapa garbitzeko)
                    kickedUsers.Add(otherId);
                }

                // Gela garbitu
                _clients.Clear();
                _clientNames.Clear();
                _clientRoles.Clear();
            }

            // Gainerakoentzat zerrenda eguneratu
            if (_clients.Count > 0)
            {
                BroadcastPlayerList();
            }

            return kickedUsers; // Itzuli nor bota dugun
        }

        // --- PAKETEAK PROZESATU ---
        public void HandlePacket(int clientId, Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.ChatMessage:
                    if (_clientNames.TryGetValue(clientId, out string name))
                    {
                        string msg = packet.Message;
                        BroadcastPacket(packet);
                    }
                    break;

                case PacketType.GameStart:
                    // Bakarrik Host-ak hasi dezake
                    if (clientId == HostId) StartGameLogic();
                    break;

                case PacketType.SubmitGameWord:
                    HandleSubmitWord(clientId, packet.Message);
                    break;

                case PacketType.Vote:
                    HandleVote(clientId, packet.Message);
                    break;

                case PacketType.AdminAnnounce:
                    BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[MODERATZAILEA]: {packet.Message.ToUpper()}" });
                    break;

                case PacketType.AdminSkip:
                    StartVotingPhase();
                    break;

                case PacketType.RequestPlayerList:
                    BroadcastPlayerList();
                    break;

                case PacketType.RestartGameRequest:
                    // Ziurtatu Host-a dela eskatu duena (aukerakoa)
                    if (clientId == HostId)
                    {
                        ResetGame();
                    }
                    break;

                case PacketType.GetStatsRequest:
                    try
                    {
                        // 1. Nire izena lortu (Gela barruko zerrendatik)
                        string myName = _clientNames.ContainsKey(clientId) ? _clientNames[clientId] : "";

                        // 2. Datuak DBtik irakurri
                        int myDbId = _dbManager.GetUserIdByName(myName);
                        var stats = _dbManager.GetUserStats(myDbId);

                        // 3. Bezeroari zuzenean erantzun (ez Broadcast)
                        if (_clients.TryGetValue(clientId, out StreamWriter myWriter))
                        {
                            myWriter.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.GetStatsResponse,
                                Message = PacketSerializer.SerializeData(stats)
                            }));
                        }
                    }
                    catch { }
                    break;

                case PacketType.GetRankingRequest:
                    try
                    {
                        // DBtik ranking orokorra lortu
                        var list = _dbManager.GetGlobalRanking();
                        var gStats = _dbManager.GetGlobalStats();

                        // Bidali
                        if (_clients.TryGetValue(clientId, out StreamWriter myWriter))
                        {
                            myWriter.WriteLine(PacketSerializer.Serialize(new Packet
                            {
                                Type = PacketType.GetRankingResponse,
                                Message = PacketSerializer.SerializeData(new RankingPayload { List = list, Stats = gStats })
                            }));
                        }
                    }
                    catch { }
                    break;

                // GameRoom.cs -> HandlePacket barruan gehitu:

                case PacketType.GetUserListRequest:
                    var users = _dbManager.GetAllUsers();
                    if (_clients.TryGetValue(clientId, out StreamWriter uWriter))
                    {
                        uWriter.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.GetUserListResponse, Message = PacketSerializer.SerializeData(users) }));
                    }
                    break;

                case PacketType.BanUserRequest:
                    var banTarget = PacketSerializer.DeserializeData<User>(packet.Message);
                    _dbManager.SetUserBanStatus(banTarget.Username, banTarget.IsBanned);
                    break;

                case PacketType.UpdateUserRoleRequest:
                    var roleReq = PacketSerializer.DeserializeData<UpdateRoleRequest>(packet.Message);
                    _dbManager.UpdateUserRole(roleReq.Username, roleReq.NewRole);
                    break;

                case PacketType.CreateUserRequest:
                    var createReq = PacketSerializer.DeserializeData<CreateUserRequest>(packet.Message);
                    bool created = _dbManager.CreateUserWithRole(createReq.Username, createReq.Password, createReq.Role);
                    if (_clients.TryGetValue(clientId, out StreamWriter cWriter))
                    {
                        cWriter.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.CreateUserResponse, Message = created ? "OK" : "ERROR" }));
                    }
                    break;

                case PacketType.GetCategoriesRequest:
                    // Kategoriak lortu DBtik
                    var categories = _dbManager.GetCategories();
                    if (_clients.TryGetValue(clientId, out StreamWriter catWriter))
                    {
                        catWriter.WriteLine(PacketSerializer.Serialize(new Packet
                        {
                            Type = PacketType.GetCategoriesResponse,
                            Message = PacketSerializer.SerializeData(categories)
                        }));
                    }
                    break;

                case PacketType.AddWordRequest:
                    // Hitz berria gehitu DBra
                    var wordReq = PacketSerializer.DeserializeData<NewWordRequest>(packet.Message);
                    bool wordAdded = _dbManager.AddNewWord(wordReq.Category, wordReq.Word);

                    string addResult = wordAdded ? "OK" : "EXISTS";

                    if (_clients.TryGetValue(clientId, out StreamWriter wordWriter))
                    {
                        wordWriter.WriteLine(PacketSerializer.Serialize(new Packet
                        {
                            Type = PacketType.AddWordResponse,
                            Message = addResult
                        }));
                    }
                    break;
            }
        }

        // --- JOKO LOGIKA ---

        private void StartGameLogic()
        {
            _roundCount = 1;
            _isVotingPhase = false;
            _currentTurnIndex = 0;
            _gameWords.Clear();
            _votes.Clear();
            _playersWhoVoted.Clear();
            _eliminatedPlayers.Clear();

            // Jokalari aktiboak (Moderatzaileak kenduta)
            var playingClients = _clients.Keys.Where(id =>
                !_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator"
            ).ToList();

            if (playingClients.Count < 3)
            {
                BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = "[SISTEMA] Gutxienez 3 jokalari behar dira." });
                return;
            }

            if (playingClients.Count == 3) _maxRounds = 1;
            else if (playingClients.Count <= 5) _maxRounds = 2;
            else _maxRounds = 3;

            var randomWord = _dbManager.GetRandomWord();
            Random rnd = new Random();
            _impostorId = playingClients[rnd.Next(playingClients.Count)];
            string impostorName = _clientNames[_impostorId];

            foreach (var cid in _clients.Keys)
            {
                if (!_clients.TryGetValue(cid, out StreamWriter writer)) continue;

                GameInfo info = new GameInfo();
                string role = _clientRoles[cid];

                if (role == "Moderator")
                {
                    info.IsImpostor = false;
                    info.Category = randomWord.Category;
                    info.Word = $"{randomWord.Word} || INPOSTOREA: {impostorName}";
                }
                else if (cid == _impostorId)
                {
                    info.IsImpostor = true;
                    info.Category = randomWord.Category;
                    info.Word = "???";
                }
                else
                {
                    info.IsImpostor = false;
                    info.Category = randomWord.Category;
                    info.Word = randomWord.Word;
                }

                try { writer.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.GameInfo, Message = PacketSerializer.SerializeData(info) })); } catch { }
            }

            _turnOrder = playingClients;

            SendRoundUpdate();
            BroadcastPacket(new Packet { Type = PacketType.GameStart, Message = "Partida hasi da!" });

            NextTurn();
        }

        private void NextTurn()
        {
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _currentTurnUser = "";
                StartVotingPhase();
                return;
            }

            int currentClientId = _turnOrder[_currentTurnIndex];

            if (_eliminatedPlayers.Contains(currentClientId))
            {
                _currentTurnIndex++;
                NextTurn();
                return;
            }

            if (_clientNames.TryGetValue(currentClientId, out string username))
            {
                _currentTurnUser = username;
                BroadcastPlayerList();
            }

            if (_clients.TryGetValue(currentClientId, out StreamWriter writer))
            {
                try { writer.WriteLine(PacketSerializer.Serialize(new Packet { Type = PacketType.YourTurn })); } catch { }
            }

            StartTimer(20, () =>
            {
                lock (_turnLock)
                {
                    if (_currentTurnIndex >= _turnOrder.Count) return;

                    if (_clientNames.TryGetValue(currentClientId, out string name))
                    {
                        _gameWords.AddOrUpdate(name, "Hutsa (Time)", (k, v) => "Hutsa (Time)");
                        BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {name}-k ez du garaiz idatzi." });
                    }

                    _currentTurnIndex++;
                    NextTurn();
                }
            });
        }

        private void HandleSubmitWord(int clientId, string word)
        {
            lock (_turnLock)
            {
                if (_currentTurnIndex >= _turnOrder.Count) return;
                int expectedId = _turnOrder[_currentTurnIndex];

                if (clientId != expectedId) return;

                StopTimer();

                if (_clientNames.TryGetValue(clientId, out string name))
                {
                    _gameWords.AddOrUpdate(name, word, (k, v) => word);
                    _currentTurnIndex++;
                    NextTurn();
                }
            }
        }

        private void StartVotingPhase()
        {
            _isVotingPhase = true;
            _playersVotedCount = 0;
            _votes.Clear();
            _playersWhoVoted.Clear();

            Task.Delay(100).Wait();

            BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {_roundCount}. Ronda amaitu da. BOZKETA HASI DA." });
            BroadcastPlayerList();

            StartTimer(60, () =>
            {
                var nonVoters = _clients.Keys.Where(id =>
                    !_eliminatedPlayers.Contains(id) &&
                    !_playersWhoVoted.Contains(id) &&
                    (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
                ).ToList();

                foreach (var id in nonVoters)
                {
                    _eliminatedPlayers.Add(id);
                    if (_clientNames.TryGetValue(id, out string name))
                        BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {name} kanporatua (AFK)." });
                }

                int survivors = _clients.Keys.Count(id =>
                    !_eliminatedPlayers.Contains(id) &&
                    (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
                );

                if (survivors < 3)
                {
                    EndGame("PARTIDA BERTAN BEHERA (Jokalari gutxiegi)");
                    return;
                }

                ProcessVotingResults();
            });
        }

        private void HandleVote(int clientId, string votedName)
        {
            if (!_isVotingPhase) return;
            if (_eliminatedPlayers.Contains(clientId)) return;
            if (_playersWhoVoted.Contains(clientId)) return;

            _playersWhoVoted.Add(clientId);
            _votes.AddOrUpdate(votedName, 1, (k, v) => v + 1);
            _playersVotedCount++;

            int totalMods = _clientRoles.Values.Count(r => r == "Moderator");
            int activePlayers = _clients.Count - _eliminatedPlayers.Count - totalMods;

            if (_playersVotedCount >= activePlayers)
            {
                StopTimer();
                ProcessVotingResults();
            }
        }

        private void ProcessVotingResults()
        {
            _isVotingPhase = false;

            string mostVoted = null;
            int maxVotes = 0;
            bool tie = false;

            foreach (var v in _votes)
            {
                if (v.Value > maxVotes) { maxVotes = v.Value; mostVoted = v.Key; tie = false; }
                else if (v.Value == maxVotes) tie = true;
            }

            string impostorName = _clientNames.ContainsKey(_impostorId) ? _clientNames[_impostorId] : "";

            if (mostVoted != null && !tie)
            {
                BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = $"[SISTEMA] {mostVoted} kanporatua izan da." });

                int kickedId = _clientNames.FirstOrDefault(x => x.Value == mostVoted).Key;
                _eliminatedPlayers.Add(kickedId);

                if (mostVoted == impostorName)
                {
                    EndGame("HERRITARREK IRABAZI DUTE!");
                    return;
                }
            }
            else
            {
                BroadcastPacket(new Packet { Type = PacketType.ChatMessage, Message = "[SISTEMA] Berdinketa. Inor ez da kanporatu." });

                // Errepikatu ronda
                _gameWords.Clear();
                _currentTurnIndex = 0;
                var playingClients = _clients.Keys.Where(id =>
                    (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator") &&
                    !_eliminatedPlayers.Contains(id)
                ).ToList();
                _turnOrder = playingClients;

                BroadcastPlayerList();
                NextTurn();
                return;
            }

            int survivors = _clients.Keys.Count(id =>
                !_eliminatedPlayers.Contains(id) &&
                (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator")
            );

            if (survivors < 3)
            {
                EndGame("INPOSTOREAK IRABAZI");
                return;
            }

            if (_roundCount >= _maxRounds)
            {
                EndGame("INPOSTOREAK IRABAZI (Rondak amaitu dira)");
            }
            else
            {
                _roundCount++;
                StartNextRound();
            }
        }

        private void StartNextRound()
        {
            _gameWords.Clear();
            _currentTurnIndex = 0;
            var playingClients = _clients.Keys.Where(id =>
                (!_clientRoles.ContainsKey(id) || _clientRoles[id] != "Moderator") &&
                !_eliminatedPlayers.Contains(id)
            ).ToList();
            _turnOrder = playingClients;

            SendRoundUpdate();
            BroadcastPlayerList();
            NextTurn();
        }

        private void EndGame(string winner)
        {
            BroadcastPacket(new Packet { Type = PacketType.GameEnd, Message = winner });

            // Estatistikak eguneratu
            if (!winner.Contains("BERTAN BEHERA"))
            {
                bool impostorWon = winner.Contains("INPOSTOREAK");
                foreach (var cid in _clientNames.Keys)
                {
                    string username = _clientNames[cid];
                    string role = _clientRoles.ContainsKey(cid) ? _clientRoles[cid] : "Player";
                    if (role == "Moderator") continue;

                    int dbId = _dbManager.GetUserIdByName(username);
                    bool isImp = (cid == _impostorId);
                    bool isWin = (isImp && impostorWon) || (!isImp && !impostorWon);
                    _dbManager.UpdateStats(dbId, isImp, isWin);
                }
            }
        }

        public void BroadcastPacket(Packet packet)
        {
            string json = PacketSerializer.Serialize(packet);
            foreach (var writer in _clients.Values)
            {
                try { writer.WriteLine(json); } catch { }
            }
        }

        public void BroadcastPlayerList()
        {
            List<PlayerState> list = new List<PlayerState>();
            foreach (var entry in _clientNames)
            {
                int id = entry.Key;
                // Iragazi Moderatzailea zerrendatik
                if (_clientRoles.ContainsKey(id) && _clientRoles[id] == "Moderator") continue;

                bool eliminated = _eliminatedPlayers.Contains(id);
                list.Add(new PlayerState
                {
                    Username = entry.Value,
                    SubmittedWord = _gameWords.ContainsKey(entry.Value) ? _gameWords[entry.Value] : "",
                    IsTurn = (entry.Value == _currentTurnUser),
                    IsEliminated = eliminated,
                    IsVotingPhase = _isVotingPhase && !eliminated
                });
            }
            BroadcastPacket(new Packet { Type = PacketType.PlayerList, Message = PacketSerializer.SerializeData(list) });
        }

        private void SendRoundUpdate()
        {
            var info = new RoundInfo { CurrentRound = _roundCount, TotalRounds = _maxRounds };
            BroadcastPacket(new Packet { Type = PacketType.RoundUpdate, Message = PacketSerializer.SerializeData(info) });
        }

        private async void StartTimer(int seconds, Action onTimeout)
        {
            if (_timerCts != null) _timerCts.Cancel();
            _timerCts = new CancellationTokenSource();
            var token = _timerCts.Token;

            try
            {
                for (int i = seconds; i > 0; i--)
                {
                    BroadcastPacket(new Packet { Type = PacketType.TimeUpdate, Message = i.ToString() });
                    await Task.Delay(1000, token);
                }
                BroadcastPacket(new Packet { Type = PacketType.TimeUpdate, Message = "0" });
                onTimeout?.Invoke();
            }
            catch (TaskCanceledException) { }
        }

        private void StopTimer()
        {
            if (_timerCts != null) _timerCts.Cancel();
            BroadcastPacket(new Packet { Type = PacketType.TimeUpdate, Message = "--" });
        }

        private void ResetGame()
        {
            Console.WriteLine($"[ROOM {RoomCode}] Partida berrabiarazten...");

            // 1. Aldagai guztiak reset
            _roundCount = 1;
            _eliminatedPlayers.Clear();
            _gameWords.Clear();
            _votes.Clear();
            _playersWhoVoted.Clear();
            _isVotingPhase = false;

            // 2. Bezeroei abisatu (Gonbidapena)
            BroadcastPacket(new Packet { Type = PacketType.RestartGameInvite });

            // 3. Zerrenda garbia bidali
            BroadcastPlayerList();
        }
    }
}
