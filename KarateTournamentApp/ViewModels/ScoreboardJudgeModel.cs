using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.ViewModels
{
    /// <summary>
    /// ViewModel for judge control panel
    /// </summary>
    public class ScoreboardJudgeViewModel : ViewModelBase
    {
        private readonly CompetitionManagerViewModel _competitionManager;
        private readonly ScoreboardViewModel _scoreboardViewModel;
        private readonly IDialogService _dialogService;

        public CompetitionManagerViewModel CompetitionManager => _competitionManager;

        public string CategoryName => _competitionManager.Category.Name;
        public string AkaName => _competitionManager.AkaParticipant?.FullName ?? "---";
        public string ShiroName => _competitionManager.ShiroParticipant?.FullName ?? "---";
        public bool IsShobuSanbon => _competitionManager.IsShobuSanbon;
        public int AkaScore => _competitionManager.CurrentMatch?.AkaScore ?? 0;
        public int ShiroScore => _competitionManager.CurrentMatch?.ShiroScore ?? 0;
        public int AkaAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch akaMatch ? akaMatch.AtenaiAka : 0;
        public int ShiroAtenai => _competitionManager.CurrentMatch is ShobuSanbonMatch shiroMatch ? shiroMatch.AtenaiShiro : 0;
        public int AkaChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch akaPenaltyMatch ? akaPenaltyMatch.ChukokuAka : 0;
        public int ShiroChukoku => _competitionManager.CurrentMatch is ShobuSanbonMatch shiroPenaltyMatch ? shiroPenaltyMatch.ChukokuShiro : 0;

        // Commands for Shobu Sanbon
        public ICommand AddAkaPointCommand { get; }
        public ICommand AddShiroPointCommand { get; }
        public ICommand RemoveAkaPointCommand { get; }
        public ICommand RemoveShiroPointCommand { get; }
        public ICommand AddAkaAtenaiCommand { get; }
        public ICommand RemoveAkaAtenaiCommand { get; }
        public ICommand AddShiroAtenaiCommand { get; }
        public ICommand RemoveShiroAtenaiCommand { get; }
        public ICommand AddAkaChukokuCommand { get; }
        public ICommand RemoveAkaChukokuCommand { get; }
        public ICommand AddShiroChukokuCommand { get; }
        public ICommand RemoveShiroChukokuCommand { get; }
        public ICommand ToggleTimerCommand { get; }
        public ICommand StartTimerCommand { get; }
        public ICommand StopTimerCommand { get; }
        public ICommand ResetTimerCommand { get; }
        public ICommand SetTimeCommand { get; }
        public ICommand FinishMatchCommand { get; }
        public ICommand NextMatchCommand { get; }

        // Commands for other categories (Kata, Kumite, etc.)
        public ICommand AddJudgeScoreCommand { get; }

        public bool IsTimerRunning => _competitionManager.CurrentMatch is ShobuSanbonMatch match && match.IsRunning;

        public string TimerToggleText => IsTimerRunning ? "Стоп" : "Старт";

        public string TimeDisplay
        {
            get
            {
                if (_scoreboardViewModel != null)
                {
                    return _scoreboardViewModel.TimeDisplay;
                }

                if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
                {
                    var minutes = (int)(shobuMatch.TimeRemaining / 60);
                    var seconds = (int)(shobuMatch.TimeRemaining % 60);
                    return $"{minutes:D2}:{seconds:D2}";
                }

                return "--:--";
            }
        }

        private string _timeInput;
        public string TimeInput
        {
            get => _timeInput;
            set
            {
                _timeInput = value;
                OnPropertyChanged();
            }
        }

        public ScoreboardJudgeViewModel(CompetitionManagerViewModel competitionManager, IDialogService dialogService, ScoreboardViewModel scoreboardViewModel = null)
        {
            _competitionManager = competitionManager;
            _dialogService = dialogService ?? throw new System.ArgumentNullException(nameof(dialogService));
            _scoreboardViewModel = scoreboardViewModel;

            // Initialize commands
            AddAkaPointCommand = new RelayCommand(o => AddPoint(true, 1));
            AddShiroPointCommand = new RelayCommand(o => AddPoint(false, 1));
            RemoveAkaPointCommand = new RelayCommand(o => AddPoint(true, -1));
            RemoveShiroPointCommand = new RelayCommand(o => AddPoint(false, -1));

            AddAkaAtenaiCommand = new RelayCommand(o => AddPenalty(true, PenaltyType.Atenai, 1));
            RemoveAkaAtenaiCommand = new RelayCommand(o => AddPenalty(true, PenaltyType.Atenai, -1));
            AddShiroAtenaiCommand = new RelayCommand(o => AddPenalty(false, PenaltyType.Atenai, 1));
            RemoveShiroAtenaiCommand = new RelayCommand(o => AddPenalty(false, PenaltyType.Atenai, -1));
            AddAkaChukokuCommand = new RelayCommand(o => AddPenalty(true, PenaltyType.Chukoku, 1));
            RemoveAkaChukokuCommand = new RelayCommand(o => AddPenalty(true, PenaltyType.Chukoku, -1));
            AddShiroChukokuCommand = new RelayCommand(o => AddPenalty(false, PenaltyType.Chukoku, 1));
            RemoveShiroChukokuCommand = new RelayCommand(o => AddPenalty(false, PenaltyType.Chukoku, -1));

            ToggleTimerCommand = new RelayCommand(o => ToggleTimer());
            StartTimerCommand = new RelayCommand(o => StartTimer());
            StopTimerCommand = new RelayCommand(o => StopTimer());
            ResetTimerCommand = new RelayCommand(o => ResetTimer());
            SetTimeCommand = new RelayCommand(o => SetTime(o), o => CanSetTime(o));

            FinishMatchCommand = _competitionManager.FinishMatchCommand;
            NextMatchCommand = _competitionManager.NextMatchCommand;

            AddJudgeScoreCommand = new RelayCommand(o => AddJudgeScore(o));

            // Subscribe to changes
            _competitionManager.PropertyChanged += (s, e) => RefreshAll();
            if (_scoreboardViewModel != null)
            {
                _scoreboardViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ScoreboardViewModel.TimeDisplay))
                    {
                        OnPropertyChanged(nameof(TimeDisplay));
                    }
                };
            }
        }

        private void AddPoint(bool isAka, int points)
        {
            _competitionManager.UpdateMatchScore(isAka, points);
            RefreshAll();
        }

        private void AddPenalty(bool isAka, PenaltyType penaltyType, int change)
        {
            _competitionManager.UpdatePenalty(isAka, penaltyType, change);
            RefreshAll();
        }

        private void ToggleTimer()
        {
            if (IsTimerRunning)
            {
                StopTimer();
            }
            else
            {
                StartTimer();
            }
        }

        private void StartTimer()
        {
            _scoreboardViewModel?.StartTimer();
            RefreshAll();
        }

        private void StopTimer()
        {
            _scoreboardViewModel?.StopTimer();
            RefreshAll();
        }

        private void ResetTimer()
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                shobuMatch.TimeRemaining = 180;
                shobuMatch.IsRunning = false;
                _scoreboardViewModel?.StopTimer();
                RefreshAll();
            }
        }

        private bool CanSetTime(object parameter)
        {
            if (_competitionManager.CurrentMatch is not ShobuSanbonMatch)
                return false;
            
            // If parameter is provided (quick preset button)
            if (parameter is string)
                return true;
            
            // If manual input
            return !string.IsNullOrWhiteSpace(TimeInput);
        }

        private void SetTime(object parameter)
        {
            if (_competitionManager.CurrentMatch is ShobuSanbonMatch shobuMatch)
            {
                string timeValue = parameter as string ?? TimeInput;
                
                if (string.IsNullOrWhiteSpace(timeValue))
                    return;
                
                // Try to parse as seconds first
                if (double.TryParse(timeValue, out double seconds))
                {
                    if (seconds >= 0 && seconds <= 600) // Max 10 minutes
                    {
                        shobuMatch.TimeRemaining = seconds;
                        shobuMatch.IsRunning = false;
                        _scoreboardViewModel?.StopTimer();
                        TimeInput = string.Empty;
                        RefreshAll();
                        return;
                    }
                }
                
                // Try to parse as MM:SS format
                var parts = timeValue.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int secs))
                {
                    if (minutes >= 0 && minutes <= 10 && secs >= 0 && secs < 60)
                    {
                        double totalSeconds = minutes * 60 + secs;
                        shobuMatch.TimeRemaining = totalSeconds;
                        shobuMatch.IsRunning = false;
                        _scoreboardViewModel?.StopTimer();
                        TimeInput = string.Empty;
                        RefreshAll();
                        return;
                    }
                }
                
                // Invalid input
                _dialogService.ShowMessage(
                    "Неверный формат времени!\nУкажите секунды (например, 180) или формат ММ:СС (например, 3:00).",
                    "Ошибка",
                    DialogButtons.Ok,
                    DialogIcon.Warning);
            }
        }

        private void AddJudgeScore(object parameter)
        {
            // Logic for adding judge scores in Kata/Kumite categories
        }

        private void RefreshAll()
        {
            OnPropertyChanged(nameof(AkaName));
            OnPropertyChanged(nameof(ShiroName));
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(IsShobuSanbon));
            OnPropertyChanged(nameof(AkaScore));
            OnPropertyChanged(nameof(ShiroScore));
            OnPropertyChanged(nameof(AkaAtenai));
            OnPropertyChanged(nameof(ShiroAtenai));
            OnPropertyChanged(nameof(AkaChukoku));
            OnPropertyChanged(nameof(ShiroChukoku));
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerToggleText));
            OnPropertyChanged(nameof(TimeDisplay));
        }
    }
}
