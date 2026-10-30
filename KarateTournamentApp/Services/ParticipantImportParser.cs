using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services
{
    internal static class ParticipantImportParser
    {
        public static Participant? TryCreateParticipant(
            string? firstName,
            string? lastName,
            string? ageText,
            string? beltText,
            string? sexText,
            IEnumerable<string?> categoryValues,
            string? club,
            string? weightText = null,
            string? country = null)
        {
            if (string.IsNullOrWhiteSpace(firstName)
                || string.IsNullOrWhiteSpace(lastName)
                || string.IsNullOrWhiteSpace(ageText)
                || string.IsNullOrWhiteSpace(beltText)
                || string.IsNullOrWhiteSpace(sexText))
            {
                return null;
            }

            if (!int.TryParse(ageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var age) || age <= 0)
                return null;

            if (!TryParseBelt(beltText, out var belt) || !TryParseSex(sexText, out var sex))
                return null;

            decimal? weightKg = null;
            if (!string.IsNullOrWhiteSpace(weightText))
            {
                var normalizedWeight = weightText.Trim().Replace(',', '.');
                if (!decimal.TryParse(normalizedWeight, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedWeight)
                    || parsedWeight <= 0)
                    return null;
                weightKg = parsedWeight;
            }

            var categories = new List<CategoryType>();
            foreach (var categoryValue in categoryValues)
            {
                if (string.IsNullOrWhiteSpace(categoryValue)) break;
                if (TryParseCategory(categoryValue, out var categoryType) && !categories.Contains(categoryType))
                    categories.Add(categoryType);
            }

            if (categories.Count == 0) return null;

            return new Participant(
                firstName.Trim(), lastName.Trim(), age, belt, sex, categories,
                string.IsNullOrWhiteSpace(club) ? null : club.Trim(),
                string.IsNullOrWhiteSpace(country) ? null : country.Trim())
            {
                WeightKg = weightKg
            };
        }

        private static bool TryParseBelt(string value, out Belts belt)
        {
            if (Enum.TryParse(value, true, out belt) && Enum.IsDefined(belt)) return true;
            var normalized = Normalize(value);
            var map = new Dictionary<string, Belts>(StringComparer.OrdinalIgnoreCase)
            {
                ["10кю"] = Belts.Kyu10, ["9кю"] = Belts.Kyu9, ["8кю"] = Belts.Kyu8,
                ["7кю"] = Belts.Kyu7, ["6кю"] = Belts.Kyu6, ["5кю"] = Belts.Kyu5,
                ["4кю"] = Belts.Kyu4, ["3кю"] = Belts.Kyu3, ["2кю"] = Belts.Kyu2,
                ["1кю"] = Belts.Kyu1, ["дан"] = Belts.Dan
            };
            return map.TryGetValue(normalized, out belt);
        }

        private static bool TryParseSex(string value, out Sex sex)
        {
            if (Enum.TryParse(value, true, out sex) && Enum.IsDefined(sex)) return true;
            switch (Normalize(value))
            {
                case "мужской": case "муж": sex = Sex.Male; return true;
                case "женский": case "жен": sex = Sex.Female; return true;
                case "смешанный": case "общий": sex = Sex.Unisex; return true;
                default: sex = default; return false;
            }
        }

        private static bool TryParseCategory(string value, out CategoryType category)
        {
            if (Enum.TryParse(value, true, out category) && Enum.IsDefined(category) && category != CategoryType.Team) return true;
            switch (Normalize(value))
            {
                case "ката": category = CategoryType.Kata; return true;
                case "кумите": case "кумитэ": category = CategoryType.Kumite; return true;
                case "кихон": category = CategoryType.Kihon; return true;
                case "кобудокороткоеоружие": category = CategoryType.KobudoShort; return true;
                case "кобудодлинноеоружие": category = CategoryType.KobudoLong; return true;
                case "грэпплинг": case "грепплинг": category = CategoryType.Grappling; return true;
                default: category = default; return false;
            }
        }

        private static string Normalize(string value) =>
            new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
