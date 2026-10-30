using System.IO;
using System.Text.Json;
using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using Xunit;

namespace KarateTournamentApp.Tests;

public class JsonServiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"karate-tests-{Guid.NewGuid():N}.json");
    private readonly JsonService _service = new();

    public void Dispose() => File.Delete(_file);

    private static Participant Person() =>
        new("Łukasz", "Żółć", 12, Belts.Kyu8, Sex.Male, [CategoryType.Kata], "Łódź", "Кыргызстан") { Id = 42 };

    private async Task<Category> RoundTrip(Category category)
    {
        await _service.SaveTournamentDataAsync(_file, [category]);
        return Assert.Single(await _service.LoadTournamentDataAsync(_file));
    }

    [Fact]
    public async Task MissingFileReturnsEmptyTournament() =>
        Assert.Empty(await _service.LoadTournamentDataAsync(_file));

    [Fact]
    public async Task SavingAgainReplacesPreviousTournament()
    {
        await _service.SaveTournamentDataAsync(_file, [new Category { Name = "Old" }]);
        await _service.SaveTournamentDataAsync(_file, []);
        Assert.Empty(await _service.LoadTournamentDataAsync(_file));
    }

    [Fact]
    public async Task ParticipantAndResultsKeepIdentityPrecisionAndDiscardedNotes()
    {
        var person = Person();
        var category = new Category(Belts.Kyu8, CategoryType.Kata, Sex.Male, 10, 12)
        {
            Id = 7, Name = "Kata młodzików", Participants = [person], IsFinished = true,
            FinalResults = [new ParticipantResult
            {
                Participant = person, Score = 24.6m,
                JudgeScores = [7.1m, 8.2m, 9.3m, 6m, 10m], DiscardedJudgeScoreIndexes = [3, 4]
            }]
        };
        var loaded = await RoundTrip(category);
        var restoredPerson = Assert.Single(loaded.Participants);
        var restoredResult = Assert.Single(loaded.FinalResults);
        Assert.Equal(7, loaded.Id);
        Assert.Equal("Kata młodzików", loaded.Name);
        Assert.True(loaded.IsFinished);
        Assert.Equal((10, 12), (loaded.MinAge, loaded.MaxAge));
        Assert.Equal(Belts.Kyu8, Assert.Single(loaded.AllowedBelts));
        Assert.Equal("Łukasz", restoredPerson.FirstName);
        Assert.Equal("Żółć", restoredPerson.LastName);
        Assert.Equal("Łódź", restoredPerson.Club);
        Assert.Equal("Кыргызстан", restoredPerson.Country);
        Assert.Same(restoredPerson, restoredResult.Participant);
        Assert.Equal(24.6m, restoredResult.Score);
        Assert.Equal(category.FinalResults[0].JudgeScores, restoredResult.JudgeScores);
        Assert.Equal(new[] { 3, 4 }, restoredResult.DiscardedJudgeScoreIndexes);
    }

    [Fact]
    public async Task ShobuCategoryAndMatchKeepSpecializedState()
    {
        var category = new ShobuSanbonCategory
        {
            Participants = [Person()],
            BracketMatches = [new ShobuSanbonMatch
            {
                Id = 11, Aka = 42, WinnerId = 42, AkaScore = 3, ShiroScore = 1,
                IsFinished = true, IsDisqualification = true, TimeRemaining = 42.5,
                AtenaiAka = 1, AtenaiShiro = 2, ChukokuAka = 3, ChukokuShiro = 1,
                SenshuEnabled = false, HasSenshuAka = true, IsInOvertime = true, OvertimeCount = 2
            }]
        };
        var loaded = Assert.IsType<ShobuSanbonCategory>(await RoundTrip(category));
        var match = Assert.IsType<ShobuSanbonMatch>(Assert.Single(loaded.BracketMatches));
        Assert.Equal(11, match.Id);
        Assert.Equal(42, match.Aka);
        Assert.Null(match.Shiro);
        Assert.Equal(42, match.WinnerId);
        Assert.Equal((3, 1), ((int)match.AkaScore, (int)match.ShiroScore));
        Assert.True(match.IsFinished);
        Assert.True(match.IsDisqualification);
        Assert.Equal(42.5, match.TimeRemaining);
        Assert.Equal((1, 2, 3, 1), (match.AtenaiAka, match.AtenaiShiro, match.ChukokuAka, match.ChukokuShiro));
        Assert.False(match.SenshuEnabled);
        Assert.True(match.HasSenshuAka);
        Assert.True(match.IsInOvertime);
        Assert.Equal(2, match.OvertimeCount);
    }

    [Fact]
    public async Task ThirdPlaceBoutAndShobuStateSurviveSaveAndLoad()
    {
        var category = new ShobuSanbonCategory
        {
            Participants = [Person(), new Participant("Ivan", "Test", 12, Belts.Kyu8, Sex.Male, [CategoryType.Kumite]) { Id = 43 }],
            ThirdPlaceMatch = new ShobuSanbonMatch
            {
                Id = 17, Aka = 42, Shiro = 43, WinnerId = 42,
                AkaScore = 3, ShiroScore = 1, IsFinished = true,
                TimeRemaining = 12.5, IsInOvertime = true, OvertimeCount = 1
            }
        };

        var loaded = Assert.IsType<ShobuSanbonCategory>(await RoundTrip(category));
        var bronze = Assert.IsType<ShobuSanbonMatch>(loaded.ThirdPlaceMatch);
        Assert.Equal(17, bronze.Id);
        Assert.Equal(42, bronze.Aka);
        Assert.Equal(43, bronze.Shiro);
        Assert.Equal(42, bronze.WinnerId);
        Assert.True(bronze.IsFinished);
        Assert.Equal(12.5, bronze.TimeRemaining);
        Assert.True(bronze.IsInOvertime);
        Assert.Equal(1, bronze.OvertimeCount);
    }

    [Fact]
    public async Task TeamMembersShareCanonicalParticipantAfterLoading()
    {
        var person = Person();
        var category = new TeamCategory
        {
            Participants = [person],
            Teams = [new Team("Łódź A", Sex.Male) { Id = 8, Members = [person] }]
        };
        var loaded = Assert.IsType<TeamCategory>(await RoundTrip(category));
        var team = Assert.Single(loaded.Teams);
        Assert.Equal(8, team.Id);
        Assert.Equal("Łódź A", team.Name);
        Assert.Equal(Sex.Male, team.Sex);
        Assert.Same(Assert.Single(loaded.Participants), Assert.Single(team.Members));
    }

    [Fact]
    public async Task RawJudgingScoresSurviveSaveAndLoad()
    {
        var category = new Category
        {
            Participants = [Person()],
            JudgingScores = [(new List<decimal> { 7.1m, 8.2m, 9.3m }, 42)]
        };
        var loaded = await RoundTrip(category);
        var scores = Assert.Single(loaded.JudgingScores);
        Assert.Equal(42, scores.ParticipantId);
        Assert.Equal(new decimal[] { 7.1m, 8.2m, 9.3m }, scores.Scores);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("[{\"$type\":\"unsupported\"}]")]
    public async Task MalformedOrUnsupportedJsonIsRejectedWithoutChangingFile(string json)
    {
        await File.WriteAllTextAsync(_file, json);
        await Assert.ThrowsAsync<JsonException>(() => _service.LoadTournamentDataAsync(_file));
        Assert.Equal(json, await File.ReadAllTextAsync(_file));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"Participants\":null}]")]
    [InlineData("[{\"BracketMatches\":[{\"Aka\":99}]}]")]
    public async Task IncompleteGraphOrMissingReferenceIsRejected(string json)
    {
        await File.WriteAllTextAsync(_file, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.LoadTournamentDataAsync(_file));
    }
}
