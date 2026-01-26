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
using System.Windows.Media.Animation;

namespace Client
{
    public partial class MenuWindow : Window
    {
        private ServerConnection _server;
        private User _currentUser;
        private string _myRoomCode;
        private SemaphoreSlim _readLock = new SemaphoreSlim(1, 1);

        public MenuWindow(ServerConnection server, User user)
        {
            InitializeComponent();
            _server = server;
            _currentUser = user;

            if (_currentUser.Username.ToLower() == "moderator")
            {
                pnlMainButtons.Visibility = Visibility.Collapsed;
                pnlModeratorRooms.Visibility = Visibility.Visible;
                RequestRooms();
            }
        }

        // --- KLIK GAKOA: KODEA KOPIATU ---
        private async void CodesBox_Click(object sender, MouseButtonEventArgs e)
        {
            if (lblRoomCode.Text == "-----" || lblRoomCode.Text == "") return;

            try
            {
                // 1. Kopiatu (Errore isila)
                try { Clipboard.SetText(lblRoomCode.Text); } catch { }

                // 2. Animazioa: Kodea ezkutatu, Mezua erakutsi
                lblRoomCode.Visibility = Visibility.Collapsed;
                pnlCopiedMessage.Visibility = Visibility.Visible;
                brdCodeBox.BorderBrush = System.Windows.Media.Brushes.Turquoise;

                // 3. Itxaron
                await Task.Delay(1500);

                // 4. Itzuli
                pnlCopiedMessage.Visibility = Visibility.Collapsed;
                lblRoomCode.Visibility = Visibility.Visible;
                brdCodeBox.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }
            catch { }
        }

        // --- BESTE BOTOIAK (Aurreko berdinak) ---

        private void BtnCreateMode_Click(object sender, RoutedEventArgs e)
        {
            pnlMainButtons.Visibility = Visibility.Collapsed;
            pnlCreate.Visibility = Visibility.Visible;
            RequestCreateRoom();
        }

        private void BtnJoinMode_Click(object sender, RoutedEventArgs e)
        {
            pnlMainButtons.Visibility = Visibility.Collapsed;
            pnlJoin.Visibility = Visibility.Visible;
            txtCodeInput.Focus();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            pnlCreate.Visibility = Visibility.Collapsed;
            pnlJoin.Visibility = Visibility.Collapsed;
            pnlMainButtons.Visibility = Visibility.Visible;
            lblRoomCode.Text = "-----";
            btnContinueToLobby.Visibility = Visibility.Collapsed;
            txtCodeInput.Text = "";
        }

        private async void RequestCreateRoom()
        {
            await _readLock.WaitAsync();
            try
            {
                await _server.SendPacketAsync(new Packet { Type = PacketType.CreateRoomRequest });

                while (true)
                {
                    Packet response = await _server.ReadPacketAsync();
                    if (response == null) break;

                    if (response.Type == PacketType.CreateRoomResponse)
                    {
                        _myRoomCode = response.Message;
                        lblRoomCode.Text = _myRoomCode;
                        btnContinueToLobby.Visibility = Visibility.Visible;
                        break;
                    }
                }
            }
            finally { _readLock.Release(); }
        }

        private void BtnContinueToLobby_Click(object sender, RoutedEventArgs e)
        {
            OpenGameWindow(true);
        }

        private async void BtnJoinGame_Click(object sender, RoutedEventArgs e)
        {
            string code = txtCodeInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code)) return;

            btnJoinGame.IsEnabled = false;

            await _readLock.WaitAsync();
            try
            {
                await _server.SendPacketAsync(new Packet { Type = PacketType.JoinRoomRequest, Message = code });

                while (true)
                {
                    Packet response = await _server.ReadPacketAsync();
                    if (response == null) break;

                    if (response.Type == PacketType.JoinRoomResponse)
                    {
                        if (response.Message == "OK") OpenGameWindow(false);
                        else
                        {
                            MessageBox.Show("ERROREA: " + response.Message);
                            btnJoinGame.IsEnabled = true;
                        }
                        break;
                    }
                }
            }
            finally { _readLock.Release(); }
        }

        private void OpenGameWindow(bool isHost)
        {
            string finalCode = isHost ? _myRoomCode : txtCodeInput.Text.Trim().ToUpper();
            if (lstRooms != null && lstRooms.IsVisible && lstRooms.SelectedItem != null)
                finalCode = lstRooms.SelectedItem.ToString();

            GameWindow gameWin = new GameWindow(_server, _currentUser, isHost, finalCode);
            gameWin.Show();
            this.Close();
        }

        // Moderatzaile metodoak mantendu...
        private async void RequestRooms()
        {
            await _readLock.WaitAsync();
            try
            {
                await _server.SendPacketAsync(new Packet { Type = PacketType.GetRoomsRequest });
                Packet response = await _server.ReadPacketAsync();
                if (response != null && response.Type == PacketType.GetRoomsResponse)
                {
                    var rooms = PacketSerializer.DeserializeData<List<string>>(response.Message);
                    lstRooms.ItemsSource = rooms;
                }
            }
            finally { _readLock.Release(); }
        }

        private void BtnRefreshRooms_Click(object sender, RoutedEventArgs e) => RequestRooms();

        private async void BtnJoinSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lstRooms.SelectedItem == null) return;
            string code = lstRooms.SelectedItem.ToString();

            await _readLock.WaitAsync();
            try
            {
                await _server.SendPacketAsync(new Packet { Type = PacketType.JoinRoomRequest, Message = code });
                while (true)
                {
                    Packet response = await _server.ReadPacketAsync();
                    if (response == null) break;
                    if (response.Type == PacketType.JoinRoomResponse)
                    {
                        if (response.Message == "OK") OpenGameWindow(false);
                        else MessageBox.Show(response.Message);
                        break;
                    }
                }
            }
            finally { _readLock.Release(); }
        }
    }
}
