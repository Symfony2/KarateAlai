using KarateTournamentApp.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace KarateTournamentApp.Services
{
    public sealed record ExcelParticipantImportResult(
        IReadOnlyList<Participant> Participants,
        int TotalRows,
        int InvalidRows);

    public class ExcelImportService
    {
        public ExcelImportService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Asynchronously imports participants from Excel file
        /// Excel format (current Russian template):
        /// A: Имя, B: Фамилия, C: Возраст, D: Страна (optional), E: Клуб (optional),
        /// F: Пояс, G: Пол, H: Вес (кг, optional), I+: Категория 1, Категория 2, ...
        /// Legacy English templates are also supported through header names.
        /// First row contains headers and is skipped.
        /// </summary>
        public async Task<ExcelParticipantImportResult> ImportParticipantsFromExcelAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var participants = new List<Participant>();
                var totalRows = 0;
                var invalidRows = 0;

                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);

                    using (ExcelPackage package = new ExcelPackage(fileInfo))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension?.Rows ?? 0;
                        int columnCount = worksheet.Dimension?.Columns ?? 0;

                        totalRows = Math.Max(0, rowCount - 1);
                        if (rowCount < 2 || columnCount < 6)
                        {
                            invalidRows = totalRows;
                            return new ExcelParticipantImportResult(participants, totalRows, invalidRows);
                        }

                        var columns = BuildColumnMap(worksheet, columnCount);
                        var firstNameColumn = FindColumn(columns, "firstname", "имя");
                        var lastNameColumn = FindColumn(columns, "lastname", "фамилия");
                        var ageColumn = FindColumn(columns, "age", "возраст");
                        var clubColumn = FindColumn(columns, "club", "клуб");
                        var countryColumn = FindColumn(columns, "country", "страна", "представительство");
                        var beltColumn = FindColumn(columns, "belt", "пояс", "разряд");
                        var sexColumn = FindColumn(columns, "sex", "gender", "пол");
                        var weightColumn = FindColumn(columns, "weightkg", "weight", "вес", "вескг", "вескг.", "вес(кг)", "вескг", "масса");

                        // Prefer explicitly named category columns. If a legacy workbook has generic
                        // headers, preserve the old convention: categories start at G, except Weight.
                        var categoryColumns = Enumerable.Range(1, columnCount)
                            .Where(column => IsCategoryHeader(NormalizeHeader(worksheet.Cells[1, column].Text)))
                            .ToArray();
                        if (categoryColumns.Length == 0)
                        {
                            categoryColumns = Enumerable.Range(7, Math.Max(0, columnCount - 6))
                                .Where(column => column != weightColumn)
                                .ToArray();
                        }

                        if (firstNameColumn == 0 || lastNameColumn == 0 || ageColumn == 0 || beltColumn == 0 || sexColumn == 0)
                        {
                            throw new InvalidDataException("Не найдены обязательные заголовки: Имя, Фамилия, Возраст, Пояс и Пол.");
                        }

                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                var participant = ParticipantImportParser.TryCreateParticipant(
                                    worksheet.Cells[row, firstNameColumn].Text?.Trim(),
                                    worksheet.Cells[row, lastNameColumn].Text?.Trim(),
                                    worksheet.Cells[row, ageColumn].Text?.Trim(),
                                    worksheet.Cells[row, beltColumn].Text?.Trim(),
                                    worksheet.Cells[row, sexColumn].Text?.Trim(),
                                    ReadCategoryValues(worksheet, row, categoryColumns),
                                    clubColumn > 0 ? worksheet.Cells[row, clubColumn].Text?.Trim() : null,
                                    weightColumn > 0 ? worksheet.Cells[row, weightColumn].Text?.Trim() : null,
                                    countryColumn > 0 ? worksheet.Cells[row, countryColumn].Text?.Trim() : null);

                                if (participant != null)
                                {
                                    participants.Add(participant);
                                }
                                else
                                {
                                    invalidRows++;
                                }
                            }
                            catch (Exception)
                            {
                                invalidRows++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Не удалось обработать файл Excel: {ex.Message}", ex);
                }

                return new ExcelParticipantImportResult(participants, totalRows, invalidRows);
            });
        }

        private static Dictionary<string, int> BuildColumnMap(ExcelWorksheet worksheet, int columnCount)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column <= columnCount; column++)
            {
                var header = NormalizeHeader(worksheet.Cells[1, column].Text);
                if (!string.IsNullOrWhiteSpace(header) && !result.ContainsKey(header))
                {
                    result.Add(header, column);
                }
            }
            return result;
        }

        private static int FindColumn(Dictionary<string, int> columns, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                var normalized = NormalizeHeader(alias);
                if (columns.TryGetValue(normalized, out var column)) return column;
            }
            return 0;
        }

        private static string NormalizeHeader(string? value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool IsCategoryHeader(string header)
        {
            const string englishPrefix = "category";
            const string russianPrefix = "категория";

            if (header == englishPrefix || header == russianPrefix) return true;
            if (header.StartsWith(englishPrefix, StringComparison.Ordinal))
                return header[englishPrefix.Length..].All(char.IsDigit);
            if (header.StartsWith(russianPrefix, StringComparison.Ordinal))
                return header[russianPrefix.Length..].All(char.IsDigit);
            return false;
        }

        private static IEnumerable<string?> ReadCategoryValues(ExcelWorksheet worksheet, int row, IReadOnlyList<int> categoryColumns)
        {
            foreach (var column in categoryColumns)
            {
                var value = worksheet.Cells[row, column].Text?.Trim();
                if (string.IsNullOrWhiteSpace(value)) yield break;
                yield return value;
            }
        }

        /// <summary>
        /// Creates an Excel import template with Russian dropdowns and ten fictional sample participants.
        /// </summary>
        public async Task CreateSampleExcelFileAsync(string filePath)
        {
            await Task.Run(() =>
            {
                using var package = new ExcelPackage(new FileInfo(filePath));
                var worksheet = package.Workbook.Worksheets.Add("Участники");
                var instructions = package.Workbook.Worksheets.Add("Инструкция");
                var lists = package.Workbook.Worksheets.Add("Справочники");

                var headers = new[]
                {
                    "Имя", "Фамилия", "Возраст", "Страна", "Клуб", "Пояс", "Пол",
                    "Вес (кг)", "Категория 1", "Категория 2", "Категория 3"
                };
                for (var col = 0; col < headers.Length; col++)
                    worksheet.Cells[1, col + 1].Value = headers[col];

                var samples = new object?[,]
                {
                    { "Айбек", "Садыков", 10, "Кыргызстан", "Додзё Ала-Тоо", "7 кю", "Мужской", 32.4m, "Кумитэ", "Ката", "Кихон" },
                    { "Алина", "Ибраимова", 9, "Кыргызстан", "Додзё Ала-Тоо", "8 кю", "Женский", 28.0m, "Кумитэ", "Ката", null },
                    { "Нурбек", "Токтосунов", 12, "Казахстан", "Алматы Додзё", "5 кю", "Мужской", 42.6m, "Кумитэ", "Кихон", null },
                    { "Медина", "Абдиева", 11, "Узбекистан", "Ташкент Карате", "6 кю", "Женский", 36.1m, "Ката", "Кихон", null },
                    { "Данияр", "Касымов", 14, "Россия", "Школа Восток", "3 кю", "Мужской", 54.8m, "Кумитэ", "Грэпплинг", null },
                    { "Самира", "Рахимова", 13, "Таджикистан", "Душанбе Шотокан", "4 кю", "Женский", 47.0m, "Кумитэ", "Ката", "Кобудо (короткое оружие)" },
                    { "Эрлан", "Маматов", 8, "Кыргызстан", "Додзё Ала-Тоо", "9 кю", "Мужской", 25.5m, "Ката", null, null },
                    { "Аружан", "Нурланова", 15, "Казахстан", "Алматы Додзё", "2 кю", "Женский", 58.2m, "Кумитэ", "Кихон", null },
                    { "Тимур", "Юлдашев", 16, "Узбекистан", "Ташкент Карате", "1 кю", "Мужской", 67.3m, "Кумитэ", "Ката", "Кихон" },
                    { "Малика", "Шарипова", 12, "Таджикистан", "Душанбе Шотокан", "5 кю", "Женский", 41.4m, "Ката", "Грэпплинг", null }
                };

                for (var row = 0; row < samples.GetLength(0); row++)
                    for (var col = 0; col < samples.GetLength(1); col++)
                        worksheet.Cells[row + 2, col + 1].Value = samples[row, col];

                var countries = new[] { "Кыргызстан", "Казахстан", "Узбекистан", "Россия", "Таджикистан", "Другая страна" };
                var belts = new[] { "10 кю", "9 кю", "8 кю", "7 кю", "6 кю", "5 кю", "4 кю", "3 кю", "2 кю", "1 кю", "Дан" };
                var sexes = new[] { "Мужской", "Женский", "Смешанный" };
                // Individual disciplines. Team entries are managed through the dedicated team workflow.
                var categories = new[] { "Кумитэ", "Ката", "Кихон", "Кобудо (короткое оружие)", "Кобудо (длинное оружие)", "Грэпплинг" };

                WriteList(lists, 1, "Страны", countries);
                WriteList(lists, 2, "Пояса", belts);
                WriteList(lists, 3, "Пол", sexes);
                WriteList(lists, 4, "Дисциплины", categories);

                package.Workbook.Names.Add("CountryOptions", lists.Cells["A2:A7"]);
                package.Workbook.Names.Add("BeltOptions", lists.Cells["B2:B12"]);
                package.Workbook.Names.Add("SexOptions", lists.Cells["C2:C4"]);
                package.Workbook.Names.Add("CategoryOptions", lists.Cells["D2:D7"]);

                AddListValidation(worksheet, "D2:D201", "CountryOptions");
                AddListValidation(worksheet, "F2:F201", "BeltOptions");
                AddListValidation(worksheet, "G2:G201", "SexOptions");
                AddListValidation(worksheet, "I2:K201", "CategoryOptions");

                var ageValidation = worksheet.DataValidations.AddIntegerValidation("C2:C201");
                ageValidation.Operator = OfficeOpenXml.DataValidation.ExcelDataValidationOperator.greaterThan;
                ageValidation.Formula.Value = 0;
                ageValidation.ShowErrorMessage = true;
                ageValidation.ErrorTitle = "Некорректный возраст";
                ageValidation.Error = "Укажите целый возраст больше нуля.";

                var weightValidation = worksheet.DataValidations.AddDecimalValidation("H2:H201");
                weightValidation.Operator = OfficeOpenXml.DataValidation.ExcelDataValidationOperator.greaterThan;
                weightValidation.Formula.Value = 0;
                weightValidation.AllowBlank = true;
                weightValidation.ShowErrorMessage = true;
                weightValidation.ErrorTitle = "Некорректный вес";
                weightValidation.Error = "Вес должен быть положительным числом в килограммах.";

                // Participant sheet styling.
                using (var header = worksheet.Cells[1, 1, 1, headers.Length])
                {
                    header.Style.Font.Bold = true;
                    header.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    header.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(36, 73, 112));
                    header.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    header.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }
                worksheet.Cells[2, 1, samples.GetLength(0) + 1, headers.Length].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                worksheet.Cells[2, 3, 201, 3].Style.Numberformat.Format = "0";
                worksheet.Cells[2, 8, 201, 8].Style.Numberformat.Format = "0.0";
                worksheet.Cells[1, 1, 201, headers.Length].AutoFilter = true;
                worksheet.View.FreezePanes(2, 1);
                worksheet.Column(1).Width = 16;
                worksheet.Column(2).Width = 20;
                worksheet.Column(3).Width = 11;
                worksheet.Column(4).Width = 17;
                worksheet.Column(5).Width = 24;
                worksheet.Column(6).Width = 12;
                worksheet.Column(7).Width = 14;
                worksheet.Column(8).Width = 12;
                for (var col = 9; col <= 11; col++) worksheet.Column(col).Width = 30;
                worksheet.Row(1).Height = 28;
                worksheet.Cells[1, 1, 201, headers.Length].Style.WrapText = true;

                // Instructions sheet.
                instructions.Cells["A1"].Value = "ШАБЛОН ДЛЯ ИМПОРТА СПОРТСМЕНОВ";
                instructions.Cells["A1"].Style.Font.Size = 18;
                instructions.Cells["A1"].Style.Font.Bold = true;
                instructions.Cells["A3"].Value = "В файле уже есть 10 вымышленных примеров. Замените их своими участниками или удалите строки-примеры.";
                instructions.Cells["A4"].Value = "Поля Имя, Фамилия, Возраст, Пояс, Пол и минимум одна дисциплина обязательны.";
                instructions.Cells["A5"].Value = "Страна и клуб необязательны. Вес указывайте в килограммах; для Кумитэ он используется при группировке.";
                instructions.Cells["A6"].Value = "В колонках Страна, Пояс, Пол и Категории есть выпадающие списки. Возраст и вес проверяются при вводе.";
                instructions.Cells["A7"].Value = "Можно выбрать до трёх дисциплин у одного спортсмена. Если нужно больше, добавьте справа столбцы Категория 4, Категория 5 и т. д.";
                instructions.Cells["A8"].Value = "Не переименовывайте заголовки. Для импорта сохраняйте первый лист «Участники» первым в книге.";
                instructions.Cells["A9"].Value = "Дисциплины: Кумитэ, Ката, Кихон, Кобудо (короткое оружие), Кобудо (длинное оружие), Грэпплинг. Командные заявки оформляются отдельно.";
                instructions.Cells["A10"].Value = "Пояса: 10 кю — 1 кю, Дан. Пол: Мужской, Женский, Смешанный.";
                instructions.Cells["A12"].Value = "Лист «Справочники» содержит варианты для выпадающих списков. Его можно дополнить своими клубами или странами, если потребуется.";
                instructions.Column(1).Width = 115;
                instructions.Cells["A1:A12"].Style.WrapText = true;
                instructions.Cells["A3:A12"].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                instructions.Row(3).Height = 34;
                for (var r = 4; r <= 12; r++) instructions.Row(r).Height = 28;

                // Reference list sheet.
                lists.Cells[1, 1, 1, 4].Style.Font.Bold = true;
                lists.Cells[1, 1, 1, 4].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                lists.Cells[1, 1, 1, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(226, 235, 245));
                lists.Cells.AutoFitColumns();
                lists.View.FreezePanes(2, 1);

                package.Save();
            });
        }

        private static void WriteList(ExcelWorksheet sheet, int column, string header, IReadOnlyList<string> values)
        {
            sheet.Cells[1, column].Value = header;
            for (var index = 0; index < values.Count; index++)
                sheet.Cells[index + 2, column].Value = values[index];
        }

        private static void AddListValidation(ExcelWorksheet sheet, string address, string name)
        {
            var validation = sheet.DataValidations.AddListValidation(address);
            validation.Formula.ExcelFormula = name;
            validation.AllowBlank = true;
            validation.ShowErrorMessage = true;
            validation.ErrorTitle = "Выберите значение из списка";
            validation.Error = "Используйте один из вариантов выпадающего списка.";
        }
    }
}
