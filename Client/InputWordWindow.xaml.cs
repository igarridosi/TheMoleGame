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
    public partial class InputWordWindow : Window
    {
        private ServerConnection _server;

        // Constructor berria: ServerConnection jasotzen du
        public InputWordWindow(ServerConnection server)
        {
            InitializeComponent();
            _server = server;
            txtWord.Focus();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtWord.Text))
            {
                // LEIHOAK BERAK BIDALTZEN DU ORAIN
                var packet = new Packet
                {
                    Type = PacketType.SubmitGameWord,
                    Message = txtWord.Text
                };
                await _server.SendPacketAsync(packet);

                this.Close(); // Eta ixten da
            }
            else
            {
                MessageBox.Show("Idatzi zerbait mesedez.");
            }
        }

        private void TxtWord_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSend_Click(sender, e);
            }
        }
    }
}
