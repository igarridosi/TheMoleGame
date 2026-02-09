using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using QuestPDF.Helpers;
using IOPath = System.IO.Path; // ALIAS gehitu

namespace Client
{
    // UI-rako klase laguntzailea (Metrikak erakusteko)
    public class PlayerMetricDisplay
    {
        public string Username { get; set; }
        public string DetectiveScore { get; set; }  // Detektibe Sen (%)
        public string MartyrScore { get; set; }     // Martiria (%)
        public string CamouflageScore { get; set; } // Kamuflajea (Rondak)
    }

    public partial class RankingWindow : Window
    {
        private RankingPayload _data;
        private List<UserStatsWithName> _detailedStats; // Estatistika zehatzak

        public RankingWindow(RankingPayload payload, bool isModerator)
        {
            InitializeComponent();
            _data = payload;
            _detailedStats = payload.DetailedStats; // Zuzenean payload-etik lortu

            QuestPDF.Settings.License = LicenseType.Community;

            // 1. Zerrenda bete
            gridRanking.ItemsSource = _data.List;

            // 2. Txartelak bete (Stats)
            var stats = _data.Stats;
            if (stats != null)
            {
                txtTotalGames.Text = stats.TotalMatches.ToString();
                txtTopImpostor.Text = stats.TopImpostor;
                txtTopWins.Text = $"{stats.TopImpostorWins} garaipen";

                // Barra grafikoa doitu (Grid Length erabiliz)
                double impPercent = stats.ImpostorWinRate;
                double civPercent = 100 - impPercent;

                // Zabalera proportzionalak esleitu
                colImp.Width = new GridLength(impPercent, GridUnitType.Star);
                colCiv.Width = new GridLength(civPercent, GridUnitType.Star);

                txtBalance.Text = $"{impPercent:F0}% / {civPercent:F0}%";
            }

            // 3. METRIKA AURRERATUAK BETE
            LoadMetricsGrid();

            if (isModerator) btnExportPdf.Visibility = Visibility.Visible;
        }

        // METODO BERRIA: Metrika aurreratuak taula batean erakutsi
        private void LoadMetricsGrid()
        {
            Console.WriteLine($"[CLIENT] LoadMetricsGrid deiturik. _detailedStats = {(_detailedStats == null ? "NULL" : _detailedStats.Count + " jokalari")}");
            
            if (_detailedStats == null || !_detailedStats.Any())
            {
                Console.WriteLine("[CLIENT] OHARRA: _detailedStats hutsa dago, metrikak ez dira erakutsiko.");
                gridMetrics.ItemsSource = null;
                return;
            }

            var metricsList = new List<PlayerMetricDisplay>();

            foreach (var item in _detailedStats)
            {
                var stats = item.Stats;

                // 1. Detektibe Sen (Accuracy) - Herritarra denean, zuzen bozkatu dion inpostoreari
                double detective = stats.TotalVotesCast > 0
                    ? (double)stats.CorrectVotes / stats.TotalVotesCast * 100
                    : 0;

                // 2. Martiria - Herritarra denean, oker kanporatua izateko probabilitatea
                double martyr = stats.CivilianCount > 0
                    ? (double)stats.TimesEjectedAsCivilian / stats.CivilianCount * 100
                    : 0;

                // 3. Kamuflajea - Inpostorea denean, batez beste zenbat ronda iraun duen
                double camouflage = stats.ImpostorCount > 0
                    ? (double)stats.ImpostorRoundsSurvived / stats.ImpostorCount
                    : 0;

                Console.WriteLine($"[CLIENT] Metrika: {item.Username} - Det:{detective:F0}% Martyr:{martyr:F0}% Kamuf:{camouflage:F1}");

                metricsList.Add(new PlayerMetricDisplay
                {
                    Username = item.Username,
                    DetectiveScore = $"{detective:F0}%",
                    MartyrScore = $"{martyr:F0}%",
                    CamouflageScore = $"{camouflage:F1} ronda",
                });
            }

            Console.WriteLine($"[CLIENT] Metrika zerrenda sortu da: {metricsList.Count} erregistro. ItemsSource ezartzen...");
            gridMetrics.ItemsSource = metricsList;
        }

