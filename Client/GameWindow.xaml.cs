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

namespace Client
{
    public partial class GameWindow : Window
    {
        private ServerConnection _server;
        private User _currentUser;

        // Eraikitzailea aldatu dugu parametroak jasotzeko!
        public GameWindow(ServerConnection server, User user)
        {
            InitializeComponent();
            _server = server;
            _currentUser = user;

            // 1. Chat-a EZKUTATU hasieran
            // Grid honek dauka Chat-a (begiratu XAML-a, eskumako zatia)
            // Izen bat jarri beharko diogu XAML-ean Grid horri. 
            // Baina errazago: botoia eta inputa desgaitu ditzakegu.
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
                    // Mezu arrunta ("Mikel: Kaixo")
                    // Packet.Message barruan testua dago zuzenean
                    AddRawMessage(packet.Message);
                    break;

                case PacketType.GameStart:
                    // PARTIDA HASI DA!
                    txtMessage.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnStartGame.Visibility = Visibility.Collapsed; // Botoia ezkutatu
                    AddSystemMessage("PARTIDA HASI DA! Orain eztabaidatu dezakezue.");
                    // Hemen etorkizunean rolak eta hitzak erakutsiko ditugu
                    break;
            }
        }

        // Botoiak sakatzean
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                string fullMessage = $"{_currentUser.Username}: {txtMessage.Text}";

                var packet = new Packet
                {
                    Type = PacketType.ChatMessage,
                    Message = fullMessage
                };

                await _server.SendPacketAsync(packet);
                txtMessage.Text = "";
            }
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
    }
}
