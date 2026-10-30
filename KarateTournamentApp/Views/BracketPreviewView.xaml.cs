using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using KarateTournamentApp.ViewModels;

namespace KarateTournamentApp.Views
{
    public partial class BracketPreviewView : UserControl
    {
        public BracketPreviewView()
        {
            InitializeComponent();
        }

        private void BracketPreviewView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }

        private void ExportPdfOrPrint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BracketPreviewViewModel viewModel)
                return;

            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.PrintTicket != null)
                    printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

                if (printDialog.ShowDialog() != true)
                    return;

                if (printDialog.PrintTicket != null)
                    printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

                var document = BuildPrintableReport(
                    viewModel,
                    Math.Max(500, printDialog.PrintableAreaWidth),
                    Math.Max(400, printDialog.PrintableAreaHeight));

                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)document).DocumentPaginator,
                    $"Протокол — {viewModel.CategoryName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось подготовить документ к печати или PDF-экспорту.\n\n{ex.Message}",
                    "Ошибка печати",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static FlowDocument BuildPrintableReport(BracketPreviewViewModel vm, double pageWidth, double pageHeight)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                FontSize = 9,
                PageWidth = pageWidth,
                PageHeight = pageHeight,
                PagePadding = new Thickness(34),
                ColumnWidth = double.PositiveInfinity,
                IsColumnWidthFlexible = false
            };

            var title = new Paragraph(new Run("ПРОТОКОЛ ТУРНИРНОЙ СЕТКИ"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#172B45"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            document.Blocks.Add(title);

            var category = new Paragraph(new Run(vm.CategoryName))
            {
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#34465F"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            document.Blocks.Add(category);

            var info = new Paragraph(new Run(
                $"Дисциплина: Кумитэ    Пол: {vm.SexName}    Участников: {vm.ParticipantCount}    Дата: {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                Foreground = BrushFromHex("#5D6D7E"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            document.Blocks.Add(info);

            AddSectionTitle(document, "ПРИЗЁРЫ");
            var podium = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
            podium.Columns.Add(new TableColumn { Width = new GridLength(90) });
            podium.Columns.Add(new TableColumn { Width = new GridLength(300) });
            podium.Columns.Add(new TableColumn { Width = new GridLength(180) });
            var podiumRows = new TableRowGroup();
            podiumRows.Rows.Add(CreateTableRow(new[] { "Место", "Спортсмен", "Страна" }, true, false));
            podiumRows.Rows.Add(CreateTableRow(new[] { "1 · Золото", vm.FirstPlaceName, vm.FirstPlaceCountry }, false, false));
            podiumRows.Rows.Add(CreateTableRow(new[] { "2 · Серебро", vm.SecondPlaceName, vm.SecondPlaceCountry }, false, true));
            podiumRows.Rows.Add(CreateTableRow(new[] { "3 · Бронза", vm.ThirdPlaceName, vm.ThirdPlaceCountry }, false, false));
            podium.RowGroups.Add(podiumRows);
            document.Blocks.Add(podium);

            foreach (var round in vm.Rounds)
            {
                AddSectionTitle(document, round.Name.ToUpperInvariant());
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
                table.Columns.Add(new TableColumn { Width = new GridLength(48) });
                table.Columns.Add(new TableColumn { Width = new GridLength(220) });
                table.Columns.Add(new TableColumn { Width = new GridLength(65) });
                table.Columns.Add(new TableColumn { Width = new GridLength(220) });
                table.Columns.Add(new TableColumn { Width = new GridLength(230) });

                var rows = new TableRowGroup();
                rows.Rows.Add(CreateTableRow(new[] { "№", "АКА · красный", "Счёт", "СИРО · белый", "Результат" }, true, false));
                foreach (var match in round.Matches)
                {
                    rows.Rows.Add(CreateTableRow(new[]
                    {
                        match.Number.ToString(),
                        FormatParticipant(match.AkaName, match.AkaCountry),
                        match.Score,
                        FormatParticipant(match.ShiroName, match.ShiroCountry),
                        match.Status
                    }, false, match.Number % 2 == 0));
                }
                table.RowGroups.Add(rows);
                document.Blocks.Add(table);
            }

            var footer = new Paragraph(new Run("Документ сформирован программой KarateTournamentApp."))
            {
                FontSize = 8,
                Foreground = BrushFromHex("#7B8794"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            document.Blocks.Add(footer);
            return document;
        }

        private static void AddSectionTitle(FlowDocument document, string text)
        {
            document.Blocks.Add(new Paragraph(new Run(text))
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromHex("#1769AA"),
                Margin = new Thickness(0, 4, 0, 6)
            });
        }

        private static TableRow CreateTableRow(string[] values, bool isHeader, bool alternate)
        {
            var row = new TableRow
            {
                Background = isHeader
                    ? BrushFromHex("#243A55")
                    : alternate ? BrushFromHex("#F0F4F8") : Brushes.White
            };

            foreach (var value in values)
            {
                var paragraph = new Paragraph(new Run(string.IsNullOrWhiteSpace(value) ? "—" : value))
                {
                    Margin = new Thickness(0),
                    FontSize = isHeader ? 8 : 8.5,
                    FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isHeader ? Brushes.White : BrushFromHex("#26384D")
                };
                row.Cells.Add(new TableCell(paragraph)
                {
                    Padding = new Thickness(5, 4, 5, 4),
                    BorderBrush = BrushFromHex("#D6DEE8"),
                    BorderThickness = new Thickness(0, 0, 1, 1)
                });
            }

            return row;
        }

        private static string FormatParticipant(string name, string country)
        {
            return string.IsNullOrWhiteSpace(country) ? name : $"{name} ({country})";
        }

        
        private static SolidColorBrush BrushFromHex(string value) =>
            new((Color)ColorConverter.ConvertFromString(value)!);
    }
}
