using Shared;
using System;
using System.Windows;

namespace Client
{
    public partial class UserProfileWindow : Window
    {
        // Constructor-ean datuak jaso eta bete
        public UserProfileWindow(string username, UserStats stats)
        {
            InitializeComponent();
            lblUsername.Text = username;

            // Goiko Txartelak
            txtGames.Text = stats.GamesPlayed.ToString();
            txtWins.Text = stats.GamesWon.ToString();

            double winRate = stats.GamesPlayed > 0 ? (double)stats.GamesWon / stats.GamesPlayed * 100 : 0;
            txtWinRate.Text = $"Win Rate: {winRate:F1}%";

            txtImpGames.Text = stats.ImpostorCount.ToString();
            txtImpWins.Text = $"Irabaziak: {stats.ImpostorWins}";

            // --- ANALITIKA (Datuak TextBlock-etan betetzen) ---

            // 1. Detektibe Sen (0-100%)
            double accuracy = stats.TotalVotesCast > 0 ? (double)stats.CorrectVotes / stats.TotalVotesCast * 100 : 0;
            txtDetective.Text = $"{accuracy:F0}%";

            // 2. Martiria (Zenbakia zuzena)
            txtMartyr.Text = stats.TimesEjectedAsCivilian.ToString();

            // 3. Kamuflajea (0-3 Rondak batez beste)
            double avgSurvival = stats.ImpostorCount > 0 ? (double)stats.ImpostorRoundsSurvived / stats.ImpostorCount : 0;
            txtCamo.Text = $"{avgSurvival:F1}";
        }
    }
}
