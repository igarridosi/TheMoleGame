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
    public partial class CreateUserWindow : Window
    {
        private ServerConnection _server;

        public CreateUserWindow(ServerConnection server)
        {
            InitializeComponent();
            _server = server;
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string user = txtUser.Text;
            string pass = txtPass.Password;
            string role = (cmbRole.SelectedItem as ComboBoxItem).Content.ToString();

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Bete eremu guztiak.");
                return;
            }

            var req = new CreateUserRequest { Username = user, Password = pass, Role = role };
            var packet = new Packet
            {
                Type = PacketType.CreateUserRequest,
                Message = PacketSerializer.SerializeData(req)
            };

            await _server.SendPacketAsync(packet);

            // Oharra: Erantzuna GameWindow-ek jasoko du, baina hemen mezua atera dezakegu
            // edo leihoa itxi eta itxaron. Sinpleena: Itxi.
            this.Close();
        }
    }
}
