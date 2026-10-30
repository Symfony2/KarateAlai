using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class BracketTests
{
    [Theory]
    [InlineData(false, 3, 3)]
    [InlineData(true, 3, 3)]
    [InlineData(false, 8, 7)]
    [InlineData(true, 8, 7)]
    public void GeneratedBracketHasCorrectSizeAndEachParticipantStartsExactlyOnce(
        bool shobu, int participantCount, int expectedMatches)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        for (var id = 1; id <= participantCount; id++)
            category.Participants.Add(new Participant { Id = id });

        category.InitializeBracket();

        Assert.Equal(expectedMatches, category.BracketMatches.Count);
        var firstRoundStart = expectedMatches / 2;
        var firstRound = category.BracketMatches.Skip(firstRoundStart).ToArray();
        var startingIds = firstRound.SelectMany(match => new[] { match.Aka, match.Shiro })
            .Where(id => id.HasValue).Select(id => id!.Value).OrderBy(id => id).ToArray();
        Assert.Equal(Enumerable.Range(1, participantCount), startingIds);
        Assert.False(category.IsFinished);
        if (shobu)
            Assert.All(category.BracketMatches, match => Assert.IsType<ShobuSanbonMatch>(match));

        if (participantCount == 3)
        {
            var bye = Assert.Single(firstRound, match => match.Aka.HasValue != match.Shiro.HasValue);
            Assert.True(bye.IsFinished);
            Assert.Equal(bye.Aka ?? bye.Shiro, bye.WinnerId);
            var byeIndex = category.BracketMatches.IndexOf(bye);
            var final = category.BracketMatches[0];
            Assert.Equal(bye.WinnerId, byeIndex % 2 != 0 ? final.Aka : final.Shiro);
            Assert.False(final.IsFinished);
        }
        else
        {
            Assert.All(category.BracketMatches, match =>
            {
                Assert.False(match.IsFinished);
                Assert.Null(match.WinnerId);
            });
            Assert.All(category.BracketMatches.Take(firstRoundStart), match =>
            {
                Assert.Null(match.Aka);
                Assert.Null(match.Shiro);
            });
        }
    }

    [Theory]
    [InlineData(42, null, 42, true)]
    [InlineData(null, 43, 43, true)]
    [InlineData(42, 43, null, false)]
    [InlineData(null, null, null, false)]
    public void MatchAwardsByeOnlyWhenExactlyOneParticipantExists(
        int? aka, int? shiro, int? winner, bool finished)
    {
        var match = new Match(aka, shiro);
        Assert.Equal(winner, match.WinnerId);
        Assert.Equal(finished, match.IsFinished);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void SemifinalWinnerAdvancesToCorrectSideOfFinal(int index, bool akaSide)
    {
        var category = new Category { BracketMatches = [new(), new(), new()] };
        category.BracketMatches[index].WinnerId = 42;
        category.PromoteWinner(index);
        Assert.Equal(42, akaSide ? category.BracketMatches[0].Aka : category.BracketMatches[0].Shiro);
        Assert.Null(akaSide ? category.BracketMatches[0].Shiro : category.BracketMatches[0].Aka);
        Assert.False(category.IsFinished);
    }

    [Fact]
    public void FinalWinnerFinishesCategory()
    {
        var category = new Category { BracketMatches = [new() { WinnerId = 42 }] };
        category.PromoteWinner(0);
        Assert.True(category.IsFinished);
        Assert.Equal(42, category.BracketMatches[0].WinnerId);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReinitializingBracketClearsPreviousMedalResults(bool shobu)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        var first = new Participant { Id = 42 };
        var second = new Participant { Id = 43 };
        category.Participants.Add(first);
        category.Participants.Add(second);
        category.FinalResults = [new ParticipantResult { Participant = first, Score = 1 }];

        category.InitializeBracket();

        Assert.Empty(category.FinalResults);
        Assert.Null(category.ThirdPlaceMatch);
        Assert.False(category.IsFinished);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoParticipantsInitializeOneUnfinishedMatch(bool shobu)
    {
        Category category = shobu ? new ShobuSanbonCategory() : new Category();
        category.Participants.Add(new Participant { Id = 42 });
        category.Participants.Add(new Participant { Id = 43 });
        category.InitializeBracket();
        var match = Assert.Single(category.BracketMatches);
        Assert.Equal(42, match.Aka);
        Assert.Equal(43, match.Shiro);
        Assert.False(match.IsFinished);
        Assert.Null(match.WinnerId);
        if (shobu) Assert.IsType<ShobuSanbonMatch>(match);
    }
}
