using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;

namespace KarateTournamentApp.ViewModels
{
    public class IndividualCompetitionManagerViewModel : ViewModelBase
    {
        private readonly Category _category;
        private readonly IWindowService _windowService;
        private readonly IUiScheduler _uiScheduler;
        private int _currentParticipantIndex;
        
        public Category Category => _category;
        
        private Participant _currentParticipant;
        public Participant CurrentParticipant
        {
            get => _currentParticipant;
            set
            {
                _currentParticipant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentParticipantNumber));
                OnPropertyChanged(nameof(TotalParticipants));
                
                // Reset judge scores for new participant
                JudgeScores.Clear();
                IsParticipantFinished = false;
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        public int CurrentParticipantNumber => _currentParticipantIndex + 1;
        public int TotalParticipants => _category.Participants.Count;

        // Collection to store scores from multiple judges
        public ObservableCollection<decimal> JudgeScores { get; set; }

        private bool _isParticipantFinished;
        public bool IsParticipantFinished
        {
            get => _isParticipantFinished;
            set
            {
                _isParticipantFinished = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        // Calculate final score (remove highest and lowest, then sum)
        public decimal FinalScore
        {
            get
            {
                if (!IsParticipantFinished || JudgeScores.Count < 3) return 0;

                return JudgingScoreCalculator.CalculateFinalScore(JudgeScores).FinalScore;
            }
        }

        // Store all participant results
        public ObservableCollection<ParticipantResult> Results { get; set; }

        public ICommand NextParticipantCommand { get; }
        public ICommand FinishParticipantCommand { get; }
        public ICommand AddJudgeScoreCommand { get; }
        public ICommand RemoveLastJudgeScoreCommand { get; }

        public IndividualCompetitionManagerViewModel(
            Category category,
            IWindowService windowService,
            IUiScheduler uiScheduler)
        {
            _category = category;
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
            _currentParticipantIndex = 0;
            
            JudgeScores = new ObservableCollection<decimal>();
            Results = new ObservableCollection<ParticipantResult>();
            
            if (_category.Participants.Any())
            {
                CurrentParticipant = _category.Participants[_currentParticipantIndex];
            }

            NextParticipantCommand = new RelayCommand(o => MoveToNextParticipant(), o => CanMoveToNext());
            FinishParticipantCommand = new RelayCommand(o => FinishCurrentParticipant(), o => CanFinishParticipant());
            AddJudgeScoreCommand = new RelayCommand(AddJudgeScore);
            RemoveLastJudgeScoreCommand = new RelayCommand(o => RemoveLastScore(), o => JudgeScores.Any());
        }

        private bool CanMoveToNext()
        {
            return _currentParticipantIndex < _category.Participants.Count - 1;
        }

        private bool CanFinishParticipant()
        {
            return CurrentParticipant != null && JudgeScores.Count >= 3;
        }

        private void MoveToNextParticipant()
        {
            if (_currentParticipantIndex < _category.Participants.Count - 1)
            {
                _currentParticipantIndex++;
                CurrentParticipant = _category.Participants[_currentParticipantIndex];
            }
            else
            {
                // Also save raw judge scores for backward compatibility
                foreach (var result in Results)
                {
                    _category.JudgingScores.Add((result.JudgeScores, result.Participant.Id));
                }
                
                _category.IsFinished = true;
                CurrentParticipant = null;
            }
        }

        private void FinishCurrentParticipant()
        {
            if (CurrentParticipant != null && JudgeScores.Count >= 3)
            {
                IsParticipantFinished = true;

                var scoreCalculation = JudgingScoreCalculator.CalculateFinalScore(JudgeScores);
                var result = new ParticipantResult
                {
                    Participant = CurrentParticipant,
                    Score = scoreCalculation.FinalScore,
                    JudgeScores = new List<decimal>(JudgeScores),
                    DiscardedJudgeScoreIndexes = scoreCalculation.DiscardedScoreIndexes
                };
                
                Results.Add(result);
                

                MoveToNextParticipant();
            }
        }

        private void AddJudgeScore(object parameter)
        {
            if (parameter is string scoreText && decimal.TryParse(scoreText, out decimal score))
            {
                if (score >= 0 && score <= 10) 
                {
                    JudgeScores.Add(score);
                    OnPropertyChanged(nameof(FinalScore));
                }
            }
        }

        private void RemoveLastScore()
        {
            if (JudgeScores.Any())
            {
                JudgeScores.RemoveAt(JudgeScores.Count - 1);
                OnPropertyChanged(nameof(FinalScore));
            }
        }

        private ParticipantResult ResolveDraw(ParticipantResult participant1, ParticipantResult participant2)
        {
            var scoreboardViewModel = new DrawResolverScoreboardViewModel(participant1.Participant, participant2.Participant);
            var drawResolver = new DrawResolverViewModel(participant1.Participant, participant2.Participant, scoreboardViewModel);

            var scoreboardHandle = _windowService.Show(scoreboardViewModel, new WindowOptions
            {
                Title = "ДОПОЛНИТЕЛЬНЫЙ ПОЕДИНОК — ТАБЛО",
                Width = 1920,
                Height = 1080,
                SizeState = WindowSizeState.Maximized,
                Borderless = true
            });

            Participant winner = null;
            IWindowHandle judgeHandle = null;

            drawResolver.WinnerConfirmed += (sender, selectedWinner) =>
            {
                winner = selectedWinner;

                _uiScheduler.Schedule(TimeSpan.FromSeconds(3), () =>
                {
                    scoreboardHandle.Close();
                    judgeHandle?.Close();
                });
            };

            _windowService.ShowDialog(drawResolver, new WindowOptions
            {
                Title = "Разрешение ничьей — панель судьи",
                Width = 800,
                Height = 600,
                StartupPosition = WindowStartupPosition.CenterScreen,
                Resizable = false,
                Topmost = true
            }, handle => judgeHandle = handle);

            return winner == participant1.Participant ? participant1 : participant2;
        }

        public ObservableCollection<ParticipantResult> GetFinalRankings()
        {
            // If final rankings are already resolved and saved, use them directly.
            if (_category.FinalResults.Any())
            {
                return new ObservableCollection<ParticipantResult>(_category.FinalResults);
            }

            var results = new ObservableCollection<ParticipantResult>(Results.OrderByDescending(r => r.Score));
            
            System.Diagnostics.Debug.WriteLine($"Расчёт итогового рейтинга. Всего результатов: {results.Count}");
            
            if (results.Count >= 2)
            {
                ResolveRankingTie(results, 0);
            }

            if (results.Count >= 3)
            {
                ResolveRankingTie(results, 1);
            }

            if (_category.IsFinished)
            {
                _category.FinalResults = results.ToList();
            }
            
            return results;
        }

        private void ResolveRankingTie(ObservableCollection<ParticipantResult> results, int firstIndex)
        {
            var first = results[firstIndex];
            var second = results[firstIndex + 1];
            if (first.Score != second.Score)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Обнаружена ничья между {first.Participant.FullName} и {second.Participant.FullName}");

            var firstScores = first.JudgeScores.OrderBy(score => score).ToList();
            var secondScores = second.JudgeScores.OrderBy(score => score).ToList();
            if (firstScores.Count < 3 || secondScores.Count < 3)
            {
                return;
            }

            var highestScoreComparison = firstScores[firstScores.Count - 1]
                .CompareTo(secondScores[secondScores.Count - 1]);
            if (highestScoreComparison < 0)
            {
                SwapResults(results, firstIndex, firstIndex + 1);
                return;
            }

            if (highestScoreComparison > 0)
            {
                return;
            }

            var lowestScoreComparison = firstScores[0].CompareTo(secondScores[0]);
            if (lowestScoreComparison < 0)
            {
                SwapResults(results, firstIndex, firstIndex + 1);
                return;
            }

            if (lowestScoreComparison == 0)
            {
                System.Diagnostics.Debug.WriteLine("Ничья сохраняется — открывается окно разрешения ничьей");
                var winner = ResolveDraw(first, second);
                if (winner == second)
                {
                    SwapResults(results, firstIndex, firstIndex + 1);
                }
            }
        }

        private void SwapResults(ObservableCollection<ParticipantResult> results, int index1, int index2)
        {
            var temp = results[index1];
            results[index1] = results[index2];
            results[index2] = temp;
        }
    }

}
