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
    public partial class UserProfileWindow : Window
    {
        // Constructor-ean datuak jaso eta bete
        public UserProfileWindow(string username, UserStats stats)
        {
            InitializeComponent();
            lblUsername.Text = username;

            txtGames.Text = stats.GamesPlayed.ToString();
            txtWins.Text = stats.GamesWon.ToString();

            // Ehunekoak kalkulatu
            double winRate = stats.GamesPlayed > 0 ? (double)stats.GamesWon / stats.GamesPlayed * 100 : 0;
            txtWinRate.Text = $"Win Rate: {winRate:F1}%";

            txtImpGames.Text = stats.ImpostorCount.ToString();
            txtImpWins.Text = $"Irabaziak: {stats.ImpostorWins}";
        }
    }
}
