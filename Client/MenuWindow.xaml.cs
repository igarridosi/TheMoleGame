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
        private string _myRoomCode; // Gorde kodea

        // Constructor: Datuak jaso Login leihotik
        public MenuWindow(ServerConnection server, User user)
        {
            InitializeComponent();
            _server = server;
            _currentUser = user;
        }

        // --- INTERFAZE NABIGAZIOA ---

        private void BtnCreateMode_Click(object sender, RoutedEventArgs e)
        {
            pnlMainButtons.Visibility = Visibility.Collapsed;
            pnlCreate.Visibility = Visibility.Visible;

            // Berehala eskatu kodea zerbitzariari
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
            // Reset UI
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
            var packet = new Packet { Type = PacketType.CreateRoomRequest };
            await _server.SendPacketAsync(packet);

            // Begizta: Itxaron mezu zuzena iritsi arte
            // (Batzuetan beste mezu batzuk irits daitezke lehenago, adibidez Chat zaharrak)
            while (true)
            {
                Packet response = await _server.ReadPacketAsync();
                if (response == null) break;

                if (response.Type == PacketType.CreateRoomResponse)
                {
                    _myRoomCode = response.Message;
                    lblRoomCode.Text = _myRoomCode;
                    btnContinueToLobby.Visibility = Visibility.Visible;
                    break; // Kodea lortu dugu, irten begiztatik
                }
            }
        }

        private void BtnContinueToLobby_Click(object sender, RoutedEventArgs e)
        {
            // Ni naiz HOST-a (Sortzailea)
            // GameWindow ireki 'true' pasatuz
            OpenGameWindow(true);
        }

        private async void BtnJoinGame_Click(object sender, RoutedEventArgs e)
        {
            string code = txtCodeInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code)) return;

            btnJoinGame.IsEnabled = false;

            var packet = new Packet { Type = PacketType.JoinRoomRequest, Message = code };
            await _server.SendPacketAsync(packet);

            // Begizta hemen ere
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

        private void OpenGameWindow(bool isHost)
        {
            // GameWindow sortu eta ireki
            // Ziurtatu GameWindow-ek 'isHost' parametroa onartzen duela bere constructor-ean!
            GameWindow gameWin = new GameWindow(_server, _currentUser, isHost);
            gameWin.Show();

            this.Close();
        }
    }
}
