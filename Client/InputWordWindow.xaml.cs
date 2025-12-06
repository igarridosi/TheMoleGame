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
        public string EnteredWord { get; private set; }

        public InputWordWindow()
        {
            InitializeComponent();
            txtWord.Focus(); // Kurtsorea zuzenean idazteko prest
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtWord.Text))
            {
                EnteredWord = txtWord.Text;
                this.DialogResult = true; // Leihoa ondo itxi dela esateko
                this.Close();
            }
            else
            {
                MessageBox.Show("Mesedez, idatzi zerbait.");
            }
        }
    }
}
