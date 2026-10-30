using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using KarateTournamentApp.Models;
using KarateTournamentApp.ViewModels;

namespace KarateTournamentApp.Views
{
    /// <summary>
    /// Public tournament tree displayed above the live AKA / SHIRO scoreboard.
    /// The tree follows the binary-heap match layout used by Category.BracketMatches.
    /// </summary>
    public partial class BracketView : UserControl
    {
        private ScoreboardViewModel? _viewModel;
        private bool _refreshQueued;

        public BracketView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => QueueRebuild();
            Unloaded += (_, _) => DetachViewModel();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachViewModel();
            _viewModel = e.NewValue as ScoreboardViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            QueueRebuild();
        }

        private void DetachViewModel()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel = null;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(ScoreboardViewModel.BracketRevision)
                || e.PropertyName == nameof(ScoreboardViewModel.BracketMatches)
                || e.PropertyName == nameof(ScoreboardViewModel.ThirdPlaceMatch))
            {
                QueueRebuild();
            }
        }

        private void BracketCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e) => QueueRebuild();

        private void QueueRebuild()
        {
            if (_refreshQueued || !IsLoaded)
            {
                return;
            }

            _refreshQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _refreshQueued = false;
                RebuildBracket();
            }));
        }

        private void RebuildBracket()
        {
            BracketCanvas.Children.Clear();

            var width = BracketCanvas.ActualWidth;
            var height = BracketCanvas.ActualHeight;
            if (_viewModel == null || width < 100 || height < 80)
            {
                return;
            }

            var matches = _viewModel.BracketMatches;
            if (matches.Count == 0)
            {
                AddEmptyState(width, height, "Жеребьёвка ещё не создана", "Сначала сформируйте сетку категории.");
                return;
            }

            var slotCount = matches.Count + 1;
            var roundCount = 0;
            var powerOfTwo = 1;
            while (powerOfTwo < slotCount)
            {
                powerOfTwo <<= 1;
                roundCount++;
            }

            if (powerOfTwo != slotCount)
            {
                AddEmptyState(width, height, "Не удалось построить сетку", "Количество поединков не соответствует структуре олимпийской сетки.");
                return;
            }

            const double horizontalPadding = 10;
            const double headerHeight = 22;
            const double bottomPadding = 8;
            var availableHeight = Math.Max(1, height - headerHeight - bottomPadding);
            var slotPitch = availableHeight / slotCount;
            var columnWidth = Math.Max(1, (width - horizontalPadding * 2) / roundCount);
            var cardWidth = Math.Min(218, Math.Max(96, columnWidth * 0.76));
            var cardHeight = Math.Min(60, Math.Max(16, slotPitch * 1.48));
            var fontSize = Math.Clamp(cardHeight * 0.22, 7, 13);
            var positions = new Dictionary<int, MatchPosition>();

            // Calculate positions for all rounds from the first round to the final.
            for (var round = 0; round < roundCount; round++)
            {
                var matchCount = slotCount >> (round + 1);
                var firstIndex = matchCount - 1;
                var groupSize = 1 << (round + 1);
                var columnX = horizontalPadding + round * columnWidth;
                var cardX = columnX + (columnWidth - cardWidth) / 2;

                AddRoundLabel(columnX, RoundLabel(round, roundCount, matchCount), columnWidth);

                for (var i = 0; i < matchCount; i++)
                {
                    var matchIndex = firstIndex + i;
                    var centerY = headerHeight + (i * groupSize + groupSize / 2d) * slotPitch;
                    positions[matchIndex] = new MatchPosition(matchIndex, round, cardX, centerY, cardWidth, cardHeight);
                }
            }

            // Draw the winner progression connectors before the match cards.
            for (var round = 0; round < roundCount - 1; round++)
            {
                var matchCount = slotCount >> (round + 1);
                var firstIndex = matchCount - 1;
                for (var i = 0; i < matchCount; i++)
                {
                    var childIndex = firstIndex + i;
                    var parentIndex = (childIndex - 1) / 2;
                    if (!positions.TryGetValue(childIndex, out var child) || !positions.TryGetValue(parentIndex, out var parent))
                    {
                        continue;
                    }

                    var childMatch = matches[childIndex];
                    var parentMatch = matches[parentIndex];
                    var isPromoted = childMatch.WinnerId.HasValue &&
                        (childIndex % 2 != 0
                            ? parentMatch.Aka == childMatch.WinnerId
                            : parentMatch.Shiro == childMatch.WinnerId);
                    AddConnector(child, parent, isPromoted ? ColorFromHex("#39A982") : ColorFromHex("#52637B"), false);
                }
            }

            // Bronze-medal fight: the two semifinal losers feed into a separate bout.
            MatchPosition? bronzePosition = null;
            if (roundCount >= 2 && positions.TryGetValue(1, out var firstSemi) && positions.TryGetValue(2, out var secondSemi))
            {
                var finalPosition = positions[0];
                var bronzeCenterY = Math.Min(
                    height - cardHeight / 2 - 5,
                    finalPosition.CenterY + Math.Max(cardHeight * 1.7, availableHeight * 0.25));
                bronzePosition = new MatchPosition(-1, roundCount - 1, finalPosition.X, bronzeCenterY, cardWidth, cardHeight);

                AddLoserConnector(firstSemi, bronzePosition.Value, firstSemi.CenterY, bronzeCenterY - cardHeight * 0.18);
                AddLoserConnector(secondSemi, bronzePosition.Value, secondSemi.CenterY, bronzeCenterY + cardHeight * 0.18);
            }

            // Render normal match cards.
            foreach (var position in positions.Values.OrderBy(position => position.Round).ThenBy(position => position.CenterY))
            {
                var match = matches[position.Index];
                var akaPlaceholder = GetWaitingName(position.Index, true, matches.Count);
                var shiroPlaceholder = GetWaitingName(position.Index, false, matches.Count);
                var card = CreateMatchCard(
                    match,
                    $"ПОЕДИНОК {position.Index + 1}",
                    akaPlaceholder,
                    shiroPlaceholder,
                    ReferenceEquals(match, _viewModel.CurrentMatch),
                    position.Width,
                    position.Height,
                    fontSize);
                Canvas.SetLeft(card, position.X);
                Canvas.SetTop(card, position.CenterY - position.Height / 2);
                BracketCanvas.Children.Add(card);
            }

            if (bronzePosition.HasValue)
            {
                var bronze = _viewModel.ThirdPlaceMatch;
                var bronzeCard = CreateMatchCard(
                    bronze,
                    "БОЙ ЗА 3-Е МЕСТО",
                    "Проигравший полуфинала 1",
                    "Проигравший полуфинала 2",
                    _viewModel.IsThirdPlaceMatch || (bronze != null && ReferenceEquals(bronze, _viewModel.CurrentMatch)),
                    bronzePosition.Value.Width,
                    bronzePosition.Value.Height,
                    fontSize);
                Canvas.SetLeft(bronzeCard, bronzePosition.Value.X);
                Canvas.SetTop(bronzeCard, bronzePosition.Value.CenterY - bronzePosition.Value.Height / 2);
                BracketCanvas.Children.Add(bronzeCard);
            }
        }

        private string GetWaitingName(int matchIndex, bool aka, int totalMatches)
        {
            var participantId = aka
                ? _viewModel!.BracketMatches[matchIndex].Aka
                : _viewModel!.BracketMatches[matchIndex].Shiro;
            if (participantId.HasValue)
            {
                return FindParticipantName(participantId.Value);
            }

            var childIndex = matchIndex * 2 + (aka ? 1 : 2);
            return childIndex < totalMatches
                ? $"Победитель боя {childIndex + 1}"
                : "Ожидает участника";
        }

        private string FindParticipantName(int participantId)
        {
            return _viewModel!.Category.Participants.FirstOrDefault(participant => participant.Id == participantId)?.FullName
                   ?? "Спортсмен не найден";
        }

        private Border CreateMatchCard(
            Match? match,
            string title,
            string akaPlaceholder,
            string shiroPlaceholder,
            bool isCurrent,
            double width,
            double height,
            double fontSize)
        {
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = BrushFromHex("#19283B"),
                BorderBrush = isCurrent ? BrushFromHex("#F5C451") : BrushFromHex("#3B4E68"),
                BorderThickness = new Thickness(isCurrent ? 2.5 : 1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(4),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 7,
                    ShadowDepth = 2,
                    Opacity = 0.24
                }
            };

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var status = match == null
                ? "ПОСЛЕ ПОЛУФИНАЛОВ"
                : match.IsFinished ? "ЗАВЕРШЁН" : isCurrent ? "СЕЙЧАС" : "ОЖИДАЕТ";
            var header = new Grid { Margin = new Thickness(2, 0, 2, 2) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleText = new TextBlock
            {
                Text = title,
                Foreground = BrushFromHex("#9FB1C8"),
                FontSize = Math.Max(6, fontSize * 0.72),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var statusText = new TextBlock
            {
                Text = status,
                Foreground = isCurrent ? BrushFromHex("#F5C451") : BrushFromHex("#7F91A8"),
                FontSize = Math.Max(6, fontSize * 0.68),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusText, 1);
            header.Children.Add(titleText);
            header.Children.Add(statusText);
            Grid.SetRow(header, 0);
            content.Children.Add(header);

            var hasBye = match != null && match.IsFinished && (match.Aka.HasValue ^ match.Shiro.HasValue);
            var akaName = match != null && match.Aka.HasValue
                ? FindParticipantName(match.Aka.Value)
                : hasBye ? "СВОБОДНЫЙ ПРОХОД" : akaPlaceholder;
            var shiroName = match != null && match.Shiro.HasValue
                ? FindParticipantName(match.Shiro.Value)
                : hasBye ? "СВОБОДНЫЙ ПРОХОД" : shiroPlaceholder;
            var akaWon = match != null && match.IsFinished && match.WinnerId.HasValue && match.WinnerId == match.Aka;
            var shiroWon = match != null && match.IsFinished && match.WinnerId.HasValue && match.WinnerId == match.Shiro;

            var akaRow = CreateParticipantRow("AKA", akaName, match?.AkaScore, akaWon, true, fontSize);
            var shiroRow = CreateParticipantRow("СИРО", shiroName, match?.ShiroScore, shiroWon, false, fontSize);
            Grid.SetRow(akaRow, 1);
            Grid.SetRow(shiroRow, 2);
            content.Children.Add(akaRow);
            content.Children.Add(shiroRow);
            border.Child = content;
            return border;
        }

        private static Border CreateParticipantRow(string side, string name, short? score, bool isWinner, bool isAka, double fontSize)
        {
            var row = new Grid { Margin = new Thickness(1, 1, 1, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

            var sideBadge = new Border
            {
                Background = isAka ? BrushFromHex("#B83845") : BrushFromHex("#E3E9F1"),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = side,
                    Foreground = isAka ? Brushes.White : BrushFromHex("#25344A"),
                    FontSize = Math.Max(6, fontSize * 0.67),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var nameText = new TextBlock
            {
                Text = name,
                Foreground = BrushFromHex("#F1F5FA"),
                FontSize = fontSize,
                FontWeight = isWinner ? FontWeights.Bold : FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = name
            };
            var scoreText = new TextBlock
            {
                Text = score.HasValue ? score.Value.ToString() : string.Empty,
                Foreground = isWinner ? BrushFromHex("#F5C451") : BrushFromHex("#DAE3EE"),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(sideBadge, 0);
            Grid.SetColumn(nameText, 1);
            Grid.SetColumn(scoreText, 2);
            row.Children.Add(sideBadge);
            row.Children.Add(nameText);
            row.Children.Add(scoreText);

            return new Border
            {
                Background = isWinner ? BrushFromHex("#194B3D") : (isAka ? BrushFromHex("#3A2530") : BrushFromHex("#293647")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(3, 1, 5, 1),
                Child = row
            };
        }

        private void AddRoundLabel(double x, string text, double columnWidth)
        {
            var label = new TextBlock
            {
                Text = text,
                Width = columnWidth,
                Foreground = BrushFromHex("#8296B1"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, 2);
            BracketCanvas.Children.Add(label);
        }

        private void AddConnector(MatchPosition child, MatchPosition parent, Color color, bool dashed)
        {
            var startX = child.X + child.Width;
            var endX = parent.X;
            var middleX = startX + (endX - startX) / 2;
            var line = new Polyline
            {
                Points = new PointCollection
                {
                    new(startX, child.CenterY),
                    new(middleX, child.CenterY),
                    new(middleX, parent.CenterY),
                    new(endX, parent.CenterY)
                },
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            };
            if (dashed)
            {
                line.StrokeDashArray = new DoubleCollection { 4, 3 };
            }
            BracketCanvas.Children.Add(line);
        }

        private void AddLoserConnector(MatchPosition semifinal, MatchPosition bronze, double startY, double targetY)
        {
            var startX = semifinal.X + semifinal.Width;
            var endX = bronze.X;
            var middleX = startX + (endX - startX) / 2;
            var line = new Polyline
            {
                Points = new PointCollection
                {
                    new(startX, startY),
                    new(middleX, startY),
                    new(middleX, targetY),
                    new(endX, targetY)
                },
                Stroke = BrushFromHex("#D2A74C"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0.72,
                IsHitTestVisible = false
            };
            BracketCanvas.Children.Add(line);
        }

        private void AddEmptyState(double width, double height, string title, string description)
        {
            var stack = new StackPanel
            {
                Width = Math.Min(width - 30, 480),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = BrushFromHex("#91A4BD"),
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 0),
                TextAlignment = TextAlignment.Center
            });
            Canvas.SetLeft(stack, (width - stack.Width) / 2);
            Canvas.SetTop(stack, Math.Max(0, (height - 58) / 2));
            BracketCanvas.Children.Add(stack);
        }

        private static string RoundLabel(int round, int roundCount, int matchCount)
        {
            if (round == roundCount - 1) return "ФИНАЛ";
            if (round == roundCount - 2) return "ПОЛУФИНАЛ";
            return $"1/{matchCount} ФИНАЛА";
        }

        
        private static SolidColorBrush BrushFromHex(string value) => new(ColorFromHex(value));

        private static Color ColorFromHex(string value) => (Color)ColorConverter.ConvertFromString(value)!;

        private readonly record struct MatchPosition(int Index, int Round, double X, double CenterY, double Width, double Height);
    }
}
