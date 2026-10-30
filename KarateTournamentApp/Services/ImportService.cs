using KarateTournamentApp.Models;
using KarateTournamentApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KarateTournamentApp.Services.Dialogs;

namespace KarateTournamentApp.Services
{
    public class ImportService
    {
        private readonly JsonService _jsonService;
        private readonly ExcelImportService _excelImportService;
        private readonly IDialogService _dialogService;

        public ImportService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _jsonService = new JsonService();
            _excelImportService = new ExcelImportService();
        }

        public async Task<int> ImportParticipantsFromExcelAsync(
            string filePath,
            ObservableCollection<Participant> allParticipants)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Не указан путь к файлу импорта.", nameof(filePath));

            var importResult = await _excelImportService.ImportParticipantsFromExcelAsync(filePath);
            if (importResult.Participants.Count == 0)
            {
                _dialogService.ShowMessage(
                    $"В файле нет корректных данных спортсменов.\n" +
                    $"Некорректных строк: {importResult.InvalidRows} из {importResult.TotalRows}.",
                    "Импорт не выполнен", DialogButtons.Ok, DialogIcon.Warning);
                return 0;
            }

            var addedCount = 0;
            foreach (var participant in importResult.Participants)
            {
                var isDuplicate = allParticipants.Any(existing =>
                    string.Equals(existing.FirstName?.Trim(), participant.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.LastName?.Trim(), participant.LastName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (isDuplicate) continue;

                allParticipants.Add(participant);
                addedCount++;
            }

            var duplicateCount = importResult.Participants.Count - addedCount;
            var skippedCount = duplicateCount + importResult.InvalidRows;
            _dialogService.ShowMessage(
                $"Добавлено спортсменов: {addedCount} из {importResult.TotalRows}.\n" +
                $"Пропущено всего: {skippedCount}.\n" +
                $"Дубликаты: {duplicateCount}; некорректные строки: {importResult.InvalidRows}.",
                "Импорт завершён", DialogButtons.Ok, DialogIcon.Information);

            return addedCount;
        }

        public async Task CreateSampleExcelFileAsync()
        {
            var filePath = _dialogService.ShowSaveFileDialog(
                "Сохранение шаблона Excel",
                "Файлы Excel (*.xlsx)|*.xlsx",
                "Шаблон_участников.xlsx");
            if (filePath == null)
            {
                return;
            }

            try
            {
                await _excelImportService.CreateSampleExcelFileAsync(filePath);
                _dialogService.ShowMessage($"Шаблон Excel сохранён.\n\nПуть: {filePath}",
                    "Успешно", DialogButtons.Ok, DialogIcon.Information);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при создании шаблона:\n{ex.Message}",
                    "Ошибка", DialogButtons.Ok, DialogIcon.Error);
            }
        }

        public async Task ImportDataAsync(
            string filePath,
            CategoryManager categoryManager,
            ObservableCollection<Participant> allParticipants)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Не указан путь к файлу импорта.", nameof(filePath));
            }

            try
            {
                var importedCategories = await _jsonService.LoadTournamentDataAsync(filePath);

                if (importedCategories != null && importedCategories.Any())
                {
                    categoryManager.DefinedCategories.Clear();
                    categoryManager.DefinedCategories.AddRange(importedCategories);

                    allParticipants.Clear();
                    foreach (var category in importedCategories)
                    {
                        foreach (var participant in category.Participants)
                        {
                            if (!allParticipants.Any(p => p.Id == participant.Id))
                            {
                                allParticipants.Add(participant);
                            }
                        }
                    }

                    _dialogService.ShowMessage($"Импортировано категорий: {importedCategories.Count}; спортсменов: {allParticipants.Count}.",
                        "Импорт завершён", DialogButtons.Ok, DialogIcon.Information);
                }
                else
                {
                    _dialogService.ShowMessage("В файле нет данных.",
                        "Ошибка", DialogButtons.Ok, DialogIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при импорте данных:\n{ex.Message}",
                    "Ошибка", DialogButtons.Ok, DialogIcon.Error);
            }
        }
    }
}