        // METODO BERRIA: Estatistika zehatzak eskatu (Moderatzaileak)
        public void LoadDetailedStats(List<UserStatsWithName> detailedStats)
        {
            _detailedStats = detailedStats;
            LoadMetricsGrid(); // Taula eguneratu
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // PDFa sortu
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(QuestPDF.Helpers.Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        // ====== GOIBURUA ======
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("🎭 THE MOLE GAME - TXOSTEN OFIZIALA")
                                   .SemiBold().FontSize(22).FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                                col.Item().PaddingTop(5).Text("Estatistika Profesionalak eta Analisi Sakona")
                                   .FontSize(11).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            });
                            row.ConstantItem(120).AlignRight().Column(col =>
                            {
                                col.Item().Text($"📅 {DateTime.Now:yyyy-MM-dd}").FontSize(10);
                                col.Item().Text($"🕐 {DateTime.Now:HH:mm}").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                            });
                        });

                        // ====== GORPUTZA ======
                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                        {
                            // 1️⃣ ESTATISTIKA GLOBALAK (Laburmena)
                            col.Item().PaddingBottom(15).Element(ComposeGlobalStats);

                            // 2️⃣ RANKING OROKORRA (Top 10)
                            col.Item().PaddingBottom(20).Element(ComposeRankingTable);

                            // 3️⃣ METRIKA AURRERATUAK (Bakarrik estatistika zehatzak badaude)
                            if (_detailedStats != null && _detailedStats.Any())
                            {
                                col.Item().PageBreak(); // Orrialde berria
                                col.Item().PaddingBottom(15).Element(ComposeAdvancedMetrics);
                            }

                            // 4️⃣ KONKLUSIOAK ETA GOMENDIOAK
                            col.Item().PageBreak();
                            col.Item().Element(ComposeConclusions);
                        });

                        // ====== OINA ======
                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Orrialdea ");
                                x.CurrentPageNumber();
                                x.Span(" / ");
                                x.TotalPages();
                            });
                    });
                });

                // Gorde
                string path = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                    $"TheMole_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                document.GeneratePdf(path);

                MessageBox.Show($"✅ PDFa sortu da mahaigainean:\n{path}", "Esportatuta", MessageBoxButton.OK, MessageBoxImage.Information);

                // Automatikoki ireki
                new System.Diagnostics.Process 
                { 
                    StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } 
                }.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Errorea PDFa sortzean:\n{ex.Message}", "Errorea", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== PDF ATALAK ==========

        void ComposeGlobalStats(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("📊 ESTATISTIKA GLOBALAK").SemiBold().FontSize(16).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                col.Item().PaddingTop(10).Row(row =>
                {
                    // Txartel 1: Partidak
                    row.RelativeItem().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                       .Background(QuestPDF.Helpers.Colors.Blue.Lighten4).Padding(10).Column(c =>
                    {
                        c.Item().Text("🎮 Partida Guztira").SemiBold().FontSize(11);
                        c.Item().Text(_data.Stats.TotalMatches.ToString()).FontSize(24).FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    });

                    row.ConstantItem(10); // Tartea

                    // Txartel 2: Top Inpostorea
                    row.RelativeItem().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                       .Background(QuestPDF.Helpers.Colors.Red.Lighten4).Padding(10).Column(c =>
                    {
                        c.Item().Text("🥷 Top Inpostorea").SemiBold().FontSize(11);
                        c.Item().Text(_data.Stats.TopImpostor).FontSize(16).FontColor(QuestPDF.Helpers.Colors.Red.Darken2);
                        c.Item().Text($"{_data.Stats.TopImpostorWins} Garaipen").FontSize(10);
                    });

                    row.ConstantItem(10);

                    // Txartel 3: Orekaturik
                    row.RelativeItem().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                       .Background(QuestPDF.Helpers.Colors.Green.Lighten4).Padding(10).Column(c =>
                    {
                        c.Item().Text("⚖️ Orekaturik").SemiBold().FontSize(11);
                        double imp = _data.Stats.ImpostorWinRate;
                        double civ = 100 - imp;
                        c.Item().Text($"Inp: {imp:F1}%").FontSize(12).FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                        c.Item().Text($"Her: {civ:F1}%").FontSize(12).FontColor(QuestPDF.Helpers.Colors.Green.Medium);
                    });
                });
            });
        }

        void ComposeRankingTable(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("🏆 RANKING OROKORRA (Top 10)").SemiBold().FontSize(16).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);  // #
                        columns.RelativeColumn(2);   // Jokalaria
                        columns.ConstantColumn(70);  // Jokatuta
                        columns.ConstantColumn(70);  // Irabazita
                        columns.ConstantColumn(70);  // Inp. Wins
                        columns.ConstantColumn(60);  // #
                    });

                    // Goiburuak
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
                        header.Cell().Element(HeaderStyle).Text("Jokalaria");
                        header.Cell().Element(HeaderStyle).Text("Jokatuta");
                        header.Cell().Element(HeaderStyle).Text("Irabazita");
                        header.Cell().Element(HeaderStyle).Text("Inp. Wins");
                        header.Cell().Element(HeaderStyle).Text("Win %");
                    });

                    // Datuak (Top 10)
                    int rank = 1;
                    foreach (var item in _data.List.Take(10))
                    {
                        var bgColor = rank <= 3 ? QuestPDF.Helpers.Colors.Yellow.Lighten3 : QuestPDF.Helpers.Colors.White;
                        
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(rank.ToString()).SemiBold();
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(item.Username);
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(item.GamesPlayed.ToString());
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(item.TotalWins.ToString());
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(item.ImpostorWins.ToString());
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(item.WinRate);
                        
                        rank++;
                    }
                });
            });
        }

        void ComposeAdvancedMetrics(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("📈 METRIKA AURRERATUAK (Analisi Sakona)").SemiBold().FontSize(16).FontColor(QuestPDF.Helpers.Colors.Purple.Medium);
                
                col.Item().PaddingTop(10).Text("Metrika hauek jokalari bakoitzaren gaitasun zehatzak neurtzen dituzte:")
                   .FontSize(9).Italic().FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);    // Jokalaria
                        columns.ConstantColumn(90);   // Detektibe Sen
                        columns.ConstantColumn(90);   // Martiria
                        columns.ConstantColumn(90);   // Kamuflajea
                    });

                    // Goiburuak
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Jokalaria");
                        header.Cell().Element(HeaderStyle).Text("🕵️ Detekt.");
                        header.Cell().Element(HeaderStyle).Text("💀 Martir.");
                        header.Cell().Element(HeaderStyle).Text("🥷 Kamuf.");
                    });

                    // Datuak - ALDATUTA: UserStatsWithName erabili
                    foreach (var item in _detailedStats.Take(15))
                    {
                        var stats = item.Stats;

                        // 1. Detektibe Sen (Accuracy)
                        double detective = stats.TotalVotesCast > 0 
                            ? (double)stats.CorrectVotes / stats.TotalVotesCast * 100 
                            : 0;

                        // 2. Martiria (Wrongly Ejected %)
                        double martyr = stats.CivilianCount > 0 
                            ? (double)stats.TimesEjectedAsCivilian / stats.CivilianCount * 100 
                            : 0;

                        // 3. Kamuflajea (Avg Rounds Survived)
                        double camouflage = stats.ImpostorCount > 0 
                            ? (double)stats.ImpostorRoundsSurvived / stats.ImpostorCount 
                            : 0;

                        // 4. Profila erabaki
                        table.Cell().Element(CellStyleDefault).Text(item.Username);
                        table.Cell().Element(CellStyleDefault).Text($"{detective:F0}%");
                        table.Cell().Element(CellStyleDefault).Text($"{martyr:F0}%");
                        table.Cell().Element(CellStyleDefault).Text($"{camouflage:F1}");
                    }
                });

                // Azalpenak
                col.Item().PaddingTop(15).Column(c =>
                {
                    c.Item().Text("ℹ️ Metrika Azalpenak:").SemiBold().FontSize(11);
                    c.Item().PaddingLeft(10).Text("• 🕵️ Detektibe Sen: Herritarra denean, zenbat zuzen bozkatu dion inpostoreari (%)").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                    c.Item().PaddingLeft(10).Text("• 💀 Martiria: Herritarra denean, zenbat aldiz kanporatu duten oker (%)").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                    c.Item().PaddingLeft(10).Text("• 🥷 Kamuflajea: Inpostorea denean, batez beste zenbat bozketa iraun ditu").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                });
            });
        }

        void ComposeConclusions(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("🎯 KONKLUSIOAK ETA GOMENDIOAK").SemiBold().FontSize(16).FontColor(QuestPDF.Helpers.Colors.Green.Darken2);
                
                col.Item().PaddingTop(10).Column(c =>
                {
                    // 1. Orekaren analisia
                    double impRate = _data.Stats.ImpostorWinRate;
                    string balanceAnalysis = impRate > 55 
                        ? "⚠️ Inpostoreek abantaila handia daukate. Gomendio: Herritarrei bozketa denbora gehiago eman."
                        : impRate < 45 
                        ? "⚠️ Herritarrek abantaila handia daukate. Gomendio: Inpostore rol-a errazteko aldaketak egin."
                        : "✅ Jokoa ondo orekaturik dago. Jarraitu horrela!";

                    c.Item().Text(balanceAnalysis).FontSize(10).FontColor(QuestPDF.Helpers.Colors.Blue.Darken1);

                    // 2. Top jokalaria
                    if (_data.List.Any())
                    {
                        var topPlayer = _data.List.First();
                        c.Item().PaddingTop(10).Text($"🏆 Jokalari onena: {topPlayer.Username} ({topPlayer.TotalWins} gareipen, {topPlayer.WinRate} win rate)")
                           .FontSize(10).SemiBold();
                    }
                });

                col.Item().PaddingTop(20).AlignCenter().Text("Mila esker jokatzeagatik! 🎭")
                   .SemiBold().FontSize(14).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
            });
        }

        // ========== ESTILO LAGUNTZAILEAK ==========

        static IContainer HeaderStyle(IContainer container)
        {
            return container.Border(1).BorderColor(QuestPDF.Helpers.Colors.Blue.Medium)
                .Background(QuestPDF.Helpers.Colors.Blue.Lighten3)
                .Padding(5).AlignCenter().AlignMiddle();
        }

        static IContainer CellStyle(IContainer container, string bgColor)
        {
            return container.Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                .Background(bgColor).Padding(5).AlignCenter().AlignMiddle();
        }

        static IContainer CellStyleDefault(IContainer container)
        {
            return CellStyle(container, QuestPDF.Helpers.Colors.White);
        }
    }
}
