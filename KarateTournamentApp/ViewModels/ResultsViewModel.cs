using KarateTournamentApp.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace KarateTournamentApp.ViewModels
{
    public class ResultsViewModel : ViewModelBase
    {
        private readonly IndividualCompetitionManagerViewModel _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public ObservableCollection<ParticipantResult> Rankings { get; set; }
        public bool HasKumiteWinner =>
            _competitionManager.Category.CategoryType == CategoryType.Kumite
            && _competitionManager.Category.BracketMatches.Any()
            && _competitionManager.Category.BracketMatches[0].WinnerId.HasValue;

        public bool HasKumiteFinalMatchSummary => HasKumiteWinner && _competitionManager.Category.BracketMatches[0].Aka.HasValue && _competitionManager.Category.BracketMatches[0].Shiro.HasValue;

        public string KumiteWinnerDisplay
        {
            get
            {
                if (!HasKumiteWinner)
                {
                    return string.Empty;
                }

                var winnerId = _competitionManager.Category.BracketMatches[0].WinnerId!.Value;
                var winner = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == winnerId);
                return winner != null ? $"Победитель: {winner.FullName}" : "Победитель: ---";
            }
        }

        public string KumiteFinalMatchSummary
        {
            get
            {
                if (!HasKumiteFinalMatchSummary)
                {
                    return string.Empty;
                }

                var finalMatch = _competitionManager.Category.BracketMatches[0];
                var aka = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == finalMatch.Aka!.Value);
                var shiro = _competitionManager.Category.Participants.FirstOrDefault(p => p.Id == finalMatch.Shiro!.Value);

                string akaName = aka?.FullName ?? "АКА";
                string shiroName = shiro?.FullName ?? "СИРО";
                string resolution = BuildResolutionSuffix(finalMatch);

                return $"Финал: {akaName} {finalMatch.AkaScore}:{finalMatch.ShiroScore} {shiroName}{resolution}";
            }
        }

        public ResultsViewModel(IndividualCompetitionManagerViewModel competitionManager)
        {
            _competitionManager = competitionManager;
            
            Rankings = _competitionManager.GetFinalRankings();
        }

        private static string BuildResolutionSuffix(Match finalMatch)
        {
            if (finalMatch is not ShobuSanbonMatch shobuMatch)
            {
                return string.Empty;
            }

            if (finalMatch.IsDisqualification)
            {
                return " — ХАНСОКУ";
            }

            if (shobuMatch.AkaScore == shobuMatch.ShiroScore && (shobuMatch.HasSenshuAka || shobuMatch.HasSenshuShiro))
            {
                return " — СЭНШУ";
            }

            if (shobuMatch.TimeRemaining <= 0 && shobuMatch.AkaScore != shobuMatch.ShiroScore)
            {
                return shobuMatch.OvertimeCount > 0 ? " (по времени в дополнительном поединке)" : " (по истечении времени)";
            }

            if (shobuMatch.AkaScore >= 6 || shobuMatch.ShiroScore >= 6)
            {
                return " — набрано 6 очков";
            }

            if (shobuMatch.OvertimeCount > 0)
            {
                return " (после дополнительного поединка)";
            }

            return string.Empty;
        }
    }
}



