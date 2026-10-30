using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Navigation;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for judge panel in individual competitions
    /// </summary>
    public class IndividualJudgeViewModel : ViewModelBase
    {
        private readonly IndividualCompetitionManagerViewModel _competitionManager;
        private readonly IWindowService _windowService;

        public IndividualCompetitionManagerViewModel CompetitionManager => _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public string CurrentParticipantName => _competitionManager.CurrentParticipant?.FullName ?? "---";
        
        public ObservableCollection<decimal> JudgeScores => _competitionManager.JudgeScores;
        public decimal FinalScore => _competitionManager.FinalScore;
        public bool IsParticipantFinished => _competitionManager.IsParticipantFinished;

        // Commands
        public ICommand AddScoreCommand { get; }
        public ICommand RemoveLastScoreCommand { get; }
        public ICommand FinishParticipantCommand { get; }
        public ICommand NextParticipantCommand { get; }
        public ICommand ShowResultsCommand { get; }
        public ICommand ShowFinalScoreCommand { get; }

        private string _scoreInput;
        public string ScoreInput
        {
            get => _scoreInput;
            set
            {
                _scoreInput = value;
                OnPropertyChanged();
            }
        }

        public IndividualJudgeViewModel(IndividualCompetitionManagerViewModel competitionManager, IWindowService windowService)
        {
            _competitionManager = competitionManager;
            _windowService = windowService ?? throw new System.ArgumentNullException(nameof(windowService));

            AddScoreCommand = new RelayCommand(o => AddScore());
            RemoveLastScoreCommand = _competitionManager.RemoveLastJudgeScoreCommand;
            FinishParticipantCommand = _competitionManager.FinishParticipantCommand;
            NextParticipantCommand = _competitionManager.NextParticipantCommand;
            ShowResultsCommand = new RelayCommand(o => ShowResults());
            ShowFinalScoreCommand = new RelayCommand(o => ShowFinalScore(), o => CanShowFinalScore());

            _competitionManager.PropertyChanged += (s, e) => RefreshAll();
            _competitionManager.JudgeScores.CollectionChanged += (s, e) => RefreshScores();
        }

        private void AddScore()
        {
            if (!string.IsNullOrWhiteSpace(ScoreInput) && decimal.TryParse(ScoreInput, out decimal score))
            {
                if (score >= 0 && score <= 10)
                {
                    JudgeScores.Add(score);
                    ScoreInput = string.Empty;
                    RefreshScores();
                }
            }
        }

        private void ShowResults()
        {
            var resultsViewModel = new ResultsViewModel(_competitionManager);
            _windowService.ShowDialog(resultsViewModel, new WindowOptions
            {
                Title = $"Результаты — {CategoryName}",
                Width = 800,
                Height = 600
            });
        }

        private bool CanShowFinalScore()
        {
            return JudgeScores.Count >= 3;
        }

        private void ShowFinalScore()
        {
            // Temporarily finish participant to calculate and show final score
            _competitionManager.IsParticipantFinished = true;
            RefreshScores();
        }

        private void RefreshAll()
        {
            OnPropertyChanged(nameof(CurrentParticipantName));
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(IsParticipantFinished));
            RefreshScores();
        }

        private void RefreshScores()
        {
            OnPropertyChanged(nameof(FinalScore));
        }
    }
}
