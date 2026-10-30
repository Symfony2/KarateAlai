using System;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for public scoreboard display during draw resolution
    /// </summary>
    public class DrawResolverScoreboardViewModel : ViewModelBase
    {
        private readonly Participant _akaParticipant;
        private readonly Participant _shiroParticipant;

        public string AkaName => _akaParticipant.FullName;
        public string ShiroName => _shiroParticipant.FullName;

        private Participant _winner;
        public Participant Winner
        {
            get => _winner;
            set
            {
                _winner = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWinnerAnnounced));
                OnPropertyChanged(nameof(WinnerText));
                OnPropertyChanged(nameof(WinnerColor));
            }
        }

        public bool IsWinnerAnnounced => Winner != null;

        public string WinnerText
        {
            get
            {
                if (Winner == null) return "";
                return Winner == _akaParticipant ? "ПОБЕЖДАЕТ АКА!" : "ПОБЕЖДАЕТ СИРО!";
            }
        }

        public string WinnerColor => Winner == _akaParticipant ? "#E74C3C" : Winner == _shiroParticipant ? "White" : "Transparent";

        public DrawResolverScoreboardViewModel(Participant akaParticipant, Participant shiroParticipant)
        {
            _akaParticipant = akaParticipant ?? throw new ArgumentNullException(nameof(akaParticipant));
            _shiroParticipant = shiroParticipant ?? throw new ArgumentNullException(nameof(shiroParticipant));
        }

        public void AnnounceWinner(Participant winner)
        {
            Winner = winner;
        }
    }
}
