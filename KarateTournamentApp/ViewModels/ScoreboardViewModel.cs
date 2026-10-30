using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for public scoreboard display
    /// </summary>
    public class ScoreboardViewModel : ViewModelBase
    {
        private readonly CompetitionManagerViewModel _competitionManager;
        private readonly System.Timers.Timer _timer;

        public CompetitionManagerViewModel CompetitionManager => _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public Category Category => _competitionManager.Category;
        public IReadOnlyList<Match> BracketMatches => _competitionManager.Category.BracketMatches;
        public Match? ThirdPlaceMatch => _competitionManager.Category.ThirdPlaceMatch;
        public Match? CurrentMatch => _competitionManager.CurrentMatch;
        public int CurrentMatchIndex => _competitionManager.CurrentMatchIndex;
        public bool IsThirdPlaceMatch => _competitionManager.IsThirdPlaceMatch;
        public int BracketRevision { get; private set; }

        public string AkaName => _competitionManager.AkaParticipant?.FullName ?? "---";
        public string ShiroName => _competitionManager.ShiroParticipant?.FullName ?? "---";

        public int AkaScore => _competitionManager.CurrentMatch?.AkaScore ?? 0;
        public int ShiroScore => _competitionManager.CurrentMatch?.ShiroScore ?? 0;

        public string TimeDisplay
        {
            get
            {
                if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
                {
                    var minutes = (int)(shobuMatch.TimeRemaining / 60);
                    var seconds = (int)(shobuMatch.TimeRemaining % 60);
                    return $"{minutes:D2}:{seconds:D2}";
                }
                return "";
            }
        }

        public int AkaAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch 
            ? shobuMatch.AtenaiAka : 0;

        public int AkaChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch
            ? shobuMatch.ChukokuAka : 0;

        public int ShiroAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch 
            ? shobuMatch.AtenaiShiro : 0;

        public int ShiroChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch
            ? shobuMatch.ChukokuShiro : 0;

        public bool AkaHansoku => AkaAtenai >= 3 || AkaChukoku >= 4;
        public bool ShiroHansoku => ShiroAtenai >= 3 || ShiroChukoku >= 4;

        public bool IsShobuSanbon => _competitionManager.Category.CategoryType == CategoryType.Kumite;

        private Participant? FirstPlaceParticipant =>
            _competitionManager.Category.FinalResults.ElementAtOrDefault(0)?.Participant
            ?? FindParticipant(GetFinalWinnerId());

        private Participant? SecondPlaceParticipant =>
            _competitionManager.Category.FinalResults.ElementAtOrDefault(1)?.Participant
            ?? FindParticipant(GetFinalLoserId());

        private Participant? ThirdPlaceParticipant =>
            _competitionManager.Category.FinalResults.ElementAtOrDefault(2)?.Participant
            ?? FindParticipant(_competitionManager.Category.ThirdPlaceMatch?.WinnerId);

        public string FirstPlaceName => FirstPlaceParticipant?.FullName ?? "Ожидается финал";
        public string FirstPlaceCountry => FirstPlaceParticipant?.Country ?? string.Empty;
        public string SecondPlaceName => SecondPlaceParticipant?.FullName ?? "Ожидается финал";
        public string SecondPlaceCountry => SecondPlaceParticipant?.Country ?? string.Empty;
        public string ThirdPlaceName => ThirdPlaceParticipant?.FullName ?? GetThirdPlacePlaceholder();
        public string ThirdPlaceCountry => ThirdPlaceParticipant?.Country ?? string.Empty;

        private string GetThirdPlacePlaceholder()
        {
            if (_competitionManager.Category.Participants.Count < 3)
                return "Нет третьего места";

            return _competitionManager.Category.ThirdPlaceMatch != null
                ? "Ожидается бой за бронзу"
                : "Ожидается определение";
        }

        private int? GetFinalWinnerId() =>
            _competitionManager.Category.BracketMatches.FirstOrDefault()?.WinnerId;

        private int? GetFinalLoserId()
        {
            var final = _competitionManager.Category.BracketMatches.FirstOrDefault();
            if (final?.WinnerId is not int winnerId) return null;
            return final.Aka == winnerId ? final.Shiro : final.Aka;
        }

        private Participant? FindParticipant(int? id) => id.HasValue
            ? _competitionManager.Category.Participants.FirstOrDefault(participant => participant.Id == id.Value)
            : null;

        public ScoreboardViewModel(CompetitionManagerViewModel competitionManager)
        {
            _competitionManager = competitionManager;

            // Setup timer for Kumite / Shobu Sanbon mode
            if (IsShobuSanbon)
            {
                _timer = new System.Timers.Timer(100); // Update every 100ms
                _timer.Elapsed += OnTimerElapsed;
            }

            // Subscribe to changes in competition manager
            _competitionManager.PropertyChanged += (s, e) => RefreshAll();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch && shobuMatch.IsRunning)
            {
                shobuMatch.TimeRemaining = Math.Max(0, shobuMatch.TimeRemaining - 0.1);
                OnPropertyChanged(nameof(TimeDisplay));

                if (shobuMatch.TimeRemaining <= 0)
                {
                    StopTimer();
                }
            }
        }

        public void StartTimer()
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = true;
                _timer?.Start();
            }
        }

        public void StopTimer()
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = false;
                _timer?.Stop();
            }
        }

        /// <summary>Refreshes the public bracket view from the shared category model.</summary>
        public void RefreshBracketSnapshot() => RefreshAll();

        private void RefreshAll()
        {
            BracketRevision++;
            OnPropertyChanged(nameof(BracketRevision));
            OnPropertyChanged(nameof(BracketMatches));
            OnPropertyChanged(nameof(ThirdPlaceMatch));
            OnPropertyChanged(nameof(CurrentMatch));
            OnPropertyChanged(nameof(CurrentMatchIndex));
            OnPropertyChanged(nameof(IsThirdPlaceMatch));
            OnPropertyChanged(nameof(AkaName));
            OnPropertyChanged(nameof(ShiroName));
            OnPropertyChanged(nameof(AkaScore));
            OnPropertyChanged(nameof(ShiroScore));
            OnPropertyChanged(nameof(AkaAtenai));
            OnPropertyChanged(nameof(AkaChukoku));
            OnPropertyChanged(nameof(ShiroAtenai));
            OnPropertyChanged(nameof(ShiroChukoku));
            OnPropertyChanged(nameof(AkaHansoku));
            OnPropertyChanged(nameof(ShiroHansoku));
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(IsShobuSanbon));
            OnPropertyChanged(nameof(FirstPlaceName));
            OnPropertyChanged(nameof(FirstPlaceCountry));
            OnPropertyChanged(nameof(SecondPlaceName));
            OnPropertyChanged(nameof(SecondPlaceCountry));
            OnPropertyChanged(nameof(ThirdPlaceName));
            OnPropertyChanged(nameof(ThirdPlaceCountry));
        }
    }
}
