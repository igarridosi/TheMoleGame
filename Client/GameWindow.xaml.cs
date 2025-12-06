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

        public ObservableCollection<PlayerState> Players { get; set; } = new ObservableCollection<PlayerState>();

        // Eraikitzailea aldatu dugu parametroak jasotzeko!
        public GameWindow(ServerConnection server, User user)
        {
            InitializeComponent();
            _server = server;
            _currentUser = user;

            // Lotu ListBox-a gure zerrendarekin
            lstPlayers.ItemsSource = Players;

            // 1. Chat-a EZKUTATU hasieran
            txtMessage.IsEnabled = false;
            btnSend.IsEnabled = false;
            AddSystemMessage("Partida hasi arte itxaron txateatzeko...");

            lblUserInfo.Text = $"(Erabiltzailea: {_currentUser.Username})";
            if (_currentUser.IsAdmin) btnStartGame.Visibility = Visibility.Visible;

            // 2. HASI ENTZUTEN (Atzeko planoan)
            Task.Run(() => ReceiveLoop());
        }

        // Metodo honek etengabe entzungo du zerbitzaria
        private async Task ReceiveLoop()
        {
            while (true)
            {
                try
                {
                    Packet packet = await _server.ReadPacketAsync();
                    if (packet == null) break; // Konexioa itxi da

                    // UI eguneratzeko, Dispatcher erabili behar da (WPF haria)
                    Dispatcher.Invoke(() =>
                    {
                        HandleServerPacket(packet);
                    });
                }
                catch
                {
                    break;
                }
            }
            // Konexioa galtzen bada...
            Dispatcher.Invoke(() => MessageBox.Show("Zerbitzariarekin konexioa galdu da!"));
            Dispatcher.Invoke(() => this.Close());
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
                        InputWordWindow inputWin = new InputWordWindow();

                        // ShowDialog(): Leiho honek programa blokeatzen du itxi arte (Pop-up modua)
                        if (inputWin.ShowDialog() == true)
                        {
                            string word = inputWin.EnteredWord;

                            // Hitza zerbitzarira bidali
                            SendGameWord(word);
                        }
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

                    MessageBox.Show($"JOKOA AMAITU DA!\n\nIRABAZLEAK: {winner}", "GAME OVER", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Admin bada, botoia erakutsi
                    if (_currentUser.IsAdmin)
                    {
                        btnRestart.Visibility = Visibility.Visible;
                    }
                    break;

                // KASU BERRIA: Gonbidapena jaso
                case PacketType.RestartGameInvite:
                    HandleRestartInvite();
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
                roleMessage = "INPOSTOREA ZARA!";
                color = "#FF5555"; // Gorria
                AddSystemMessage("Saiatu besteen hitza asmatzen deskubritu gabe!");
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

            // Begiratu ea nire txanda den (zerrendan begiratuz)
            bool isMyTurn = false;
            foreach (var p in Players)
            {
                if (p.Username == _currentUser.Username && p.IsTurn) isMyTurn = true;
            }

            if (isMyTurn)
            {
                // JOKO HITZA BIDALI
                var packet = new Packet
                {
                    Type = PacketType.SubmitGameWord,
                    Message = txtMessage.Text
                };
                await _server.SendPacketAsync(packet);
            }
            else
            {
                // CHAT NORMALA
                string fullMessage = $"{_currentUser.Username}: {txtMessage.Text}";
                var packet = new Packet
                {
                    Type = PacketType.ChatMessage,
                    Message = fullMessage
                };
                await _server.SendPacketAsync(packet);
            }
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
            if (_currentUser.IsAdmin)
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
            btnSend.IsEnabled = false;
            lstPlayers.IsEnabled = true;  // Zerrenda berriz aktibatu

            // Botoiak ezkutatu
            btnRestart.Visibility = Visibility.Collapsed;

            // Admin bada, "Hasi" botoia berriro erakutsi
            if (_currentUser.IsAdmin)
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
    }
}
