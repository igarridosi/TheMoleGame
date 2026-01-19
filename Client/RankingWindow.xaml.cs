using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
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
using System.Collections.Generic;
using System.IO;
using QuestPDF.Helpers;

namespace Client
{
    public partial class RankingWindow : Window
    {
        private RankingPayload _data;

        public RankingWindow(RankingPayload payload, bool isModerator)
        {
            InitializeComponent();
            _data = payload;

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

            if (isModerator) btnExportPdf.Visibility = Visibility.Visible;
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
                        page.DefaultTextStyle(x => x.FontSize(12));

                        // GOIBURUA
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("THE MOLE GAME").SemiBold().FontSize(20).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                                col.Item().Text("Jokoaren Estatistika Ofizialak").FontSize(10);
                            });
                            row.ConstantItem(100).Text(System.DateTime.Now.ToString("yyyy-MM-dd"));
                        });

                        // GORPUTZA (Taula)
                        page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                        {
                            // Zutabeak definitu
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(60);
                            });

                            // Goiburuak
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Jokalaria");
                                header.Cell().Element(CellStyle).Text("Jokatuta");
                                header.Cell().Element(CellStyle).Text("Irabazita");
                                header.Cell().Element(CellStyle).Text("Inp. Irabazi");
                                header.Cell().Element(CellStyle).Text("%");
                            });

                            // Datuak
                            foreach (var item in _data.List)
                            {
                                table.Cell().Element(CellStyle).Text(item.Username);
                                table.Cell().Element(CellStyle).Text(item.GamesPlayed.ToString());
                                table.Cell().Element(CellStyle).Text(item.TotalWins.ToString());
                                table.Cell().Element(CellStyle).Text(item.ImpostorWins.ToString());
                                table.Cell().Element(CellStyle).Text(item.WinRate);
                            }
                        });

                        // OINA
                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Orrialdea ");
                                x.CurrentPageNumber();
                            });
                    });
                });

                // Gorde
                string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "TheMole_Report.pdf");
                document.GeneratePdf(path);

                MessageBox.Show($"PDFa sortu da mahaigainean:\n{path}", "Esportatuta", MessageBoxButton.OK, MessageBoxImage.Information);

                // Automatikoki ireki (aukerakoa)
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } }.Start();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Errorea PDFa sortzean: " + ex.Message);
            }
        }

        // Estilo laguntzailea
        static IContainer CellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5);
        }
    }
}
