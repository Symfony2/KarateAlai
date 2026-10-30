using KarateTournamentApp.Models;
using KarateTournamentApp.Services;
using KarateClassLibrary;
using Xunit;

namespace KarateTournamentApp.Tests;

public class GroupingRangeTests
{
    [Fact]
    public void ParsesDefaultAgeRangesWithoutGapsForRequestedTeenGroups()
    {
        var ranges = GroupingRangeParser.ParseAgeRanges(GroupingRangeParser.DefaultAgeRanges);
        Assert.Contains(ranges, range => range.Contains(5));
        Assert.Contains(ranges, range => range.Contains(6));
        Assert.Contains(ranges, range => range.Contains(13));
        Assert.Contains(ranges, range => range.Contains(19));
        Assert.Contains(ranges, range => range.Contains(45));
    }

    [Fact]
    public void ParsesWeightBandsAndRoundsDecimalWeighInUpToWholeKg()
    {
        var ranges = GroupingRangeParser.ParseWeightRanges("до 25; 25-30; 31-35; 36-40; 41+");
        Assert.Contains(ranges, range => range.Contains(25m));
        Assert.Contains(ranges, range => range.Contains(25.1m));
        Assert.Contains(ranges, range => range.Contains(30m) && range.MaxKg == 30m);
        Assert.Contains(ranges, range => range.Contains(30.1m) && range.MinKg == 31m);
    }


    [Fact]
    public void AssignsSharedWeightBoundaryToTheLighterClass()
    {
        var ranges = GroupingRangeParser.ParseWeightRanges("до 25; 25-30; 31-35");
        Assert.True(ranges[0].Contains(25m));
        Assert.False(ranges[1].Contains(25m));
        Assert.True(ranges[1].Contains(25.1m));
    }

    [Fact]
    public void RejectsOverlappingAgeBands()
    {
        Assert.Throws<FormatException>(() => GroupingRangeParser.ParseAgeRanges("до 8; 8-10; 11+"));
    }


    [Fact]
    public void AcceptsNaturalLanguageAgeAndWeightRangeLabels()
    {
        var ages = GroupingRangeParser.ParseAgeRanges("до 5 лет; 6-8 лет; 9-10; 19 лет и выше");
        var weights = GroupingRangeParser.ParseWeightRanges("до 25 кг; 25-30 кг; 80 кг и выше");

        Assert.Contains(ages, range => range.Contains(19));
        Assert.Contains(weights, range => range.Contains(80m));
        Assert.Contains(weights, range => range.Contains(80.1m));
        Assert.Equal("25–30 кг", weights[1].Label);
    }

    [Fact]
    public void RejectsAnEmptyWeightBandCreatedByAnExclusiveSharedBoundary()
    {
        Assert.Throws<FormatException>(() => GroupingRangeParser.ParseWeightRanges("до 25; 25-25; 26+"));
    }

    [Fact]
    public void RebuildsCategoriesByAgeAndWeightAndKeepsSingleParticipantCategories()
    {
        var participants = new[]
        {
            new Participant("A", "One", 16, Belts.Kyu5, Sex.Male, [CategoryType.Kumite]) { WeightKg = 49m },
            new Participant("B", "Two", 16, Belts.Kyu4, Sex.Male, [CategoryType.Kumite]) { WeightKg = 55m },
            new Participant("C", "Three", 17, Belts.Kyu5, Sex.Male, [CategoryType.Kumite]) { WeightKg = 55m }
        };
        var manager = new CategoryManager();
        manager.RebuildCategories(participants, true, false, true,
            GroupingRangeParser.ParseAgeRanges("16; 17+"),
            GroupingRangeParser.ParseWeightRanges("до 50; 51-60; 61+"));

        Assert.Equal(3, manager.DefinedCategories.Count);
        Assert.All(manager.DefinedCategories, category => Assert.Single(category.Participants));
        Assert.Contains(manager.DefinedCategories, category => category.WeightGroupLabel == "до 50 кг");
    }
}
