using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.Commands;
using System;
using KarateTournamentApp.Models.ViewItems;

namespace KarateTournamentApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CategoryManager _categoryManager;

        // Form data
        private string _firstName = string.Empty;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private string _lastName = string.Empty;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private int? _age;
        public int? Age { get => _age; set { _age = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private Sex? _selectedSex;
        public Sex? SelectedSex { get => _selectedSex; set { _selectedSex = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private Belts? _selectedBelt;
        public Belts? SelectedBelt { get => _selectedBelt; set { _selectedBelt = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private string? _club;
        public string? Club { get => _club; set { _club = value; OnPropertyChanged(); } }

        private string? _selectedCountry = "Кыргызстан";
        public string? SelectedCountry { get => _selectedCountry; set { _selectedCountry = value; OnPropertyChanged(); } }

        public ObservableCollection<string> CountryOptions { get; } = new()
        {
            "Кыргызстан", "Казахстан", "Узбекистан", "Россия", "Таджикистан", "Другая страна"
        };

        private int _selectedDisciplineIndex;
        public int SelectedDisciplineIndex
        {
            get => _selectedDisciplineIndex;
            set { if (_selectedDisciplineIndex == value) return; ClearCategoryMergeSelection(); _selectedDisciplineIndex = value; OnPropertyChanged(); RefreshVisibleCategories(); }
        }

        private int _selectedSexFilterIndex;
        public int SelectedSexFilterIndex
        {
            get => _selectedSexFilterIndex;
            set { if (_selectedSexFilterIndex == value) return; ClearCategoryMergeSelection(); _selectedSexFilterIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAllSexesFilter)); OnPropertyChanged(nameof(IsMaleFilter)); OnPropertyChanged(nameof(IsFemaleFilter)); RefreshVisibleCategories(); }
        }
        public bool IsAllSexesFilter { get => SelectedSexFilterIndex == 0; set { if (value) SelectedSexFilterIndex = 0; } }
        public bool IsMaleFilter { get => SelectedSexFilterIndex == 1; set { if (value) SelectedSexFilterIndex = 1; } }
        public bool IsFemaleFilter { get => SelectedSexFilterIndex == 2; set { if (value) SelectedSexFilterIndex = 2; } }

        private string? _weightKgText;
        public string? WeightKgText { get => _weightKgText; set { _weightKgText = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private string _ageRangesText = GroupingRangeParser.DefaultAgeRanges;
        public string AgeRangesText { get => _ageRangesText; set { _ageRangesText = value; OnPropertyChanged(); } }

        private string _weightRangesText = GroupingRangeParser.DefaultWeightRanges;
        public string WeightRangesText { get => _weightRangesText; set { _weightRangesText = value; OnPropertyChanged(); } }

        private string _groupingSummary = "Настройте диапазоны и нажмите «Применить правила».";
        public string GroupingSummary { get => _groupingSummary; private set { _groupingSummary = value; OnPropertyChanged(); } }

        private bool _divideByAge = true;
        public bool DivideByAge 
        { 
            get => _divideByAge; 
            set 
            { 
                _divideByAge = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            } 
        }

        private bool _divideByWeight = true;
        public bool DivideByWeight
        {
            get => _divideByWeight;
            set
            {
                _divideByWeight = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _divideByBelt;
        public bool DivideByBelt 
        { 
            get => _divideByBelt; 
            set 
            { 
                _divideByBelt = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            } 
        }

        public ObservableCollection<CategorySelectionItem> CategorySelections { get; set; }

        public ObservableCollection<Participant> AllParticipants { get; set; }

        public ObservableCollection<CategoryViewModel> Categories { get; set; }
        public ObservableCollection<CategoryViewModel> VisibleCategories { get; } = new();
        public bool IsCategoryListEmpty => VisibleCategories.Count == 0;
        public int SelectedCategoryCount => Categories.Count(category => category.IsSelectedForMerge);
        public bool HasMultipleCategoriesSelected => SelectedCategoryCount >= 2;
        public string MergeSelectedButtonText => $"Объединить выбранные ({SelectedCategoryCount})";

        public ICommand AddParticipantCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand CreateExcelTemplateCommand { get; }
        public ICommand ApplyGroupingRulesCommand { get; }
        public ICommand MergeSelectedCategoriesCommand { get; }
        public ICommand ClearCategorySelectionCommand { get; }

        private readonly ImportService _importService;

        private readonly ExportService _exportService;

        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;
        private readonly IUiScheduler _uiScheduler;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel(
            CategoryManager categoryManager,
            IDialogService dialogService,
            IWindowService windowService,
            IUiScheduler uiScheduler)
        {
            _categoryManager = categoryManager;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
            _importService = new ImportService(_dialogService);
            _exportService = new ExportService(_dialogService);

            CategorySelections = new ObservableCollection<CategorySelectionItem>
            {
                new CategorySelectionItem(CategoryType.Kata, "Ката"),
                new CategorySelectionItem(CategoryType.Kumite, "Кумитэ"),
                new CategorySelectionItem(CategoryType.Kihon, "Кихон"),
                new CategorySelectionItem(CategoryType.KobudoShort, "Кобудо (короткое оружие)"),
                new CategorySelectionItem(CategoryType.KobudoLong, "Кобудо (длинное оружие)"),
                new CategorySelectionItem(CategoryType.Grappling, "Грэпплинг")
            };
            foreach (var selection in CategorySelections)
            {
                selection.PropertyChanged += (_, _) => CommandManager.InvalidateRequerySuggested();
            }

            AllParticipants = new ObservableCollection<Participant>();
            Categories = new ObservableCollection<CategoryViewModel>();

            AddParticipantCommand = new RelayCommand(o => CreateParticipant(), o => CanCreateParticipant());
            ImportCommand = new AsyncRelayCommand(ImportDataAsync, HandleAsyncCommandError);
            ExportCommand = new AsyncRelayCommand(
                ExportDataAsync,
                HandleAsyncCommandError,
                () => _categoryManager.DefinedCategories.Any());
            ImportExcelCommand = new AsyncRelayCommand(
                ImportExcelDataAsync,
                HandleAsyncCommandError,
                () => DivideByAge || DivideByBelt || DivideByWeight);
            CreateExcelTemplateCommand = new AsyncRelayCommand(CreateExcelTemplateAsync, HandleAsyncCommandError);
            ApplyGroupingRulesCommand = new RelayCommand(_ => ApplyGroupingRules(), _ => AllParticipants.Count > 0);
            MergeSelectedCategoriesCommand = new RelayCommand(_ => MergeSelectedCategories(), _ => SelectedCategoryCount >= 2);
            ClearCategorySelectionCommand = new RelayCommand(_ => ClearCategoryMergeSelection(), _ => SelectedCategoryCount > 0);
        }

        private void HandleAsyncCommandError(Exception exception)
        {
            _dialogService.ShowMessage(
                $"Произошла непредвиденная ошибка:\n{exception.Message}",
                "Ошибка",
                DialogButtons.Ok,
                DialogIcon.Error);
        }

        private bool CanCreateParticipant()
        {
            return !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName) &&
                   Age.HasValue && Age > 0 &&
                   SelectedSex.HasValue && SelectedBelt.HasValue &&
                   CategorySelections.Any(c => c.IsSelected) &&
                   (string.IsNullOrWhiteSpace(WeightKgText) || TryParseWeight(WeightKgText, out _));
        }

        private void CreateParticipant()
        {
            // Validate the current grouping rules before mutating the participant collection.
            if (!TryReadGroupingRanges(out var ageRanges, out var weightRanges)) return;
            if (!ConfirmRegroupIfCompetitionStarted()) return;

            var selectedCategories = CategorySelections
                .Where(c => c.IsSelected)
                .Select(c => c.CategoryType)
                .ToList();

            var newParticipant = new Participant(
                FirstName,
                LastName,
                Age.Value,
                SelectedBelt.Value,
                SelectedSex.Value,
                selectedCategories,
                Club,
                SelectedCountry
            )
            {
                WeightKg = TryParseWeight(WeightKgText, out var enteredWeight) ? enteredWeight : null
            };

            AllParticipants.Add(newParticipant);
            RebuildCategories(ageRanges, weightRanges);
            ResetForm();
        }

        private void ApplyGroupingRules()
        {
            if (!TryReadGroupingRanges(out var ageRanges, out var weightRanges)) return;
            if (!ConfirmRegroupIfCompetitionStarted()) return;

            RebuildCategories(ageRanges, weightRanges);
            GroupingSummary = $"Спортсменов: {AllParticipants.Count} · категорий: {Categories.Count} · одиночные категории сохранены";
        }

        private bool TryReadGroupingRanges(
            out IReadOnlyList<AgeGroupRange> ageRanges,
            out IReadOnlyList<WeightGroupRange> weightRanges)
        {
            ageRanges = Array.Empty<AgeGroupRange>();
            weightRanges = Array.Empty<WeightGroupRange>();

            try
            {
                if (DivideByAge)
                    ageRanges = GroupingRangeParser.ParseAgeRanges(AgeRangesText);

                if (DivideByWeight)
                    weightRanges = GroupingRangeParser.ParseWeightRanges(WeightRangesText);

                return true;
            }
            catch (FormatException ex)
            {
                _dialogService.ShowMessage(ex.Message, "Проверьте диапазоны", DialogButtons.Ok, DialogIcon.Warning);
                return false;
            }
        }

        private void RebuildCategories(
            IReadOnlyList<AgeGroupRange> ageRanges,
            IReadOnlyList<WeightGroupRange> weightRanges)
        {
            _categoryManager.RebuildCategories(
                AllParticipants,
                DivideByAge,
                DivideByBelt,
                DivideByWeight,
                ageRanges,
                weightRanges);

            RefreshCategories();
        }

        private bool ConfirmRegroupIfCompetitionStarted()
        {
            var hasCompetitionData = _categoryManager.DefinedCategories.Any(category =>
                category.BracketMatches.Count > 0 || category.IsFinished);
            if (!hasCompetitionData) return true;

            var confirmation = _dialogService.ShowMessage(
                "Перегруппировка пересоздаст категории и удалит уже сформированные сетки и результаты. Продолжить?",
                "Подтверждение перегруппировки", DialogButtons.YesNo, DialogIcon.Warning);
            return confirmation == DialogResult.Yes;
        }

        private static bool TryParseWeight(string? value, out decimal weight)
        {
            weight = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out weight) && weight > 0;
        }

        private void RefreshCategories()
        {
            Categories.Clear();
            foreach (var category in _categoryManager.DefinedCategories)
            {
                var categoryViewModel = new CategoryViewModel(
                    category,
                    OnDeleteRequested,
                    _dialogService,
                    _windowService,
                    _uiScheduler);
                categoryViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CategoryViewModel.IsSelectedForMerge))
                        OnCategoryMergeSelectionChanged();
                };
                Categories.Add(categoryViewModel);
            }
            RefreshVisibleCategories();
            OnCategoryMergeSelectionChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshVisibleCategories()
        {
            VisibleCategories.Clear();
            var primaryTypes = new[] { CategoryType.Kumite, CategoryType.Kata, CategoryType.Kihon };
            foreach (var category in Categories)
            {
                var categoryType = category.Category.CategoryType;
                var matchesDiscipline = SelectedDisciplineIndex switch
                {
                    0 => categoryType == CategoryType.Kumite,
                    1 => categoryType == CategoryType.Kata,
                    2 => categoryType == CategoryType.Kihon,
                    _ => !primaryTypes.Contains(categoryType)
                };
                var matchesSex = SelectedSexFilterIndex switch
                {
                    1 => category.Category.Sex is Sex.Male or Sex.Unisex,
                    2 => category.Category.Sex is Sex.Female or Sex.Unisex,
                    _ => true
                };
                if (matchesDiscipline && matchesSex)
                    VisibleCategories.Add(category);
            }
            OnPropertyChanged(nameof(VisibleCategories));
            OnPropertyChanged(nameof(IsCategoryListEmpty));
        }

        private void OnDeleteRequested(CategoryViewModel categoryToDelete)
        {
            var result = _dialogService.ShowMessage(
                $"Вы действительно хотите удалить эту категорию?\n\n" +
                $"Название: «{categoryToDelete.Name}»\n" +
                $"Спортсменов: {categoryToDelete.ParticipantCount}\n\n" +
                $"Внимание: это действие необратимо!\n" +
                $"Спортсмены останутся в системе, но будут удалены из этой категории.",
                "Подтвердите удаление категории", DialogButtons.YesNo, DialogIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _categoryManager.DefinedCategories.Remove(categoryToDelete.Category);
                
                RefreshCategories();
                
                _dialogService.ShowMessage($"Категория «{categoryToDelete.Name}» успешно удалена.",
                    "Успешно", DialogButtons.Ok, DialogIcon.Information);
            }
        }

        private void OnCategoryMergeSelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCategoryCount));
            OnPropertyChanged(nameof(HasMultipleCategoriesSelected));
            OnPropertyChanged(nameof(MergeSelectedButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearCategoryMergeSelection()
        {
            foreach (var category in Categories.Where(category => category.IsSelectedForMerge).ToList())
                category.IsSelectedForMerge = false;

            OnCategoryMergeSelectionChanged();
        }

        private void MergeSelectedCategories()
        {
            var selected = Categories.Where(category => category.IsSelectedForMerge).ToList();
            if (selected.Count < 2) return;

            var selectedCategories = selected.Select(item => item.Category).ToList();
            if (selectedCategories.Any(category =>
                category.CategoryType != selectedCategories[0].CategoryType
                || category.GetType() != selectedCategories[0].GetType()))
            {
                _dialogService.ShowMessage(
                    "Выбраны разные дисциплины. Объединять можно только категории одной дисциплины и одного типа.\n\n" +
                    "Снимите лишние отметки и повторите выбор.",
                    "Несовместимые категории", DialogButtons.Ok, DialogIcon.Warning);
                return;
            }

            if (selectedCategories.Any(category => category.BracketMatches.Count > 0
                || category.ThirdPlaceMatch != null
                || category.IsFinished
                || category.FinalResults.Count > 0
                || category.JudgingScores.Count > 0))
            {
                _dialogService.ShowMessage(
                    "В одной или нескольких выбранных категориях уже создана жеребьёвка или сохранены результаты.\n\n" +
                    "Такие категории нельзя объединять, чтобы не потерять турнирные данные.",
                    "Объединение недоступно", DialogButtons.Ok, DialogIcon.Warning);
                return;
            }

            var isTeamCategory = selectedCategories[0] is TeamCategory;
            var categoryNames = string.Join("\n", selected.Select((item, index) =>
                $"{index + 1}. {item.Name} — {(item.Category is TeamCategory teamCategory ? teamCategory.Teams.Count : item.ParticipantCount)} {(isTeamCategory ? "команд" : "чел.")}"));
            var totalParticipants = isTeamCategory
                ? selectedCategories.Cast<TeamCategory>().SelectMany(category => category.Teams).Select(team => team.Id).Distinct().Count()
                : selectedCategories.SelectMany(category => category.Participants).Select(participant => participant.Id).Distinct().Count();

            var confirmation = _dialogService.ShowMessage(
                $"Объединить {selected.Count} категорий в одну?\n\n{categoryNames}\n\n" +
                $"После объединения: {totalParticipants} уникальных {(isTeamCategory ? "команд" : "спортсменов")}.\n" +
                "Возрастные и весовые границы будут объединены; если выбраны мужская и женская группы, итоговая категория станет смешанной.",
                "Подтверждение объединения", DialogButtons.YesNo, DialogIcon.Question);

            if (confirmation != DialogResult.Yes) return;

            try
            {
                _categoryManager.MergeCategories(selected.Select(item => item.Category));
                RefreshCategories();
                _dialogService.ShowMessage(
                    $"Категории успешно объединены.\n{(isTeamCategory ? "Команд" : "Спортсменов")} в общей категории: {totalParticipants}.",
                    "Объединение завершено", DialogButtons.Ok, DialogIcon.Information);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowMessage(ex.Message, "Не удалось объединить категории", DialogButtons.Ok, DialogIcon.Warning);
            }
        }

        private void ResetForm()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            Age = null;
            Club = string.Empty;
            SelectedCountry = "Кыргызстан";
            WeightKgText = string.Empty;
            
            foreach (var category in CategorySelections)
            {
                category.IsSelected = false;
            }
        }

        private async Task ImportDataAsync()
        {
            var filePath = _dialogService.ShowOpenFileDialog(
                "Импорт данных турнира",
                "Файлы JSON (*.json)|*.json|Все файлы (*.*)|*.*");
            if (filePath == null)
            {
                return;
            }

            IsLoading = true;
            try
            {
                await _importService.ImportDataAsync(filePath, _categoryManager, AllParticipants);
                RefreshCategories();
                GroupingSummary = $"Загружено спортсменов: {AllParticipants.Count} · категорий: {Categories.Count}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportExcelDataAsync()
        {
            var filePath = _dialogService.ShowOpenFileDialog(
                "Импорт участников из Excel",
                "Файлы Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*");
            if (filePath == null) return;

            if (!TryReadGroupingRanges(out var ageRanges, out var weightRanges)) return;
            if (!ConfirmRegroupIfCompetitionStarted()) return;

            IsLoading = true;
            try
            {
                var addedCount = await _importService.ImportParticipantsFromExcelAsync(filePath, AllParticipants);
                if (addedCount > 0)
                {
                    RebuildCategories(ageRanges, weightRanges);
                    GroupingSummary = $"Импортировано: {addedCount} · всего спортсменов: {AllParticipants.Count} · категорий: {Categories.Count}";
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateExcelTemplateAsync()
        {
            await _importService.CreateSampleExcelFileAsync();
        }

        private async Task ExportDataAsync()
        {
            var filePath = _dialogService.ShowSaveFileDialog(
                "Экспорт данных турнира",
                "Файлы JSON (*.json)|*.json|Все файлы (*.*)|*.*",
                $"данные_турнира_{DateTime.Now:yyyy-MM-dd_HH-mm}.json");
            if (filePath == null)
            {
                return;
            }

            IsLoading = true;
            try
            {
                await _exportService.ExportDataAsync(filePath, _categoryManager);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}