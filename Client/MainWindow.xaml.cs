using Client.Net;
using Shared;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    public partial class MainWindow : Window
    {
        private ServerConnection _server;

        public MainWindow()
        {
            InitializeComponent();
            _server = new ServerConnection();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // 1. Balidazio txikia
            string ip = txtIp.Text;
            string user = txtUsername.Text;
            string pass = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                lblStatus.Text = "Mesedez, bete eremu guztiak.";
                return;
            }

            btnLogin.IsEnabled = false; // Botoia desgaitu prozesatzen ari den bitartean
            lblStatus.Text = "Konektatzen...";
            lblStatus.Foreground = System.Windows.Media.Brushes.Yellow;

            // 2. Zerbitzariarekin konektatu
            bool connected = await _server.ConnectAsync(ip, 8080);

            if (!connected)
            {
                lblStatus.Text = "Ezin izan da zerbitzariarekin konektatu.";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
                btnLogin.IsEnabled = true;
                return;
            }

            // 3. Login paketea prestatu
            // Oharra: Momentuz pasahitza testu arruntean bidaliko dugu
            // Hurrengo pausoetan Hash-a eta datu-base konprobazioa egingo dugu.
            var loginData = new LoginRequest
            {
                Username = user,
                Password = pass
            };

            Packet loginPacket = new Packet
            {
                Type = PacketType.LoginRequest,
                Message = PacketSerializer.SerializeData(loginData)
            };

            // 4. Bidali
            await _server.SendPacketAsync(loginPacket);
            Packet response = await _server.ReadPacketAsync();

            if (response != null && response.Type == PacketType.LoginResponse)
            {
                if (response.Message != null)
                {
                    // 1. Erabiltzailearen datuak lortu
                    User loggedUser = PacketSerializer.DeserializeData<User>(response.Message);

                    // 2. Leiho berria sortu, konexioa eta erabiltzailea pasatuz
                    GameWindow gameWindow = new GameWindow(_server, loggedUser);

                    // 3. Leiho berria erakutsi
                    gameWindow.Show();

                    // 4. Leiho hau itxi
                    this.Close();
                }
                else
                {
                    lblStatus.Text = "Erabiltzailea edo pasahitza okerra.";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    btnLogin.IsEnabled = true;
                }
            }
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string ip = txtIp.Text;
            string user = txtUsername.Text;
            string pass = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                lblStatus.Text = "Idatzi izena eta pasahitza erregistratzeko.";
                return;
            }

            lblStatus.Text = "Erregistratzen...";
            lblStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            btnLogin.IsEnabled = false;
            btnRegister.IsEnabled = false;

            // 1. Konektatu (beharrezkoa bada)
            if (!_server.IsConnected)
            {
                bool connected = await _server.ConnectAsync(ip, 8080);
                if (!connected)
                {
                    lblStatus.Text = "Ezin zerbitzariarekin konektatu.";
                    btnLogin.IsEnabled = true;
                    btnRegister.IsEnabled = true;
                    return;
                }
            }

            // 2. Eskaera bidali
            var regData = new RegisterRequest { Username = user, Password = pass };
            var packet = new Packet
            {
                Type = PacketType.RegisterRequest,
                Message = PacketSerializer.SerializeData(regData)
            };

            await _server.SendPacketAsync(packet);

            // 3. Erantzuna itxaron
            Packet response = await _server.ReadPacketAsync();

            if (response != null && response.Type == PacketType.RegisterResponse)
            {
                if (response.Message == "OK")
                {
                    lblStatus.Text = "Erabiltzailea sortuta! Orain sartu jokora.";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Green;
                    // Hemen zuzenean ere egin genezake Login, baina hobe erabiltzaileak botoia ematea
                }
                else
                {
                    lblStatus.Text = "Errorea: Erabiltzailea existitzen da.";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }

            // Botoiak berriro aktibatu
            btnLogin.IsEnabled = true;
            btnRegister.IsEnabled = true;
        }

        private void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }
    }
}