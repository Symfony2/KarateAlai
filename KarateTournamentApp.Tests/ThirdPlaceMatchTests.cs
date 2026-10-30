using KarateClassLibrary;
using KarateTournamentApp.Models;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.ViewModels;
using Xunit;

namespace KarateTournamentApp.Tests;

public class ThirdPlaceMatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThirdPlaceMatchIsCreatedFromSemifinalLosers(bool shobu)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        category.Participants.Add(new Participant { Id = 1 });
        category.Participants.Add(new Participant { Id = 2 });
        category.Participants.Add(new Participant { Id = 3 });
        category.Participants.Add(new Participant { Id = 4 });
        category.InitializeBracket();

        var firstSemi = category.BracketMatches[1];
        var secondSemi = category.BracketMatches[2];
        firstSemi.WinnerId = firstSemi.Aka;
        firstSemi.IsFinished = true;
        secondSemi.WinnerId = secondSemi.Shiro;
        secondSemi.IsFinished = true;

        category.EnsureThirdPlaceMatch();

        var bronze = Assert.IsAssignableFrom<Match>(category.ThirdPlaceMatch);
        Assert.Equal(firstSemi.Shiro, bronze.Aka);
        Assert.Equal(secondSemi.Aka, bronze.Shiro);
        Assert.False(bronze.IsFinished);
        if (shobu)
        {
            Assert.IsType<ShobuSanbonMatch>(bronze);
        }
    }

    [Fact]
    public void StartingKumiteCompetitionPreservesAnAlreadyGeneratedDraw()
    {
        var category = new Category { CategoryType = CategoryType.Kumite };
        for (var id = 1; id <= 8; id++)
        {
            category.Participants.Add(new Participant { Id = id });
        }
        category.InitializeBracket();
        var originalPairings = category.BracketMatches
            .Select(match => (match.Aka, match.Shiro))
            .ToArray();

        _ = new CompetitionManagerViewModel(category, new NoOpDialogService());

        Assert.Equal(originalPairings, category.BracketMatches.Select(match => (match.Aka, match.Shiro)).ToArray());
        Assert.All(category.BracketMatches, match => Assert.IsType<ShobuSanbonMatch>(match));
    }

    [Fact]
    public void ThirdPlaceMatchIsNotCreatedForSemifinalBye()
    {
        var category = new Category
        {
            BracketMatches =
            [
                new Match(),
                new Match(1, 2) { WinnerId = 1, IsFinished = true },
                new Match(3, null) { WinnerId = 3, IsFinished = true }
            ]
        };

        category.EnsureThirdPlaceMatch();

        Assert.Null(category.ThirdPlaceMatch);
    }

    [Fact]
    public void KumiteCompetitionRunsBronzeBoutBeforeFinal()
    {
        var category = new ShobuSanbonCategory
        {
            CategoryType = CategoryType.Kumite
        };
        for (var id = 1; id <= 4; id++)
        {
            category.Participants.Add(new Participant { Id = id });
        }
        category.InitializeBracket();

        var manager = new CompetitionManagerViewModel(category, new NoOpDialogService());
        Assert.Equal(2, manager.CurrentMatchIndex);

        manager.UpdateMatchScore(isAka: true, points: 6);
        Assert.Equal(1, manager.CurrentMatchIndex);

        manager.UpdateMatchScore(isAka: true, points: 6);
        Assert.Equal(-1, manager.CurrentMatchIndex);
        Assert.Same(category.ThirdPlaceMatch, manager.CurrentMatch);
        Assert.NotNull(category.ThirdPlaceMatch);
        Assert.Equal(category.BracketMatches[1].Shiro, category.ThirdPlaceMatch!.Aka);
        Assert.Equal(category.BracketMatches[2].Aka, category.ThirdPlaceMatch.Shiro);

        manager.UpdateMatchScore(isAka: true, points: 6);
        Assert.Equal(0, manager.CurrentMatchIndex);
        Assert.Same(category.BracketMatches[0], manager.CurrentMatch);

        manager.UpdateMatchScore(isAka: true, points: 6);
        Assert.Null(manager.CurrentMatch);
        Assert.True(category.IsFinished);
        Assert.True(category.ThirdPlaceMatch.IsFinished);
        Assert.True(category.BracketMatches[0].IsFinished);
    }

    private sealed class NoOpDialogService : IDialogService
    {
        public DialogResult ShowMessage(string message, string title, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Information)
            => DialogResult.Yes;

        public string? ShowTextInput(string title, string prompt, string defaultValue = "") => null;
        public string? ShowOpenFileDialog(string title, string filter) => null;
        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName) => null;
    }
}
