using KarateClassLibrary;

namespace KarateTournamentApp.Localization;

/// <summary>
/// Russian labels for values shown in the user interface.
/// Enum names remain unchanged in JSON and Excel imports for compatibility.
/// </summary>
public static class RussianText
{
    public static string Belt(Belts belt) => belt switch
    {
        Belts.Kyu10 => "10 кю",
        Belts.Kyu9 => "9 кю",
        Belts.Kyu8 => "8 кю",
        Belts.Kyu7 => "7 кю",
        Belts.Kyu6 => "6 кю",
        Belts.Kyu5 => "5 кю",
        Belts.Kyu4 => "4 кю",
        Belts.Kyu3 => "3 кю",
        Belts.Kyu2 => "2 кю",
        Belts.Kyu1 => "1 кю",
        Belts.Dan => "Дан",
        _ => belt.ToString()
    };

    public static string Sex(Sex sex) => sex switch
    {
        KarateClassLibrary.Sex.Male => "Мужской",
        KarateClassLibrary.Sex.Female => "Женский",
        KarateClassLibrary.Sex.Unisex => "Смешанный",
        _ => sex.ToString()
    };

    public static string CategoryType(CategoryType categoryType) => categoryType switch
    {
        KarateClassLibrary.CategoryType.Kata => "Ката",
        KarateClassLibrary.CategoryType.Kumite => "Кумитэ",
        KarateClassLibrary.CategoryType.Kihon => "Кихон",
        KarateClassLibrary.CategoryType.KobudoShort => "Кобудо (короткое оружие)",
        KarateClassLibrary.CategoryType.KobudoLong => "Кобудо (длинное оружие)",
        KarateClassLibrary.CategoryType.Grappling => "Грэпплинг",
        KarateClassLibrary.CategoryType.Team => "Командное ката",
        _ => categoryType.ToString()
    };

    public static string DisplayEnum(object? value) => value switch
    {
        Belts belt => Belt(belt),
        KarateClassLibrary.Sex sex => Sex(sex),
        KarateClassLibrary.CategoryType categoryType => CategoryType(categoryType),
        PenaltyType penalty => Penalty(penalty),
        _ => value?.ToString() ?? string.Empty
    };

    public static string Penalty(PenaltyType penalty) => penalty switch
    {
        PenaltyType.Atenai => "Атэнай",
        PenaltyType.Chukoku => "Чукоку",
        _ => penalty.ToString()
    };

    public static string FormatAge(int age)
    {
        var lastTwo = age % 100;
        var last = age % 10;
        var unit = lastTwo is >= 11 and <= 14
            ? "лет"
            : last switch
            {
                1 => "год",
                2 or 3 or 4 => "года",
                _ => "лет"
            };
        return $"{age} {unit}";
    }

    public static string FormatAgeRange(int? minAge, int? maxAge)
    {
        if (minAge.HasValue && maxAge.HasValue)
        {
            if (minAge.Value == maxAge.Value)
                return FormatAge(minAge.Value);
            return $"{minAge.Value}–{maxAge.Value} лет";
        }

        if (minAge.HasValue)
            return $"от {FormatAge(minAge.Value)}";
        if (maxAge.HasValue)
            return $"до {FormatAge(maxAge.Value)}";
        return "Возраст не указан";
    }
}
