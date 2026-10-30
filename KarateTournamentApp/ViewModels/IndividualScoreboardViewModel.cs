using System;
using KarateTournamentApp.Models;
using KarateTournamentApp.Localization;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for individual competition scoreboard (Kata, Kumite, etc.)
    /// </summary>
    public class IndividualScoreboardViewModel : ViewModelBase
    {
        private readonly IndividualCompetitionManagerViewModel _competitionManager;

        public IndividualCompetitionManagerViewModel CompetitionManager => _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public string CategoryType => RussianText.CategoryType(_competitionManager.Category.CategoryType);

        public string CurrentParticipantName => _competitionManager.CurrentParticipant?.FullName ?? "---";
        public string CurrentParticipantClub => _competitionManager.CurrentParticipant?.Club ?? "";
        
        public int CurrentNumber => _competitionManager.CurrentParticipantNumber;
        public int TotalParticipants => _competitionManager.TotalParticipants;
        public string ProgressText => $"{CurrentNumber} / {TotalParticipants}";

        public decimal FinalScore => _competitionManager.FinalScore;
        public System.Collections.ObjectModel.ObservableCollection<decimal> JudgeScores => _competitionManager.JudgeScores;
        public bool IsParticipantFinished => _competitionManager.IsParticipantFinished;

        public IndividualScoreboardViewModel(IndividualCompetitionManagerViewModel competitionManager)
        {
            _competitionManager = competitionManager;

            // Subscribe to changes
            _competitionManager.PropertyChanged += (s, e) => RefreshAll();
            _competitionManager.JudgeScores.CollectionChanged += (s, e) => RefreshScores();
        }

        private void RefreshAll()
        {
            OnPropertyChanged(nameof(CurrentParticipantName));
            OnPropertyChanged(nameof(CurrentParticipantClub));
            OnPropertyChanged(nameof(CurrentNumber));
            OnPropertyChanged(nameof(TotalParticipants));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(IsParticipantFinished));
            RefreshScores();
        }

        private void RefreshScores()
        {
            OnPropertyChanged(nameof(FinalScore));
            OnPropertyChanged(nameof(JudgeScores));
        }
    }
}
