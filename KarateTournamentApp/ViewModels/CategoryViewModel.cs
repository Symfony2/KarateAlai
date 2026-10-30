using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Localization;
using KarateTournamentApp.Commands;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;

namespace KarateTournamentApp.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private const string PublicCompetitionDisplayKey = "public-competition-display";

        private readonly Category _category;
        private readonly Action<CategoryViewModel> _deleteRequestCallback;
        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;
        private readonly IUiScheduler _uiScheduler;

        public CategoryViewModel(
            Category category,
            Action<CategoryViewModel> deleteRequestCallback,
            IDialogService dialogService,
            IWindowService windowService,
            IUiScheduler uiScheduler)
        {
            _category = category;
            _deleteRequestCallback = deleteRequestCallback;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
            RenameCommand = new RelayCommand(o => RenameCategory(), o => true);
            RemoveParticipantCommand = new RelayCommand(RemoveParticipant, o => true);
            StartCompetitionCommand = new RelayCommand(o => StartCompetition(), o => CanStartCompetition());
            DeleteCommand = new RelayCommand(o => RequestDelete(), o => CanDelete());
            InitializeBracketCommand = new RelayCommand(o => InitializeBracket(), o => CanInitializeBracket());
            ViewResultsCommand = new RelayCommand(o => ViewResults(), o => CanViewResults());
            ViewBracketCommand = new RelayCommand(o => ViewBracket(), o => CanViewBracket());
        }

        public Category Category => _category;

        public string Name => _category.Name;
        public int ParticipantCount => _category.Participants.Count;
        public bool HasSingleParticipant => ParticipantCount == 1;
        public string ParticipantCountLabel
        {
            get
            {
                var count = ParticipantCount;
                var lastTwo = count % 100;
                var last = count % 10;
                var noun = lastTwo is >= 11 and <= 14 ? "спортсменов" : last switch
                {
                    1 => "спортсмен",
                    2 or 3 or 4 => "спортсмена",
                    _ => "спортсменов"
                };
                return noun;
            }
        }
        public string CategoryTypeDisplay => RussianText.CategoryType(_category.CategoryType);
        public bool IsKumite => _category.CategoryType == CategoryType.Kumite;
        public string SexDisplay => RussianText.Sex(_category.Sex);
        public bool HasKumiteWinner =>
            _category.CategoryType == CategoryType.Kumite
            && _category.IsFinished
            && _category.BracketMatches.Any()
            && _category.BracketMatches[0].WinnerId.HasValue;

        public string KumiteWinnerDisplay
        {
            get
            {
                if (!HasKumiteWinner)
                {
                    return string.Empty;
                }

                var winnerId = _category.BracketMatches[0].WinnerId!.Value;
                var winner = _category.Participants.FirstOrDefault(p => p.Id == winnerId);
                return winner != null ? $"Победитель: {winner.FullName}" : "Победитель: ---";
            }
        }

        public string AgeRangeDisplay => _category.AgeGroupLabel ??
            (_category.MinAge.HasValue || _category.MaxAge.HasValue
                ? RussianText.FormatAgeRange(_category.MinAge, _category.MaxAge)
                : "Все возраста");
        public string WeightRangeDisplay => _category.WeightGroupLabel ??
            (_category.MinWeightKg.HasValue || _category.MaxWeightKg.HasValue
                ? $"{_category.MinWeightKg?.ToString("0.##") ?? "0"}–{_category.MaxWeightKg?.ToString("0.##") ?? "∞"} кг"
                : string.Empty);
        public bool HasWeightRange => !string.IsNullOrWhiteSpace(WeightRangeDisplay);

        private bool _isSelectedForMerge;
        public bool IsSelectedForMerge
        {
            get => _isSelectedForMerge;
            set
            {
                if (_isSelectedForMerge == value) return;
                _isSelectedForMerge = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ICommand RenameCommand { get; }
        public ICommand RemoveParticipantCommand { get; }
        public ICommand StartCompetitionCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand InitializeBracketCommand { get; }
        public ICommand ViewResultsCommand { get; }
        public ICommand ViewBracketCommand { get; }

        private void RenameCategory()
        {
            var newName = _dialogService.ShowTextInput("Переименование категории", "Введите новое название категории:", _category.Name);
            if (newName == null)
            {
                return;
            }

            newName = newName.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                _dialogService.ShowMessage("Название категории не может быть пустым.", "Некорректное название", DialogButtons.Ok, DialogIcon.Warning);
                return;
            }

            _category.Name = newName;
            Refresh();
        }

        private void RemoveParticipant(object? parameter)
        {
            if (parameter is Participant participant)
            {
                _category.Participants.Remove(participant);
                OnPropertyChanged(nameof(ParticipantCount));
                OnPropertyChanged(nameof(ParticipantCountLabel));
                OnPropertyChanged(nameof(HasSingleParticipant));
                Refresh();
            }
        }

        private bool CanDelete()
        {
            // Can delete if category hasn't started or is empty
            return !_category.IsFinished;
        }

        private void RequestDelete()
        {
            _deleteRequestCallback?.Invoke(this);
        }

        private bool CanInitializeBracket()
        {
            // Can initialize bracket if:
            // 1. Category has at least 2 participants
            // 2. Category is a bracket-style category (ShobuSanbonCategory or has bracket matches)
            // 3. Bracket hasn't been initialized yet (no matches)
            // 4. Category hasn't finished
            return _category.Participants.Count >= 2 && 
                   _category.BracketMatches.Count == 0 && 
                   !_category.IsFinished;
        }

        private void InitializeBracket()
        {
            try
            {
                if(_category.CategoryType==CategoryType.Kumite)
                    _category.InitializeBracket();
                else
                    _category.Participants.OrderBy(p => p.Id);
                _dialogService.ShowMessage(
                    $"Сетка турнира создана!\n\n" +
                    $"Категория: {_category.Name}\n" +
                    $"Количество спортсменов: {_category.Participants.Count}\n" +
                    $"Количество поединков: {_category.BracketMatches.Count}",
                    "Успешно",
                    DialogButtons.Ok,
                    DialogIcon.Information);

                Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    $"Ошибка при создании сетки:\n{ex.Message}",
                    "Ошибка",
                    DialogButtons.Ok,
                    DialogIcon.Error);
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ParticipantCount));
            OnPropertyChanged(nameof(ParticipantCountLabel));
            OnPropertyChanged(nameof(HasSingleParticipant));
            OnPropertyChanged(nameof(CategoryTypeDisplay));
            OnPropertyChanged(nameof(IsKumite));
            OnPropertyChanged(nameof(SexDisplay));
            OnPropertyChanged(nameof(AgeRangeDisplay));
            OnPropertyChanged(nameof(WeightRangeDisplay));
            OnPropertyChanged(nameof(HasWeightRange));
            OnPropertyChanged(nameof(HasKumiteWinner));
            OnPropertyChanged(nameof(KumiteWinnerDisplay));
            OnPropertyChanged(nameof(Category));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanStartCompetition()
        {
            return _category.Participants.Count >= 2 && !_category.IsFinished;
        }

        private bool CanViewResults()
        {
            // Can view results if category is finished and has saved results
            return _category.IsFinished
                   && ((_category.FinalResults != null && _category.FinalResults.Any())
                       || _category.JudgingScores.Any());
        }

        private bool CanViewBracket() => IsKumite && _category.BracketMatches.Count > 0;

        private void ViewBracket()
        {
            if (!CanViewBracket())
            {
                _dialogService.ShowMessage(
                    "Сначала выполните жеребьёвку этой категории.",
                    "Турнирная сетка ещё не создана",
                    DialogButtons.Ok,
                    DialogIcon.Information);
                return;
            }

            var previewViewModel = new BracketPreviewViewModel(_category, _dialogService);
            _windowService.ShowDialog(previewViewModel, new WindowOptions
            {
                Title = $"Турнирная сетка — {_category.Name}",
                Width = 1320,
                Height = 820,
                StartupPosition = WindowStartupPosition.CenterScreen,
                Resizable = true
            });
        }

        private void ViewResults()
        {
            // Create a competition manager with saved results
            var competitionManager = new IndividualCompetitionManagerViewModel(_category, _windowService, _uiScheduler);
            
            // Load saved rankings if available, otherwise source from raw judging scores.
            var sourceResults = _category.FinalResults.Any()
                ? _category.FinalResults
                : BuildResultsFromJudgingScores();

            foreach (var result in sourceResults)
            {
                competitionManager.Results.Add(result);
            }

            var resultsViewModel = new ResultsViewModel(competitionManager);
            _windowService.ShowDialog(resultsViewModel, new WindowOptions
            {
                Title = $"Результаты — {_category.Name}",
                Width = 800,
                Height = 600
            });
        }

        private List<ParticipantResult> BuildResultsFromJudgingScores()
        {
            var results = new List<ParticipantResult>();

            foreach (var (scores, participantId) in _category.JudgingScores)
            {
                var participant = _category.Participants.FirstOrDefault(p => p.Id == participantId);
                if (participant == null)
                {
                    continue;
                }

                var scoreCalculation = JudgingScoreCalculator.CalculateFinalScore(scores);

                results.Add(new ParticipantResult
                {
                    Participant = participant,
                    Score = scoreCalculation.FinalScore,
                    JudgeScores = new List<decimal>(scores),
                    DiscardedJudgeScoreIndexes = scoreCalculation.DiscardedScoreIndexes
                });
            }

            return results;
        }

        private void StartCompetition()
        {
            // Check if this is a Kumite category (bracket-style competition)
            if (_category.CategoryType == CategoryType.Kumite || _category is ShobuSanbonCategory)
            {
                StartBracketCompetition();
            }
            else
            {
                // All other categories are individual (Kata, Kihon, Kobudo, Grappling)
                StartIndividualCompetition();
            }
        }

        private void StartBracketCompetition()
        {
            var competitionManager = new CompetitionManagerViewModel(_category, _dialogService);
            var scoreboardViewModel = new ScoreboardViewModel(competitionManager);
            var judgeViewModel = new ScoreboardJudgeViewModel(competitionManager, _dialogService, scoreboardViewModel);
            Refresh();

            _windowService.ShowOrUpdate(PublicCompetitionDisplayKey, scoreboardViewModel, new WindowOptions
            {
                Title = $"Табло — {_category.Name}",
                Width = 1200,
                Height = 800,
                SizeState = WindowSizeState.Maximized,
                FullScreenMonitor = WindowMonitor.SecondaryIfAvailable
            }, (_, _) => Refresh());

            var judgeHandle = _windowService.Show(judgeViewModel, new WindowOptions
            {
                Title = $"Панель судьи — {_category.Name}",
                Width = 800,
                Height = 600,
                FullScreenMonitor = WindowMonitor.Primary
            });

            judgeHandle.Closed += (s, e) => Refresh();
            judgeHandle.Closed += (s, e) => Refresh();
        }

        private void StartIndividualCompetition()
        {
            var competitionManager = new IndividualCompetitionManagerViewModel(_category, _windowService, _uiScheduler);
            var scoreboardViewModel = new IndividualScoreboardViewModel(competitionManager);
            var judgeViewModel = new IndividualJudgeViewModel(competitionManager, _windowService);

            _windowService.ShowOrUpdate(PublicCompetitionDisplayKey, scoreboardViewModel, new WindowOptions
            {
                Title = $"Табло — {_category.Name}",
                Width = 1200,
                Height = 800,
                SizeState = WindowSizeState.Maximized,
                FullScreenMonitor = WindowMonitor.SecondaryIfAvailable
            }, (_, _) => Refresh());

            _windowService.Show(judgeViewModel, new WindowOptions
            {
                Title = $"Панель судьи — {_category.Name}",
                Width = 900,
                Height = 700,
                FullScreenMonitor = WindowMonitor.Primary
            });
        }
    }


}
