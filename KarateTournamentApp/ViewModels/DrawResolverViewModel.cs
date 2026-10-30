using System;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for resolving draw between two participants through 1v1 match
    /// </summary>
    public class DrawResolverViewModel : ViewModelBase
    {
        private readonly Participant _akaParticipant;
        private readonly Participant _shiroParticipant;
        private readonly DrawResolverScoreboardViewModel _scoreboardViewModel;
        
        public string AkaName => _akaParticipant.FullName;
        public string ShiroName => _shiroParticipant.FullName;

        private Participant _winner;
        public Participant Winner
        {
            get => _winner;
            private set
            {
                _winner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWinnerSelected));
                OnPropertyChanged(nameof(WinnerName));
                OnPropertyChanged(nameof(WinnerColor));
            }
        }

        public bool IsWinnerSelected => Winner != null;
        
        public string WinnerName => Winner != null ? $"{(Winner == _akaParticipant ? "АКА" : "СИРО")} ПОБЕЖДАЕТ!" : "";
        
        public string WinnerColor => Winner == _akaParticipant ? "Red" : Winner == _shiroParticipant ? "White" : "Transparent";

        public ICommand SelectAkaCommand { get; }
        public ICommand SelectShiroCommand { get; }
        public ICommand ConfirmCommand { get; }

        public event EventHandler<Participant> WinnerConfirmed;

        public DrawResolverViewModel(Participant akaParticipant, Participant shiroParticipant, DrawResolverScoreboardViewModel scoreboardViewModel = null)
        {
            _akaParticipant = akaParticipant ?? throw new ArgumentNullException(nameof(akaParticipant));
            _shiroParticipant = shiroParticipant ?? throw new ArgumentNullException(nameof(shiroParticipant));
            _scoreboardViewModel = scoreboardViewModel;

            SelectAkaCommand = new RelayCommand(o => SelectWinner(_akaParticipant), o => Winner == null);
            SelectShiroCommand = new RelayCommand(o => SelectWinner(_shiroParticipant), o => Winner == null);
            ConfirmCommand = new RelayCommand(o => ConfirmWinner(), o => IsWinnerSelected);
        }

        private void SelectWinner(Participant participant)
        {
            Winner = participant;
            
            _scoreboardViewModel?.AnnounceWinner(participant);
        }

        private void ConfirmWinner()
        {
            if (Winner != null)
            {
                WinnerConfirmed?.Invoke(this, Winner);
            }
        }
    }
}

