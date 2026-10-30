using KarateClassLibrary;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class ParticipantImportParserTests
{
    [Fact]
    public void ParsesCaseInsensitiveEnumNamesAndPreservesClub()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "kyu8", "male", ["kata", "KUMITE"], "Klub");
        Assert.NotNull(participant);
        Assert.Equal("Jan", participant.FirstName);
        Assert.Equal("Kowalski", participant.LastName);
        Assert.Equal(12, participant.Age);
        Assert.Equal(Belts.Kyu8, participant.Belt);
        Assert.Equal(Sex.Male, participant.Sex);
        Assert.Equal(new[] { CategoryType.Kata, CategoryType.Kumite }, participant.Categories);
        Assert.Equal("Klub", participant.Club);
    }

    [Theory]
    [InlineData(null, "Kowalski", "12", "Kyu8", "Male")]
    [InlineData(" ", "Kowalski", "12", "Kyu8", "Male")]
    public void RejectsMissingRequiredValues(
        string? firstName, string? lastName, string? age, string? belt, string? sex) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            firstName, lastName, age, belt, sex, ["Kata"], null));

    [Theory]
    [InlineData("0")]
    [InlineData("12.5")]
    [InlineData("2147483648")]
    public void RejectsNonPositiveNonIntegerOrOverflowingAge(string age) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", age, "Kyu8", "Male", ["Kata"], null));

    [Theory]
    [InlineData("99", "Male")]
    [InlineData("Kyu8", "0")]
    [InlineData("Kyu8", "99")]
    public void RejectsUndefinedBeltOrSex(string belt, string sex) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", belt, sex, ["Kata"], null));

    [Fact]
    public void IgnoresInvalidDisciplinesWhenAtLeastOneValidDisciplineRemains()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", [null, "unknown", "99", "Kata"], null);
        Assert.NotNull(participant);
        Assert.Equal(new[] { CategoryType.Kata }, participant.Categories);
    }

    [Fact]
    public void RejectsParticipantWithoutAnySupportedDiscipline() =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", ["99"], null));

    [Fact]
    public void AcceptsDefinedNumericEnumsIncludingZeroValuedDisciplineAndBelt()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "0", "1", ["0"], null);
        Assert.NotNull(participant);
        Assert.Equal(Belts.Dan, participant.Belt);
        Assert.Equal(CategoryType.Kata, Assert.Single(participant.Categories));
    }

    [Fact]
    public void ParsesOptionalWeightUsingCommaOrDotDecimalSeparator()
    {
        var comma = ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", ["Kumite"], "Club", "52,5");
        var dot = ParticipantImportParser.TryCreateParticipant(
            "Anna", "Nowak", "13", "Kyu7", "Female", ["Kumite"], null, "48.25");

        Assert.Equal(52.5m, comma?.WeightKg);
        Assert.Equal(48.25m, dot?.WeightKg);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public void RejectsInvalidProvidedWeight(string weight) =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", ["Kumite"], null, weight));
    [Fact]
    public void ParsesRussianExcelDropdownValuesAndPreservesCountry()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Айбек", "Садыков", "10", "7 кю", "Мужской",
            ["Кумитэ", "Ката", "Кихон"], "Додзё Ала-Тоо", "32,5", "Кыргызстан");

        Assert.NotNull(participant);
        Assert.Equal(Belts.Kyu7, participant.Belt);
        Assert.Equal(Sex.Male, participant.Sex);
        Assert.Equal(new[] { CategoryType.Kumite, CategoryType.Kata, CategoryType.Kihon }, participant.Categories);
        Assert.Equal(32.5m, participant.WeightKg);
        Assert.Equal("Кыргызстан", participant.Country);
    }


    [Fact]
    public void RejectsTeamDisciplineInIndividualParticipantImport() =>
        Assert.Null(ParticipantImportParser.TryCreateParticipant(
            "Jan", "Kowalski", "12", "Kyu8", "Male", ["Командное ката"], null));

    [Fact]
    public void CountryIsOptionalAndDoesNotBreakLegacyImports()
    {
        var participant = ParticipantImportParser.TryCreateParticipant(
            "Anna", "Nowak", "12", "Kyu8", "Female", ["Kata"], "Club");

        Assert.NotNull(participant);
        Assert.Null(participant.Country);
    }

}
