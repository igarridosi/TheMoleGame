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
    public partial class WordsPanelWindow : Window
    {
        private ServerConnection _server;
        private const string NEW_CAT_OPTION = "+ Kategoria Berria Sortu...";

        // Constructor berria: Zerrenda jasotzen du
        public WordsPanelWindow(ServerConnection server, List<string> existingCategories)
        {
            InitializeComponent();
            _server = server;

            // 1. ComboBox bete
            foreach (string cat in existingCategories)
            {
                cmbCategory.Items.Add(cat);
            }
            // 2. Azken aukera gehitu
            cmbCategory.Items.Add(NEW_CAT_OPTION);

            // Lehenengoa aukeratu defektuz
            if (existingCategories.Count > 0) cmbCategory.SelectedIndex = 0;
        }

        // Dropdown aldatzean
        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCategory.SelectedItem == null) return;

            string selected = cmbCategory.SelectedItem.ToString();

            if (selected == NEW_CAT_OPTION)
            {
                // Berria sortu nahi du -> Erakutsi inputa
                pnlNewCategory.Visibility = Visibility.Visible;
                txtNewCategory.Focus();
            }
            else
            {
                // Dagoen bat aukeratu du -> Ezkutatu inputa
                pnlNewCategory.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string finalCategory = "";
            string word = txtWord.Text;

            // Kategoria zehaztu
            if (cmbCategory.SelectedItem.ToString() == NEW_CAT_OPTION)
            {
                finalCategory = txtNewCategory.Text;
            }
            else
            {
                finalCategory = cmbCategory.SelectedItem.ToString();
            }

            // Balidazioa
            if (string.IsNullOrWhiteSpace(finalCategory) || string.IsNullOrWhiteSpace(word))
            {
                lblStatus.Text = "Mesedez, bete kategoria eta hitza.";
                return;
            }

            btnAdd.IsEnabled = false;

            // Bidali
            var req = new NewWordRequest { Category = finalCategory, Word = word };
            var packet = new Packet
            {
                Type = PacketType.AddWordRequest,
                Message = PacketSerializer.SerializeData(req)
            };

            await _server.SendPacketAsync(packet);

            this.Close();
        }
    }
}
