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

        // DROPDOWN AUKERAK (XAML-tik irakurtzeko "public" eta "property" izan behar du)
        public List<string> AvailableRoles { get; set; } = new List<string> { "Player", "Moderator" };

        // Constructor-a eguneratu: Nire izena ere pasatu behar dugu konparatzeko
        public AdminUsersWindow(ServerConnection server, string myUsername)
        {
            InitializeComponent();
            _server = server;
            _myUsername = myUsername;

            // Context-a ezarri, XAML-ak "AvailableRoles" aurkitu dezan
            this.DataContext = this;

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

        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            // 1. Lortu erabiltzailea
            var user = ((FrameworkElement)sender).DataContext as User;
            if (user == null) return;

            // 2. SEGURTASUNA: Norbere burua edo Moderatzaile nagusia ez ezabatu
            if (user.Username == _myUsername)
            {
                MessageBox.Show("Ezin duzu zeure burua ezabatu!", "Errorea", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (user.Username == "moderator")
            {
                MessageBox.Show("Ezin duzu Moderatzaile nagusia ezabatu.", "Debekatua", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. BERRESPENA ESKATU
            var result = MessageBox.Show(
                $"Ziur zaude '{user.Username}' erabiltzailea BETIKO ezabatu nahi duzula?\n\n" +
                $"⚠️ ABISUA: Ekintza hau EZIN DA DESEGIN.\n" +
                $"Erabiltzailearen datu guztiak (estatistikak, historikoa...) ezabatuko dira.",
                "Erabiltzailea Ezabatu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // 4. BIDALI ESKABIDEA ZERBITZARIARI
            var packet = new Packet
            {
                Type = PacketType.DeleteUserRequest,
                Message = user.Username
            };
            await _server.SendPacketAsync(packet);

            // 5. ITXARON ERANTZUNAREN (Timeout: 3 segundu)
            await Task.Delay(500); // Zerbitzariak prozesatu dezala itxaron pixka bat

            // 6. FRESKATU ZERRENDA
            MessageBox.Show($"Erabiltzailea ezabatu da: {user.Username}", "Arrakasta", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadUsers();
        }

        private void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            CreateUserWindow win = new CreateUserWindow(_server);
            win.ShowDialog();

            // Leihoa ixtean, zerrenda freskatu
            LoadUsers();
        }

        // ROLA ALDATZEAN EXEKUTATZEN DENA
        private async void CmbRole_DropDownClosed(object sender, System.EventArgs e)
        {
            var comboBox = sender as ComboBox;
            var user = comboBox.DataContext as User;

            if (user == null) return;

            string newRole = comboBox.SelectedItem as string; // Hau aldatu

            // Rola bera bada, ez egin ezer
            // Oharra: 'user.Role' jada aldatu da Binding-agatik, beraz zaila da konparatzea.
            // Baina berdin dio, zerbitzariari bidaltzen diogu eta kito.

            var packet = new Packet
            {
                Type = PacketType.UpdateUserRoleRequest,
                Message = PacketSerializer.SerializeData(new UpdateRoleRequest
                {
                    Username = user.Username,
                    NewRole = newRole
                })
            };

            await _server.SendPacketAsync(packet);

            MessageBox.Show($"Rola eguneratua: {newRole}");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }
    }
}
