using KarateClassLibrary;
using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class CategoryMergeTests
{
    [Fact]
    public void MergeCombinesParticipantsAndEligibilityWithoutDuplicatesOrChangingSource()
    {
        var first = new Participant { Id = 41 };
        var second = new Participant { Id = 42 };
        var target = new Category(Belts.Kyu8, CategoryType.Kata, Sex.Male, 10, 12)
        {
            Participants = [first, second]
        };
        var duplicateSecond = new Participant { Id = 42 };
        var third = new Participant { Id = 43 };
        var source = new Category([Belts.Kyu8, Belts.Kyu7], CategoryType.Kata, Sex.Female, 9, 14)
        {
            Participants = [duplicateSecond, third]
        };

        target.MergeWith(source);

        Assert.Equal(new[] { first, second, third }, target.Participants);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, target.AllowedBelts);
        Assert.Equal(9, target.MinAge);
        Assert.Equal(14, target.MaxAge);
        Assert.Equal(Sex.Unisex, target.Sex);
        Assert.Equal(new[] { duplicateSecond, third }, source.Participants);
        Assert.Equal(9, source.MinAge);
        Assert.Equal(14, source.MaxAge);
        Assert.Equal(Sex.Female, source.Sex);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, source.AllowedBelts);
    }

    [Fact]
    public void TeamMergeDeduplicatesByIdAndPreservesExistingTeamAndSource()
    {
        var existing = new Team("Existing", Sex.Male) { Id = 41 };
        var duplicate = new Team("Imported copy", Sex.Male) { Id = 41 };
        var added = new Team("New", Sex.Male) { Id = 42 };
        var target = new TeamCategory([Belts.Kyu8], CategoryType.Team, Sex.Male, 12, 14)
        {
            Teams = [existing]
        };
        var source = new TeamCategory([Belts.Kyu8, Belts.Kyu7], CategoryType.Team, Sex.Male, 10, 13)
        {
            Teams = [duplicate, added]
        };

        target.MergeWith(source);

        Assert.Equal(new[] { existing, added }, target.Teams);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, target.AllowedBelts);
        Assert.Equal(10, target.MinAge);
        Assert.Equal(14, target.MaxAge);
        Assert.Equal(Sex.Male, target.Sex);
        Assert.Equal(new[] { duplicate, added }, source.Teams);
        Assert.Equal(10, source.MinAge);
        Assert.Equal(13, source.MaxAge);
        Assert.Equal(Sex.Male, source.Sex);
        Assert.Equal(new[] { Belts.Kyu8, Belts.Kyu7 }, source.AllowedBelts);
    }

    [Fact]
    public void MergeCategoriesCombinesMultipleCategoriesAndRemovesSources()
    {
        var firstParticipant = new Participant { Id = 1001 };
        var secondParticipant = new Participant { Id = 1002 };
        var thirdParticipant = new Participant { Id = 1003 };
        var first = new Category([Belts.Kyu8], CategoryType.Kata, Sex.Male, 8, 10)
        { Participants = [firstParticipant] };
        var second = new Category([Belts.Kyu7], CategoryType.Kata, Sex.Female, 11, 12)
        { Participants = [secondParticipant] };
        var third = new Category([Belts.Kyu6], CategoryType.Kata, Sex.Male, 13, 15)
        { Participants = [thirdParticipant] };
        var manager = new KarateTournamentApp.Services.CategoryManager
        { DefinedCategories = [first, second, third] };

        var result = manager.MergeCategories([first, second, third]);

        Assert.Same(first, result);
        Assert.Equal(3, result.Participants.Count);
        Assert.Equal(Sex.Unisex, result.Sex);
        Assert.Equal(8, result.MinAge);
        Assert.Equal(15, result.MaxAge);
        Assert.Single(manager.DefinedCategories);
        Assert.Contains(result, manager.DefinedCategories);
    }

    [Fact]
    public void MergeCategoriesRejectsCategoriesWithExistingBracket()
    {
        var first = new ShobuSanbonCategory([Belts.Kyu8], CategoryType.Kumite, Sex.Male, 10, 12);
        first.Participants.Add(new Participant { Id = 1101 });
        first.Participants.Add(new Participant { Id = 1102 });
        first.InitializeBracket();
        var second = new ShobuSanbonCategory([Belts.Kyu8], CategoryType.Kumite, Sex.Male, 10, 12);
        var manager = new KarateTournamentApp.Services.CategoryManager
        { DefinedCategories = [first, second] };

        Assert.Throws<InvalidOperationException>(() => manager.MergeCategories([first, second]));
        Assert.Equal(2, manager.DefinedCategories.Count);
    }


    [Fact]
    public void MergeKeepsOpenEndedAgeAndWeightBoundaries()
    {
        var target = new Category([Belts.Kyu8], CategoryType.Kumite, Sex.Male,
            null, 5, null, 25, "до 5 лет", "до 25 кг");
        var source = new Category([Belts.Kyu8], CategoryType.Kumite, Sex.Male,
            6, 8, 25, 30, "6–8 лет", "25–30 кг");

        target.MergeWith(source);

        Assert.Null(target.MinAge);
        Assert.Equal(8, target.MaxAge);
        Assert.Null(target.MinWeightKg);
        Assert.Equal(30m, target.MaxWeightKg);
        Assert.Null(target.AgeGroupLabel);
        Assert.Null(target.WeightGroupLabel);
        Assert.Contains("до 8", target.Name);
    }

}
