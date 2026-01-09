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
    public partial class AdminUsersWindow : Window
    {
        private ServerConnection _server;
        private string _myUsername; // Nire izena gordetzeko

        // Constructor-a eguneratu: Nire izena ere pasatu behar dugu konparatzeko
        public AdminUsersWindow(ServerConnection server, string myUsername)
        {
            InitializeComponent();
            _server = server;
            _myUsername = myUsername;
            LoadUsers();
        }

        private async void LoadUsers()
        {
            await _server.SendPacketAsync(new Packet { Type = PacketType.GetUserListRequest });
        }

        public void UpdateList(List<User> users)
        {
            gridUsers.ItemsSource = null; // Garbitu
            gridUsers.ItemsSource = users; // Berriro kargatu
        }

        private async void BtnToggleBan_Click(object sender, RoutedEventArgs e)
        {
            // 1. Datuak lortu ilaratik
            var user = ((FrameworkElement)sender).DataContext as User;
            if (user == null) return;

            // 2. SEGURTASUNA: Norbere burua edo Moderatzaile nagusia ez ukitu
            if (user.Username == _myUsername)
            {
                MessageBox.Show("Ezin duzu zeure burua blokeatu!", "Errorea", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (user.Username == "moderator")
            {
                MessageBox.Show("Ezin duzu Moderatzaile nagusia blokeatu.", "Debekatua");
                return;
            }

            // 3. EGOERA ALDATU (Ban <-> Unban)
            user.IsBanned = !user.IsBanned;

            // 4. SERVERRARI BIDALI
            var packet = new Packet
            {
                Type = PacketType.BanUserRequest,
                Message = PacketSerializer.SerializeData(user)
            };
            await _server.SendPacketAsync(packet);

            // 5. ZERRENDA FRESKATU (GARRANTZITSUA!)
            // Serverrak DBan gordetzen duenean, baliteke denbora pixka bat behar izatea.
            // Baina bisualki aldatzeko, zuzenean UI freskatuko dugu orain.
            gridUsers.Items.Refresh();

            string status = user.IsBanned ? "BLOKEATUA (Banned)" : "DESBLOKEATUA (Active)";
            MessageBox.Show($"Erabiltzailea eguneratuta: {user.Username}\nEgoera berria: {status}");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }
    }
}
