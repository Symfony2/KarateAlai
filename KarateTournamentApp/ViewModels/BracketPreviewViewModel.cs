using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// Read-only preview of a Kumite bracket. It shares the category's live match objects,
    /// so changes made by the judge window are reflected while this preview is open.
    /// </summary>
    public sealed class BracketPreviewViewModel : ViewModelBase, IDisposable
    {
        private readonly DispatcherTimer _refreshTimer;
        private bool _disposed;

        public Category Category { get; }
        public ScoreboardViewModel Scoreboard { get; }
        public ObservableCollection<BracketRoundDisplay> Rounds { get; } = new();

        public string CategoryName => Category.Name;
        public string DisciplineName => "Кумитэ";
        public string SexName => Category.Sex switch
        {
            Sex.Male => "Мужской", Sex.Female => "Женский", _ => "Смешанный"
        };
        public int ParticipantCount => Category.Participants.Count;
        public string TournamentState => Category.IsFinished ? "Соревнование завершено" : "Соревнование продолжается";
        public string FirstPlaceName => GetFinalWinner()?.FullName ?? "Определяется";
        public string FirstPlaceCountry => GetFinalWinner()?.Country ?? string.Empty;
        public string SecondPlaceName => GetFinalLoser()?.FullName ?? "Определяется";
        public string SecondPlaceCountry => GetFinalLoser()?.Country ?? string.Empty;
        public string ThirdPlaceName => GetThirdPlaceWinner()?.FullName ?? GetThirdPlacePlaceholder();
        public string ThirdPlaceCountry => GetThirdPlaceWinner()?.Country ?? string.Empty;

        public BracketPreviewViewModel(Category category, IDialogService dialogService)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            if (dialogService == null) throw new ArgumentNullException(nameof(dialogService));

            // CompetitionManagerViewModel upgrades legacy match objects if required, without
            // changing an already published draw. It also ensures the bronze bout when ready.
            var competitionManager = new CompetitionManagerViewModel(Category, dialogService);
            Scoreboard = new ScoreboardViewModel(competitionManager);
            RebuildSnapshot();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _refreshTimer.Tick += (_, _) => RebuildSnapshot();
            _refreshTimer.Start();
        }

        private void RebuildSnapshot()
        {
            if (_disposed) return;

            Scoreboard.RefreshBracketSnapshot();
            Rounds.Clear();

            var matches = Category.BracketMatches;
            if (matches.Count > 0)
            {
                var slotCount = matches.Count + 1;
                var powerOfTwo = 1;
                while (powerOfTwo < slotCount) powerOfTwo <<= 1;

                if (powerOfTwo == slotCount)
                {
                    var roundCount = 0;
                    for (var value = powerOfTwo; value > 1; value >>= 1) roundCount++;
                    var matchNumber = 1;

                    for (var round = 0; round < roundCount; round++)
                    {
                        var count = powerOfTwo >> (round + 1);
                        var firstIndex = count - 1;
                        var roundDisplay = new BracketRoundDisplay(GetRoundName(count));

                        for (var i = 0; i < count; i++)
                        {
                            var index = firstIndex + i;
                            if (index >= matches.Count) continue;
                            roundDisplay.Matches.Add(CreateMatchDisplay(matchNumber++, matches[index]));
                        }

                        Rounds.Add(roundDisplay);
                    }
                }
                else
                {
                    var fallbackRound = new BracketRoundDisplay("Поединки");
                    for (var i = matches.Count - 1; i >= 0; i--)
                        fallbackRound.Matches.Add(CreateMatchDisplay(fallbackRound.Matches.Count + 1, matches[i]));
                    Rounds.Add(fallbackRound);
                }
            }

            if (Category.ThirdPlaceMatch != null)
            {
                var bronzeRound = new BracketRoundDisplay("Бой за 3-е место");
                bronzeRound.Matches.Add(CreateMatchDisplay(1, Category.ThirdPlaceMatch));
                Rounds.Add(bronzeRound);
            }

            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(SexName));
            OnPropertyChanged(nameof(ParticipantCount));
            OnPropertyChanged(nameof(TournamentState));
            OnPropertyChanged(nameof(FirstPlaceName));
            OnPropertyChanged(nameof(FirstPlaceCountry));
            OnPropertyChanged(nameof(SecondPlaceName));
            OnPropertyChanged(nameof(SecondPlaceCountry));
            OnPropertyChanged(nameof(ThirdPlaceName));
            OnPropertyChanged(nameof(ThirdPlaceCountry));
        }

        private BracketMatchDisplay CreateMatchDisplay(int number, Match match)
        {
            var aka = FindParticipant(match.Aka);
            var shiro = FindParticipant(match.Shiro);
            var winner = FindParticipant(match.WinnerId);

            string akaName = aka?.FullName ?? (match.IsFinished && match.WinnerId.HasValue ? "Свободный проход" : "Ожидается соперник");
            string shiroName = shiro?.FullName ?? (match.IsFinished && match.WinnerId.HasValue ? "Свободный проход" : "Ожидается соперник");
            string score = aka != null && shiro != null ? $"{match.AkaScore} : {match.ShiroScore}" : "—";
            string status = match.IsFinished
                ? (winner != null ? $"Победитель: {winner.FullName}" : "Завершён")
                : (aka != null && shiro != null ? "Ожидает результата" : "Ожидает соперника");

            return new BracketMatchDisplay(number, akaName, aka?.Country ?? string.Empty,
                shiroName, shiro?.Country ?? string.Empty, score, status, match.IsFinished);
        }

        private static string GetRoundName(int matchCount)
        {
            if (matchCount == 1) return "Финал";
            if (matchCount == 2) return "Полуфиналы";
            if (matchCount == 4) return "Четвертьфиналы";
            return $"1/{matchCount} финала";
        }

        private Participant? FindParticipant(int? id) => id.HasValue
            ? Category.Participants.FirstOrDefault(participant => participant.Id == id.Value)
            : null;

        private Participant? GetFinalWinner() => Category.BracketMatches.Count > 0
            ? FindParticipant(Category.BracketMatches[0].WinnerId)
            : null;

        private Participant? GetFinalLoser()
        {
            if (Category.BracketMatches.Count == 0) return null;
            var final = Category.BracketMatches[0];
            if (!final.WinnerId.HasValue || !final.Aka.HasValue || !final.Shiro.HasValue) return null;
            var loserId = final.WinnerId.Value == final.Aka.Value ? final.Shiro : final.Aka;
            return FindParticipant(loserId);
        }

        private Participant? GetThirdPlaceWinner() => FindParticipant(Category.ThirdPlaceMatch?.WinnerId);

        private string GetThirdPlacePlaceholder()
        {
            if (Category.Participants.Count < 3) return "Не разыгрывается";
            if (Category.ThirdPlaceMatch == null) return "Определяется";
            return "Ожидается бой за бронзу";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _refreshTimer.Stop();
        }
    }

    public sealed class BracketRoundDisplay
    {
        public string Name { get; }
        public ObservableCollection<BracketMatchDisplay> Matches { get; } = new();
        public BracketRoundDisplay(string name) => Name = name;
    }

    public sealed class BracketMatchDisplay
    {
        public int Number { get; }
        public string AkaName { get; }
        public string AkaCountry { get; }
        public string ShiroName { get; }
        public string ShiroCountry { get; }
        public string Score { get; }
        public string Status { get; }
        public bool IsFinished { get; }

        public BracketMatchDisplay(int number, string akaName, string akaCountry,
            string shiroName, string shiroCountry, string score, string status, bool isFinished)
        {
            Number = number;
            AkaName = akaName;
            AkaCountry = akaCountry;
            ShiroName = shiroName;
            ShiroCountry = shiroCountry;
            Score = score;
            Status = status;
            IsFinished = isFinished;
        }
    }
}
