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
    public partial class MenuWindow : Window
    {
        private ServerConnection _server;
        private User _currentUser;
        private string _myRoomCode;

        // KONPONKETA: Semaforoa (Bakarrik hari bat pasatzen uzteko irakurketara)
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

        // --- LOGIKA ---

        private async void RequestCreateRoom()
        {
            // LOCK SARTU
            await _readLock.WaitAsync();
            try
            {
                var packet = new Packet { Type = PacketType.CreateRoomRequest };
                await _server.SendPacketAsync(packet);

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
            finally
            {
                _readLock.Release(); // BETI ASKATU
            }
        }

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
            finally
            {
                _readLock.Release();
            }
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
                var packet = new Packet { Type = PacketType.JoinRoomRequest, Message = code };
                await _server.SendPacketAsync(packet);

                while (true)
                {
                    Packet response = await _server.ReadPacketAsync();
                    if (response == null) break;

                    if (response.Type == PacketType.JoinRoomResponse)
                    {
                        if (response.Message == "OK")
                        {
                            OpenGameWindow(false);
                        }
                        else
                        {
                            MessageBox.Show("ERROREA: " + response.Message);
                            btnJoinGame.IsEnabled = true;
                        }
                        break;
                    }
                }
            }
            finally
            {
                _readLock.Release();
            }
        }

        private async void BtnJoinSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lstRooms.SelectedItem == null)
            {
                MessageBox.Show("Aukeratu partida bat zerrendatik.");
                return;
            }

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
                        if (response.Message == "OK")
                        {
                            OpenGameWindow(false);
                        }
                        else
                        {
                            MessageBox.Show("ERROREA: " + response.Message);
                        }
                        break;
                    }
                }
            }
            finally
            {
                _readLock.Release();
            }
        }

        private void BtnRefreshRooms_Click(object sender, RoutedEventArgs e)
        {
            RequestRooms();
        }

        private void OpenGameWindow(bool isHost)
        {
            GameWindow gameWin = new GameWindow(_server, _currentUser, isHost);
            gameWin.Show();
            this.Close();
        }
    }
}
