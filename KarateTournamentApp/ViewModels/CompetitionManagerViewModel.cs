using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// Manages the state of an ongoing competition/category
    /// </summary>
    public class CompetitionManagerViewModel : ViewModelBase
    {
        private const int MaxScoreToWin = 6;
        private const int AtenaiHansokuThreshold = 3;
        private const int ChukokuHansokuThreshold = 4;

        private readonly Category _category;
        private readonly IDialogService _dialogService;
        private const int ThirdPlaceMatchIndex = -1;
        private const int NoMatchIndex = -2;
        private int _currentMatchIndex;
        
        public Category Category => _category;
        public int CurrentMatchIndex => _currentMatchIndex;
        public bool IsThirdPlaceMatch => _currentMatchIndex == ThirdPlaceMatchIndex;
        
        private Match _currentMatch = null!;
        public Match CurrentMatch
        {
            get => _currentMatch;
            set
            {
                _currentMatch = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentMatchIndex));
                OnPropertyChanged(nameof(IsThirdPlaceMatch));
                OnPropertyChanged(nameof(AkaParticipant));
                OnPropertyChanged(nameof(ShiroParticipant));
                OnPropertyChanged(nameof(IsShobuSanbon));
                OnPropertyChanged(nameof(SenshuEnabled));
                OnPropertyChanged(nameof(HasSenshuAka));
                OnPropertyChanged(nameof(HasSenshuShiro));
                OnPropertyChanged(nameof(IsInOvertime));
            }
        }

        public bool IsShobuSanbon => _category.CategoryType == CategoryType.Kumite;

        // Senshu properties
        public bool SenshuEnabled
        {
            get => CurrentMatch is ShobuSanbonMatch match && match.SenshuEnabled;
            set
            {
                if (CurrentMatch is ShobuSanbonMatch match)
                {
                    match.SenshuEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasSenshuAka => CurrentMatch is ShobuSanbonMatch match && match.HasSenshuAka;
        public bool HasSenshuShiro => CurrentMatch is ShobuSanbonMatch match && match.HasSenshuShiro;
        public bool IsInOvertime => CurrentMatch is ShobuSanbonMatch match && match.IsInOvertime;

        public Participant? AkaParticipant => CurrentMatch?.Aka.HasValue == true 
            ? _category.Participants.FirstOrDefault(p => p.Id == CurrentMatch.Aka.Value)
            : null;

        public Participant? ShiroParticipant => CurrentMatch?.Shiro.HasValue == true
            ? _category.Participants.FirstOrDefault(p => p.Id == CurrentMatch.Shiro.Value)
            : null;

        public ICommand NextMatchCommand { get; }
        public ICommand FinishMatchCommand { get; }
        public ICommand StartOvertimeCommand { get; }
        public ICommand SetTimeCommand { get; }

        public CompetitionManagerViewModel(Category category, IDialogService dialogService)
        {
            _category = category;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            bool isKumiteCategory = _category.CategoryType == CategoryType.Kumite;
            bool hasLegacyMatchTypes = isKumiteCategory
                && _category.BracketMatches.Any(m => m is not ShobuSanbonMatch);
            bool hasNoPlayableMatches = isKumiteCategory
                && _category.BracketMatches.Any()
                && !_category.BracketMatches.Any(m => !m.IsFinished && m.Aka.HasValue && m.Shiro.HasValue)
                && _category.Participants.Count >= 2
                && !_category.IsFinished;

            if (!_category.BracketMatches.Any())
            {
                if (isKumiteCategory)
                {
                    InitializeKumiteBracketAsShobu(createNewBracket: true);
                }
                else
                {
                    _category.InitializeBracket();
                }
            }
            else if (hasNoPlayableMatches)
            {
                InitializeKumiteBracketAsShobu(createNewBracket: true);
            }
            else if (hasLegacyMatchTypes)
            {
                // Upgrade old generic matches without changing the already published draw.
                InitializeKumiteBracketAsShobu();
            }

            // Find first unfinished match
            _currentMatchIndex = FindNextUnfinishedMatch();
            CurrentMatch = GetMatchByIndex(_currentMatchIndex)!;

            NextMatchCommand = new RelayCommand(o => MoveToNextMatch(), o => CanMoveToNextMatch());
            FinishMatchCommand = new RelayCommand(o => FinishCurrentMatch(), o => CurrentMatch != null && !CurrentMatch.IsFinished);
            StartOvertimeCommand = new RelayCommand(o => StartOvertime(), o => CanStartOvertime());
            SetTimeCommand = new RelayCommand(SetTime, o => CurrentMatch is ShobuSanbonMatch);
        }

        private int FindNextUnfinishedMatch()
        {
            // Play all qualifying bouts from the first round through the semifinals.
            // The root (index 0) is deliberately held until the bronze-medal bout is complete.
            for (int i = _category.BracketMatches.Count - 1; i >= 1; i--)
            {
                var match = _category.BracketMatches[i];
                if (!match.IsFinished && match.Aka.HasValue && match.Shiro.HasValue)
                {
                    return i;
                }
            }

            _category.EnsureThirdPlaceMatch();
            if (_category.ThirdPlaceMatch is { IsFinished: false, Aka: not null, Shiro: not null })
            {
                return ThirdPlaceMatchIndex;
            }

            if (_category.BracketMatches.Count > 0)
            {
                var finalMatch = _category.BracketMatches[0];
                if (!finalMatch.IsFinished && finalMatch.Aka.HasValue && finalMatch.Shiro.HasValue)
                {
                    return 0;
                }
            }

            return NoMatchIndex;
        }

        private Match? GetMatchByIndex(int index)
        {
            if (index == ThirdPlaceMatchIndex)
            {
                return _category.ThirdPlaceMatch;
            }

            return index >= 0 && index < _category.BracketMatches.Count
                ? _category.BracketMatches[index]
                : null;
        }

        private bool CanMoveToNextMatch()
        {
            return CurrentMatch?.IsFinished == true;
        }

        private void MoveToNextMatch()
        {
            if (CurrentMatch?.IsFinished == true)
            {
                PromoteWinnerAndSelectNextMatch();
            }
        }

        private void FinishCurrentMatch()
        {
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                // Check for draw
                if (CurrentMatch.AkaScore == CurrentMatch.ShiroScore)
                {
                    if (CurrentMatch is ShobuSanbonMatch shobuMatch)
                    {
                        // Check Senshu if enabled
                        if (shobuMatch.SenshuEnabled && (shobuMatch.HasSenshuAka || shobuMatch.HasSenshuShiro))
                        {
                            // Winner by Senshu
                            var winnerId = shobuMatch.HasSenshuAka ? CurrentMatch.Aka : CurrentMatch.Shiro;
                            CompleteMatch(winnerId, false, true);
                            
                            _dialogService.ShowMessage(
                                $"Ничья! Победитель по СЭНШУ: {(shobuMatch.HasSenshuAka ? "АКА" : "СИРО")}",
                                "Решение по Сэнсю",
                                DialogButtons.Ok,
                                DialogIcon.Information);
                        }
                        else
                        {
                            // Draw without Senshu - need overtime
                            var result = _dialogService.ShowMessage(
                                "Ничья! Начать дополнительный поединок (+60 секунд)?",
                                "Дополнительный поединок",
                                DialogButtons.YesNo,
                                DialogIcon.Question);

                            if (result == DialogResult.Yes)
                            {
                                StartOvertime();
                                return; // Don't finish the match yet
                            }
                            else
                            {
                                // Manual decision or cancel
                                _dialogService.ShowMessage(
                                    "Поединок не завершён. Используйте окно разрешения ничьей или выберите победителя вручную.",
                                    "Внимание",
                                    DialogButtons.Ok,
                                    DialogIcon.Warning);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Non-Shobu Sanbon draw (shouldn't happen, but handle it)
                        _dialogService.ShowMessage("Ничья! Выберите победителя вручную.", "Ничья",
                            DialogButtons.Ok, DialogIcon.Warning);
                        return;
                    }
                }
                else
                {
                    // Determine winner based on score
                    if (CurrentMatch.AkaScore > CurrentMatch.ShiroScore)
                    {
                        CompleteMatch(CurrentMatch.Aka, false, true);
                    }
                    else
                    {
                        CompleteMatch(CurrentMatch.Shiro, false, true);
                    }
                }
                
                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        private bool CanStartOvertime()
        {
            return CurrentMatch is ShobuSanbonMatch match 
                   && !match.IsFinished 
                   && match.TimeRemaining <= 0
                   && match.AkaScore == match.ShiroScore;
        }

        private void StartOvertime()
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsInOvertime = true;
                shobuMatch.OvertimeCount++;
                shobuMatch.TimeRemaining = 60; // Add 60 seconds
                shobuMatch.IsRunning = false; // Stop timer, judge needs to start it manually
                
                // Reset Senshu for overtime (fresh start)
                shobuMatch.HasSenshuAka = false;
                shobuMatch.HasSenshuShiro = false;
                
                OnPropertyChanged(nameof(CurrentMatch));
                OnPropertyChanged(nameof(IsInOvertime));
                
                _dialogService.ShowMessage(
                    $"Дополнительный поединок № {shobuMatch.OvertimeCount} начался!\nДобавлено 60 секунд.\nСэнсю сброшено.",
                    "Дополнительный поединок",
                    DialogButtons.Ok,
                    DialogIcon.Information);
            }
        }

        private void SetTime(object parameter)
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                if (parameter is string timeText && double.TryParse(timeText, out double seconds))
                {
                    if (seconds >= 0 && seconds <= 600) // Max 10 minutes
                    {
                        shobuMatch.TimeRemaining = seconds;
                        shobuMatch.IsRunning = false; // Stop timer when manually setting time
                        OnPropertyChanged(nameof(CurrentMatch));
                        
                        System.Diagnostics.Debug.WriteLine($"Установлено время: {seconds} секунд");
                    }
                }
            }
        }

        public void UpdateMatchScore(bool isAka, int points)
        {
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                // Check if this is the first point scored (for Senshu)
                if (CurrentMatch is ShobuSanbonMatch shobuMatch && shobuMatch.SenshuEnabled)
                {
                    bool isFirstPoint = shobuMatch.AkaScore == 0 && shobuMatch.ShiroScore == 0;
                    
                    if (isFirstPoint && points > 0)
                    {
                        if (isAka)
                        {
                            shobuMatch.HasSenshuAka = true;
                            System.Diagnostics.Debug.WriteLine("Сэнсю для АКА — первое очко!");
                        }
                        else
                        {
                            shobuMatch.HasSenshuShiro = true;
                            System.Diagnostics.Debug.WriteLine("Сэнсю для СИРО — первое очко!");
                        }
                        
                        OnPropertyChanged(nameof(HasSenshuAka));
                        OnPropertyChanged(nameof(HasSenshuShiro));
                    }
                }
                
                // Update score
                if (isAka)
                {
                    CurrentMatch.AkaScore += (short)points;
                    if (CurrentMatch.AkaScore < 0) CurrentMatch.AkaScore = 0;

                    if (CurrentMatch.AkaScore >= MaxScoreToWin)
                    {
                        CompleteMatch(CurrentMatch.Aka, false, true);
                    }
                }
                else
                {
                    CurrentMatch.ShiroScore += (short)points;
                    if (CurrentMatch.ShiroScore < 0) CurrentMatch.ShiroScore = 0;

                    if (CurrentMatch.ShiroScore >= MaxScoreToWin)
                    {
                        CompleteMatch(CurrentMatch.Shiro, false, true);
                    }
                }
                
                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        public void UpdatePenalty(bool isAka, PenaltyType penaltyType, int change)
        {
            if (CurrentMatch is ShobuSanbonMatch shobuMatch && !CurrentMatch.IsFinished)
            {
                if (isAka)
                {
                    if (penaltyType == PenaltyType.Atenai)
                    {
                        shobuMatch.AtenaiAka = Math.Max(0, shobuMatch.AtenaiAka + change);
                    }
                    else
                    {
                        shobuMatch.ChukokuAka = Math.Max(0, shobuMatch.ChukokuAka + change);
                    }
                }
                else
                {
                    if (penaltyType == PenaltyType.Atenai)
                    {
                        shobuMatch.AtenaiShiro = Math.Max(0, shobuMatch.AtenaiShiro + change);
                    }
                    else
                    {
                        shobuMatch.ChukokuShiro = Math.Max(0, shobuMatch.ChukokuShiro + change);
                    }
                }

                bool akaHansoku = shobuMatch.AtenaiAka >= AtenaiHansokuThreshold
                    || shobuMatch.ChukokuAka >= ChukokuHansokuThreshold;
                bool shiroHansoku = shobuMatch.AtenaiShiro >= AtenaiHansokuThreshold
                    || shobuMatch.ChukokuShiro >= ChukokuHansokuThreshold;

                if (akaHansoku)
                {
                    CompleteMatch(CurrentMatch.Shiro, true, true);
                    _dialogService.ShowMessage(
                        "АКА получает ХАНСОКУ. Побеждает СИРО.",
                        "Дисквалификация",
                        DialogButtons.Ok,
                        DialogIcon.Information);
                }
                else if (shiroHansoku)
                {
                    CompleteMatch(CurrentMatch.Aka, true, true);
                    _dialogService.ShowMessage(
                        "СИРО получает ХАНСОКУ. Побеждает АКА.",
                        "Дисквалификация",
                        DialogButtons.Ok,
                        DialogIcon.Information);
                }

                OnPropertyChanged(nameof(CurrentMatch));
            }
        }

        private void CompleteMatch(int? winnerId, bool disqualification, bool autoAdvance)
        {
            if (CurrentMatch == null)
            {
                return;
            }

            CurrentMatch.WinnerId = winnerId;
            CurrentMatch.IsDisqualification = disqualification;
            CurrentMatch.IsFinished = true;

            if (CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.IsRunning = false;
            }

            if (autoAdvance)
            {
                PromoteWinnerAndSelectNextMatch();
            }
        }

        private void PromoteWinnerAndSelectNextMatch()
        {
            if (_currentMatchIndex >= 0)
            {
                _category.PromoteWinner(_currentMatchIndex);
            }
            // The bronze bout is a separate match and does not advance into the championship final.

            _currentMatchIndex = FindNextUnfinishedMatch();
            CurrentMatch = GetMatchByIndex(_currentMatchIndex)!;

            if (_currentMatchIndex == NoMatchIndex)
            {
                _category.IsFinished = true;
                SaveKumiteFinalResults();
                // Notify the public scoreboard after FinalResults has been calculated.
                OnPropertyChanged(nameof(Category));
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void InitializeKumiteBracketAsShobu(bool createNewBracket = false)
        {
            if (!createNewBracket && _category.BracketMatches.Count > 0)
            {
                _category.BracketMatches = _category.BracketMatches
                    .Select(source => (Match)ConvertToShobuMatch(source))
                    .ToList();

                if (_category.ThirdPlaceMatch != null)
                {
                    _category.ThirdPlaceMatch = ConvertToShobuMatch(_category.ThirdPlaceMatch);
                }

                return;
            }

            var shobuCategory = new ShobuSanbonCategory(
                _category.AllowedBelts,
                _category.CategoryType,
                _category.Sex,
                _category.MinAge,
                _category.MaxAge)
            {
                Participants = _category.Participants
            };

            shobuCategory.InitializeBracket();
            _category.BracketMatches = shobuCategory.BracketMatches;
            _category.ThirdPlaceMatch = null;
            _category.FinalResults = new List<ParticipantResult>();
            _category.IsFinished = false;
        }

        private static ShobuSanbonMatch ConvertToShobuMatch(Match source)
        {
            if (source is ShobuSanbonMatch shobuMatch)
            {
                return shobuMatch;
            }

            return new ShobuSanbonMatch(source.Aka, source.Shiro)
            {
                Id = source.Id,
                WinnerId = source.WinnerId,
                AkaScore = source.AkaScore,
                ShiroScore = source.ShiroScore,
                IsFinished = source.IsFinished,
                IsDisqualification = source.IsDisqualification
            };
        }

        private void SaveKumiteFinalResults()
        {
            if (_category.CategoryType != CategoryType.Kumite || !_category.BracketMatches.Any())
            {
                return;
            }

            var totalPoints = new Dictionary<int, int>();
            foreach (var participant in _category.Participants)
            {
                totalPoints[participant.Id] = 0;
            }

            var completedMatches = _category.BracketMatches
                .Concat(_category.ThirdPlaceMatch is { IsFinished: true } bronze ? new[] { bronze } : Array.Empty<Match>())
                .Where(match => match.IsFinished);

            foreach (var match in completedMatches)
            {
                if (match.Aka.HasValue && totalPoints.ContainsKey(match.Aka.Value))
                {
                    totalPoints[match.Aka.Value] += match.AkaScore;
                }

                if (match.Shiro.HasValue && totalPoints.ContainsKey(match.Shiro.Value))
                {
                    totalPoints[match.Shiro.Value] += match.ShiroScore;
                }
            }

            var finalMatch = _category.BracketMatches[0];
            var winnerId = finalMatch.WinnerId;
            var runnerUpId = winnerId == finalMatch.Aka ? finalMatch.Shiro
                : winnerId == finalMatch.Shiro ? finalMatch.Aka : null;
            int? thirdPlaceWinnerId = _category.ThirdPlaceMatch?.WinnerId;
            int? thirdPlaceLoserId = null;
            if (_category.ThirdPlaceMatch is { WinnerId: not null } bronzeMatch)
            {
                if (bronzeMatch.WinnerId == bronzeMatch.Aka)
                {
                    thirdPlaceLoserId = bronzeMatch.Shiro;
                }
                else if (bronzeMatch.WinnerId == bronzeMatch.Shiro)
                {
                    thirdPlaceLoserId = bronzeMatch.Aka;
                }
            }

            var orderedParticipants = _category.Participants
                .OrderByDescending(p => winnerId.HasValue && p.Id == winnerId.Value)
                .ThenByDescending(p => runnerUpId.HasValue && p.Id == runnerUpId.Value)
                .ThenByDescending(p => thirdPlaceWinnerId.HasValue && p.Id == thirdPlaceWinnerId.Value)
                .ThenByDescending(p => thirdPlaceLoserId.HasValue && p.Id == thirdPlaceLoserId.Value)
                .ThenByDescending(p => totalPoints.TryGetValue(p.Id, out int points) ? points : 0)
                .ThenBy(p => p.FullName)
                .ToList();

            var finalResults = new List<ParticipantResult>();
            for (int i = 0; i < orderedParticipants.Count; i++)
            {
                finalResults.Add(new ParticipantResult
                {
                    Participant = orderedParticipants[i],
                    Score = orderedParticipants.Count - i,
                    JudgeScores = new List<decimal>()
                });
            }

            _category.FinalResults = finalResults;
        }
    }
}
