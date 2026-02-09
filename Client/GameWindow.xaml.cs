using Client.Net;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace Client
{
    public partial class GameWindow : Window
    {
        private bool _isGameEnded = false;

        private ServerConnection _server;
        private User _currentUser;
        private InputWordWindow _currentInputWindow; // Leiho irekia gordetzeko
        private AdminUsersWindow _adminUsersWindow;

        private bool _isHost;
        private bool _isReading = true;

        // Variables para manejar la solicitud de categorías
        private bool _waitingForCategories = false;
        private List<string> _receivedCategories = null;

        public ObservableCollection<PlayerState> Players { get; set; } = new ObservableCollection<PlayerState>();

        // Eraikitzailea aldatu dugu parametroak jasotzeko!
        public GameWindow(ServerConnection server, User user, bool isHost, string roomCode)
        {
            InitializeComponent();
            _server = server;
            _currentUser = user;
            _isHost = isHost;

            lblCurrentRoomCode.Text = roomCode;

            // Botoia erakutsi Host bada (Admin rola ahaztu)
            if (isHost)
            {
                btnStartGame.Visibility = Visibility.Visible;
            }

            // 1. Datuen lotura (Binding) zerrendarako
            lstPlayers.ItemsSource = Players;

            // 2. Hasierako egoera: Chat-a eta kontrolak blokeatuta
            txtMessage.IsEnabled = false;
            btnSend.IsEnabled = false;

            // Mezua hasieran
            AddSystemMessage("Lobby-ra konektatuta. Partida hasi arte itxaron...");
            lblUserInfo.Text = $"(Erabiltzailea: {_currentUser.Username})";

            if (_currentUser.Role == "Moderator")
            {
                btnAdminWords.Visibility = Visibility.Visible; // Hitzak gehitzeko botoia
            }
            

            // --- MODERATZAILE LOGIKA (BERRIA) ---
            // Erabiltzailea 'moderator' bada, Panel Berezia erakutsi
            if (_currentUser.Username.ToLower() == "moderator")
            {
                // Ziurtatu XAML-en 'pnlModerator' deitu diozula panelari
                if (pnlModerator != null)
                {
                    pnlModerator.Visibility = Visibility.Visible;
                }

                AddSystemMessage("SISTEMA: [GOD MODE] Moderatzaile tresnak aktibatuta.");

                // Aukerakoa: Moderatzaileak agian beti izan beharko luke txata irekita?
                // Nahi baduzu, deskomentatu hurrengo lerroak:
                // txtMessage.IsEnabled = true;
                // btnSend.IsEnabled = true;

                txtMessage.Visibility = Visibility.Collapsed;
                btnSend.Visibility = Visibility.Collapsed;
            }

            // 3. ENTZUN (Atzeko planoan)
            Task.Run(() => ReceiveLoop());

            // Leihoa ireki bezain laster, zerrenda eguneratua eskatu
            RequestListUpdate();
        }

        private async void RequestListUpdate()
        {
            // Atzerapen txiki bat leihoa guztiz kargatu dadin
            await Task.Delay(500);
            await _server.SendPacketAsync(new Packet { Type = PacketType.RequestPlayerList });
        }

        // Metodo honek etengabe entzungo du zerbitzaria
        private async Task ReceiveLoop()
        {
            while (_isReading && _server.IsConnected)
            {
                try
                {
                    Packet packet = await _server.ReadPacketAsync();
                    if (packet == null) break;
                    Dispatcher.Invoke(() => HandleServerPacket(packet));
                }
                catch { break; }
            }
        }

        private async void BtnAdminWords_Click(object sender, RoutedEventArgs e)
        {
            // Markatu eskabidea bidali dugula
            _waitingForCategories = true;
            _receivedCategories = null;

            // 1. Eskatu kategoriak zerbitzariari
            var packet = new Packet { Type = PacketType.GetCategoriesRequest };
            await _server.SendPacketAsync(packet);

            // 2. Itxaron HandleServerPacket-ek kategoriak jaso arte (max 5 segundo)
            int waitTime = 0;
            while (_waitingForCategories && waitTime < 5000)
            {
                await Task.Delay(100);
                waitTime += 100;
            }

            // 3. Kategoria zerrenda jaso badugu, leihoa ireki
            if (_receivedCategories != null)
            {
                WordsPanelWindow adminWin = new WordsPanelWindow(_server, _receivedCategories);
                adminWin.ShowDialog();
            }
            else
            {
                MessageBox.Show("Ezin izan dira kategoriak kargatu. Saiatu berriro.", "Errorea", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Reset
            _waitingForCategories = false;
            _receivedCategories = null;
        }

        private void HandleServerPacket(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.ChatMessage:
                    AddRawMessage(packet.Message);
                    break;

                case PacketType.GameStart:
                    // Chat-a desblokeatu
                    txtMessage.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnStartGame.Visibility = Visibility.Collapsed;
                    AddSystemMessage("PARTIDA HASI DA! Eztabaidatu txatean.");
                    break;

                // Rola eta Hitza jasotzeko
                case PacketType.GameInfo:
                    var info = PacketSerializer.DeserializeData<GameInfo>(packet.Message);
                    DisplayGameInfo(info);
                    break;

                // Zerrenda osoa eguneratu
                case PacketType.PlayerList:
                    var newList = PacketSerializer.DeserializeData<List<PlayerState>>(packet.Message);

                    // MODERATZAILEA BANAIZ, BOTOIAK EZABATU
                    // Zerrenda pantailaratu aurretik, 'IsVotingPhase' false jartzen dugu lokalean.
                    if (_currentUser.Username.ToLower() == "moderator")
                    {
                        foreach (var p in newList)
                        {
                            p.IsVotingPhase = false; // Botoia ezkutatu egingo da
                        }
                    }

                    Players.Clear();
                    foreach (var p in newList)
                    {
                        Players.Add(p);
                    }

                    // EGIAZTAPEN BERRIA: Kanporatua al nago?
                    // Bilatu nire erabiltzailea zerrendan
                    var me = newList.FirstOrDefault(p => p.Username == _currentUser.Username);

                    if (me != null && me.IsEliminated)
                    {
                        // 1. Chat-a desgaitu
                        txtMessage.IsEnabled = false;
                        btnSend.IsEnabled = false;
                        txtMessage.Text = "Kanporatua izan zara. Ezin duzu hitz egin.";

                        // 2. Zerrenda desgaitu (Horrela ezin da "Botatu" sakatu)
                        lstPlayers.IsEnabled = false;
                    }
                    else
                    {
                        // Botoa eman ondoren blokeatu genuen, 
                        // baina zerrenda berria iristean (fase aldaketa) berriz aktibatu behar da
                        // baldin eta jokoa ez den amaitu.
                        if (!_isGameEnded)
                        {
                            lstPlayers.IsEnabled = true;
                        }
                    }
                    break;

                // POP-UP IREKI
                case PacketType.YourTurn:
                    // UI haria erabili behar da leihoa irekitzeko
                    this.Dispatcher.Invoke(() =>
                    {
                        // Leiho berria sortu (Server pasatuz)
                        _currentInputWindow = new InputWordWindow(_server);

                        // Show() erabiltzen dugu (EZ ShowDialog). 
                        // Honek UI eguneratzen jarraitzea ahalbidetzen du (Timerra ikusiko da!)
                        _currentInputWindow.Show();
                    });
                    break;

                case PacketType.RoundUpdate:
                    var rInfo = PacketSerializer.DeserializeData<RoundInfo>(packet.Message);
                    // UI Thread-ean eguneratu
                    lblRoundInfo.Text = $"{rInfo.CurrentRound} / {rInfo.TotalRounds}";
                    break;

                case PacketType.GameEnd:
                    _isGameEnded = true;
                    string winner = packet.Message;

                    MessageBox.Show($"JOKOA AMAITU DA!\n\nIRABAZLEA: {winner}", "GAME OVER", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Host bada, botoia erakutsi
                    if (_isHost)
                    {
                        btnRestart.Visibility = Visibility.Visible;
                    }
                    break;

                // KASU BERRIA: Gonbidapena jaso
                case PacketType.RestartGameInvite:
                    HandleRestartInvite();
                    break;

                case PacketType.AddWordResponse:
                    string result = packet.Message;

                    if (result == "OK")
                    {
                        AddSystemMessage("ADMIN: Hitz berria ondo gorde da datu-basean.");
                        // Pop-up txiki bat ere atera dezakezu
                        MessageBox.Show("Hitza ondo gorde da!", "Arrakasta", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (result == "EXISTS")
                    {
                        // HONA HEMEN ZURE ARAZOA KONPONTZEN DUEN MEZUA:
                        MessageBox.Show("ERROREA: Hitza DAGOENEKO EXISTITZEN da datu-basean.", "Errorea", MessageBoxButton.OK, MessageBoxImage.Warning);
                        AddSystemMessage("ADMIN ERROREA: Hitza ez da gorde, errepikatuta dagoelako.");
                    }
                    else
                    {
                        MessageBox.Show("Errore ezezagun bat gertatu da.", "Errorea", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;

                case PacketType.GetCategoriesResponse:
                    var catList = PacketSerializer.DeserializeData<List<string>>(packet.Message);

                    // Gordetzeko aldagaian gorde eta markatu jasota dagoela
                    _receivedCategories = catList;
                    _waitingForCategories = false;
                    break;

                case PacketType.TimeUpdate:
                    string seconds = packet.Message;

                    this.Dispatcher.Invoke(() =>
                    {
                        // 1. Testua eguneratu (Berdin dio zenbakia edo "--" den)
                        lblTimer.Text = seconds;

                        // KONPONKETA: Denbora agortu bada eta leihoa irekita badago -> ITXI
                        if (seconds == "0" && _currentInputWindow != null && _currentInputWindow.IsLoaded)
                        {
                            _currentInputWindow.Close();
                            _currentInputWindow = null;
                        }
                    });
                    break;

                case PacketType.GetUserListResponse:
                    var userList = PacketSerializer.DeserializeData<List<User>>(packet.Message);
                    this.Dispatcher.Invoke(() =>
                    {
                        if (_adminUsersWindow != null && _adminUsersWindow.IsVisible)
                        {
                            _adminUsersWindow.UpdateList(userList);
                        }
                    });
                    break;

                case PacketType.GetStatsResponse:
                    var myStats = PacketSerializer.DeserializeData<UserStats>(packet.Message);

                    // UI Thread-ean ireki leihoa
                    this.Dispatcher.Invoke(() =>
                    {
                        // Leiho berria sortu eta ireki
                        // _currentUser.Username pasatzen diogu izenburuan jartzeko
                        UserProfileWindow profileWin = new UserProfileWindow(_currentUser.Username, myStats);
                        profileWin.ShowDialog(); // ShowDialog erabiltzen dugu gainean geratzeko (modal)
                    });
                    break;

                case PacketType.GetRankingResponse:
                    // Orain RankingPayload deserializeatzen dugu!
                    var payload = PacketSerializer.DeserializeData<RankingPayload>(packet.Message);

                    this.Dispatcher.Invoke(() =>
                    {
                        bool amIMod = _currentUser.Username.ToLower() == "moderator";
                        RankingWindow rankWin = new RankingWindow(payload, amIMod);
                        rankWin.ShowDialog();
                    });
                    break;

                case PacketType.CreateUserResponse:
                    if (packet.Message == "OK")
                        MessageBox.Show("Erabiltzailea ondo sortu da!", "Arrakasta", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("ERROREA: Erabiltzailea existitzen da edo zerbait gaizki joan da.", "Errorea", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                case PacketType.YouAreKicked:
                    MessageBox.Show(packet.Message, "Gela Itxita", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // ITZULI MENURA
                    MenuWindow menu = new MenuWindow(_server, _currentUser);
                    menu.Show();

                    this.Close();
                    break;
            }
        }

        // Informazioa pantailan polito erakusteko metodoa
        private void DisplayGameInfo(GameInfo info)
        {
            // UI Thread-ean gaudela ziurtatu (dispatcher barruan deitu behar da hau)

            string roleMessage;
            string color;

            if (info.IsImpostor)
            {
                roleMessage = $"INPOSTOREA ZARA! (Kategoria: {info.Category})";
                color = "#FF5555"; // Gorria
                AddSystemMessage($"ZU ZARA INPOSTOREA! Ez dakizu hitza, baina Kategoria '{info.Category}' da.");
            }
            else if (!string.IsNullOrEmpty(info.Word) && info.Word.Contains("INPOSTOREA"))
            {
                // Hau Moderatzailea da (God Mode)
                roleMessage = $"GOD MODE: {info.Word}";
                color = "#FFFF00";
            }
            else
            {
                roleMessage = $"HERRITARRA ZARA. Hitza: {info.Word}";
                color = "#00ADB5"; // Urdina
                AddSystemMessage($"Kategoria: {info.Category}. Hitza: {info.Word}");
            }

            // Leihoaren goiburua edo testu berezi bat aldatu dezakegu
            // Adibidez, lblUserInfo eguneratu
            lblUserInfo.Text = roleMessage;
            lblUserInfo.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
        }

        // Botoiak sakatzean
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            string fullMessage = $"{_currentUser.Username}: {txtMessage.Text}";

            var packet = new Packet
            {
                Type = PacketType.ChatMessage,
                Message = fullMessage
            };

            await _server.SendPacketAsync(packet);

            txtMessage.Text = "";
        }
    

        private async void BtnStartGame_Click(object sender, RoutedEventArgs e)
        {
            // Adminak partida hasteko agindua bidaltzen du
            var packet = new Packet { Type = PacketType.GameStart };
            await _server.SendPacketAsync(packet);
        }

        // Laguntzaile txikia mezuak zuzenean jartzeko
        private void AddRawMessage(string text)
        {
            var txt = new System.Windows.Controls.TextBlock
            {
                Text = text,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 2, 0, 2)
            };
            pnlChatMessages.Children.Add(txt);
            scrollChat.ScrollToBottom();
        }

        // UI Laguntzaileak mezuak polito erakusteko
        private void AddSystemMessage(string text)
        {
            var txt = new System.Windows.Controls.TextBlock
            {
                Text = $"[SISTEMA] {text}",
                Foreground = System.Windows.Media.Brushes.Yellow,
                Margin = new Thickness(0, 2, 0, 2)
            };
            pnlChatMessages.Children.Add(txt);
        }

        private void AddUserMessage(string user, string text)
        {
            var txt = new System.Windows.Controls.TextBlock
            {
                Text = $"{user}: {text}",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 2, 0, 2)
            };
            pnlChatMessages.Children.Add(txt);
            scrollChat.ScrollToBottom();
        }

        // Laguntzaile txikia bidaltzeko (Task.Run barruan ez egoteko)
        private async void SendGameWord(string word)
        {
            var packet = new Packet
            {
                Type = PacketType.SubmitGameWord,
                Message = word
            };
            await _server.SendPacketAsync(packet);
        }

        private async void BtnVote_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string targetUser = btn.Tag.ToString(); // Tag-etik izena lortu

            // Norbere buruari ez bozkatzeko (aukerakoa)
            if (targetUser == _currentUser.Username)
            {
                MessageBox.Show("Ezin diozu zeure buruari bozkatu!");
                return;
            }

            // Botoa bidali
            var packet = new Packet
            {
                Type = PacketType.Vote,
                Message = targetUser
            };
            await _server.SendPacketAsync(packet);

            // Feedback bisuala (Botoiak desgaitu ditzakegu, baina sinple uzteko...)
            AddSystemMessage($"Botoa bidali diozu: {targetUser}-ri. Emaitzen zain...");

            // Zerrenda desgaitu botoa eman ondoren
            // Horrela erabiltzaileak ikusten du ezin duela gehiago sakatu
            lstPlayers.IsEnabled = false;
        }

        private void HandleRestartInvite()
        {
            // Adminak zuzenean reset egiten du (berak eman diolako botoiari)
            if (_isHost)
            {
                ResetClientUI();
                return;
            }

            // Beste erabiltzaileei galdetu
            var result = MessageBox.Show(
                "Adminak partida berri bat sortu du. Parte hartu nahi duzu?",
                "Partida Berria",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ResetClientUI();
            }
            else
            {
                // Ezetz esaten badu, leihoa itxi eta deskonektatu
                this.Close();
            }
        }

        private void ResetClientUI()
        {
            _isGameEnded = false;

            // UI garbitu
            lblUserInfo.Text = $"(Erabiltzailea: {_currentUser.Username})";
            lblUserInfo.Foreground = System.Windows.Media.Brushes.Gray;

            lblRoundInfo.Text = "LOBBY";
            pnlChatMessages.Children.Clear();
            AddSystemMessage("Lobby-ra itzuli zara. Itxaron partida hasi arte...");

            // Kontrolak berriz aktibatu
            txtMessage.IsEnabled = false; // Hasi arte blokeatuta
            txtMessage.Text = "";
            btnSend.IsEnabled = false;
            lstPlayers.IsEnabled = true;  // Zerrenda berriz aktibatu

            // Botoiak ezkutatu
            btnRestart.Visibility = Visibility.Collapsed;

            // Admin bada, "Hasi" botoia berriro erakutsi
            if (_isHost)
            {
                btnStartGame.Visibility = Visibility.Visible;
            }
        }

        // Botoiaren kodea
        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            var packet = new Packet { Type = PacketType.RestartGameRequest };
            await _server.SendPacketAsync(packet);
        }

        // --- MODERATZAILE BOTOIAK ---

        private async void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            // Pause logika (PacketType.AdminPause sortu beharko zenuke Shared-en, 
            // baina errorerik ez emateko, mezu soil bat bidaliko dugu oraingoz)
            MessageBox.Show("Pause funtzioa oraindik ez dago inplementatuta Server aldean, baina botoia dabil!");

            // Inplementatuta badaukazu:
            // await _server.SendPacketAsync(new Packet { Type = PacketType.AdminPause });
        }

        private async void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            // Ziurtatu PacketType.AdminSkip existitzen dela Shared/PacketType.cs fitxategian!
            var packet = new Packet { Type = PacketType.AdminSkip };
            await _server.SendPacketAsync(packet);
            AddSystemMessage("MODERATOR: Ronda saltatzeko agindua bidali da.");
        }

        private async void BtnAnnounce_Click(object sender, RoutedEventArgs e)
        {
            // Input txiki bat eskatzeko (InputBox ez dago WPFn defektuz, beraz leiho bat erabili edo hardcodeatu)
            // Sinpletasunagatik, leiho berri bat sortu beharrean, mezu finko bat bidaliko dugu probatzeko,
            // edo InputWordWindow berrerabili dezakegu!

            GenericInputWindow win = new GenericInputWindow("ANNOUNCE (MEZU OROKORRA)");

            if (win.ShowDialog() == true)
            {
                string msg = win.ResultText;
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    var packet = new Packet
                    {
                        Type = PacketType.AdminAnnounce,
                        Message = msg
                    };
                    await _server.SendPacketAsync(packet);
                }
            }
        }

        private void TxtMessage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Enter sakatu bada, botoiaren klik funtzioa deitu
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSend_Click(sender, e);
            }
        }

        private void BtnManageUsers_Click(object sender, RoutedEventArgs e)
        {
            // Leihoa irekita badago, ez ireki berriro (fokua eman)
            if (_adminUsersWindow != null && _adminUsersWindow.IsVisible)
            {
                _adminUsersWindow.Focus();
                return;
            }

            // Leiho berria sortu eta ireki
            _adminUsersWindow = new AdminUsersWindow(_server, _currentUser.Username);
            _adminUsersWindow.Show();
        }

        private async void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            // Eskatu estatistikak niretzat
            var packet = new Packet { Type = PacketType.GetStatsRequest };
            await _server.SendPacketAsync(packet);
        }

        private async void BtnRanking_Click(object sender, RoutedEventArgs e)
        {
            // Eskatu rankinga zerbitzariari
            var packet = new Packet { Type = PacketType.GetRankingRequest };
            await _server.SendPacketAsync(packet);
        }

        private async void BtnExitRoom_Click(object sender, RoutedEventArgs e)
        {
            // 1. BEGIZTA GELDITU
            _isReading = false;

            // 2. Zerbitzariari abisatu
            await _server.SendPacketAsync(new Packet { Type = PacketType.LeaveRoomRequest });

            // 3. Leiho berria
            MenuWindow menu = new MenuWindow(_server, _currentUser);
            menu.Show();

            // 4. Hau itxi
            this.Close();
        }
    }
}
