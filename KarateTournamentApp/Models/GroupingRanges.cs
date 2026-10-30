using System.Globalization;
using System.Text.RegularExpressions;

namespace KarateTournamentApp.Models;

public sealed record AgeGroupRange(int MinAge, int? MaxAge, string Label)
{
    public bool Contains(int age) => age >= MinAge && (!MaxAge.HasValue || age <= MaxAge.Value);
}

public sealed record WeightGroupRange(decimal MinKg, decimal? MaxKg, string Label, bool MinExclusive = false)
{
    public bool Contains(decimal weightKg)
    {
        // Weigh-in values with decimals are assigned to the next whole kilogram class.
        var competitionWeight = decimal.Ceiling(weightKg);
        var lowerBoundMatches = MinExclusive ? competitionWeight > MinKg : competitionWeight >= MinKg;
        return lowerBoundMatches && (!MaxKg.HasValue || competitionWeight <= MaxKg.Value);
    }
}

public static class GroupingRangeParser
{
    public const string DefaultAgeRanges = "до 5; 6-8; 9-10; 11-13; 14-15; 16; 17; 18; 19+";
    public const string DefaultWeightRanges = "до 25; 25-30; 31-35; 36-40; 41-45; 46-50; 51-55; 56-60; 61-65; 66-70; 71-80; 81+";

    public static IReadOnlyList<AgeGroupRange> ParseAgeRanges(string text)
    {
        var ranges = new List<AgeGroupRange>();
        foreach (var token in Split(text))
        {
            var value = token.Trim().Replace('–', '-').Replace('—', '-').ToLowerInvariant();
            value = Regex.Replace(value, @"\s*(лет|года|год)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            var openEnded = Regex.Match(value, @"^(\d+)\s*(?:\+|и\s+(?:выше|старше|более)|(?:выше|старше))$");
            if (openEnded.Success)
            {
                var min = ParseInt(openEnded.Groups[1].Value);
                ranges.Add(new AgeGroupRange(min, null, $"от {min} лет"));
            }
            else if (value.StartsWith("до ", StringComparison.Ordinal))
            {
                var max = ParseInt(value[3..]);
                ranges.Add(new AgeGroupRange(0, max, $"до {max} лет"));
            }
            else if (value.StartsWith("от ", StringComparison.Ordinal))
            {
                var min = ParseInt(value[3..]);
                ranges.Add(new AgeGroupRange(min, null, $"от {min} лет"));
            }
            else if (value.Contains('-'))
            {
                var parts = value.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length != 2) throw new FormatException($"Неверный возрастной диапазон: «{token}».");
                var min = ParseInt(parts[0]);
                var max = ParseInt(parts[1]);
                if (max < min) throw new FormatException($"В диапазоне «{token}» верхняя граница меньше нижней.");
                ranges.Add(new AgeGroupRange(min, max, min == max ? $"{min} лет" : $"{min}–{max} лет"));
            }
            else
            {
                var age = ParseInt(value);
                ranges.Add(new AgeGroupRange(age, age, $"{age} лет"));
            }
        }

        ValidateNoOverlap(ranges.OrderBy(x => x.MinAge).ToList(), x => x.MinAge, x => x.MaxAge, "возрасте");
        return ranges.OrderBy(x => x.MinAge).ToArray();
    }

    public static IReadOnlyList<WeightGroupRange> ParseWeightRanges(string text)
    {
        var ranges = new List<WeightGroupRange>();
        foreach (var token in Split(text))
        {
            var value = token.Trim().Replace('–', '-').Replace('—', '-').ToLowerInvariant();
            value = Regex.Replace(value, @"\s*(кг|килограмм(?:а|ов)?)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            var openEnded = Regex.Match(value, @"^(.+?)\s*(?:\+|и\s+(?:выше|более)|(?:выше|более))$");
            if (openEnded.Success)
            {
                var min = ParseDecimal(openEnded.Groups[1].Value);
                ranges.Add(new WeightGroupRange(min, null, $"от {FormatKg(min)} кг"));
            }
            else if (value.StartsWith("до ", StringComparison.Ordinal))
            {
                var max = ParseDecimal(value[3..]);
                ranges.Add(new WeightGroupRange(0, max, $"до {FormatKg(max)} кг"));
            }
            else if (value.StartsWith("от ", StringComparison.Ordinal))
            {
                var min = ParseDecimal(value[3..]);
                ranges.Add(new WeightGroupRange(min, null, $"от {FormatKg(min)} кг"));
            }
            else if (value.Contains('-'))
            {
                var parts = value.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length != 2) throw new FormatException($"Неверный весовой диапазон: «{token}».");
                var min = ParseDecimal(parts[0]);
                var max = ParseDecimal(parts[1]);
                if (max < min) throw new FormatException($"В диапазоне «{token}» верхняя граница меньше нижней.");
                ranges.Add(new WeightGroupRange(min, max, $"{FormatKg(min)}–{FormatKg(max)} кг"));
            }
            else
            {
                var weight = ParseDecimal(value);
                ranges.Add(new WeightGroupRange(weight, weight, $"{FormatKg(weight)} кг"));
            }
        }

        var ordered = ranges.OrderBy(x => x.MinKg).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            var previousMax = ordered[i - 1].MaxKg;
            if (!previousMax.HasValue || ordered[i].MinKg < previousMax.Value)
                throw new FormatException("Диапазоны пересекаются по весе. Проверьте границы.");

            // A shared boundary is assigned to the earlier (lighter) class. The next class starts above it.
            if (ordered[i].MinKg == previousMax.Value)
            {
                var label = ordered[i].MaxKg.HasValue
                    ? $"{FormatKg(ordered[i].MinKg)}–{FormatKg(ordered[i].MaxKg.Value)} кг"
                    : $"свыше {FormatKg(ordered[i].MinKg)} кг";
                ordered[i] = ordered[i] with { MinExclusive = true, Label = label };
            }
            if (ordered[i].MinExclusive && ordered[i].MaxKg.HasValue && ordered[i].MaxKg.Value <= ordered[i].MinKg)
                throw new FormatException($"Весовой диапазон «{ordered[i].Label}» не содержит ни одного значения.");
        }
        return ordered;
    }

    private static IEnumerable<string> Split(string text)
    {
        var tokens = Regex.Split(text ?? string.Empty, @"[;\r\n]+")
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        if (tokens.Length == 0) throw new FormatException("Укажите хотя бы один диапазон.");
        return tokens;
    }

    private static int ParseInt(string value)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 0)
            throw new FormatException($"«{value}» не является допустимым возрастом.");
        return result;
    }

    private static decimal ParseDecimal(string value)
    {
        var normalized = value.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) || result < 0)
            throw new FormatException($"«{value}» не является допустимым весом.");
        return result;
    }

    private static string FormatKg(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

    private static void ValidateNoOverlap<T, TNumber>(IReadOnlyList<T> ranges, Func<T, TNumber> min, Func<T, TNumber?> max, string dimension)
        where TNumber : struct, IComparable<TNumber>
    {
        for (var i = 1; i < ranges.Count; i++)
        {
            var previousMax = max(ranges[i - 1]);
            if (!previousMax.HasValue || min(ranges[i]).CompareTo(previousMax.Value) <= 0)
                throw new FormatException($"Диапазоны пересекаются по {dimension}. Проверьте границы.");
        }
    }
}
