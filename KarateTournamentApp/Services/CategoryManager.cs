using KarateTournamentApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KarateTournamentApp.Services
{
    public class CategoryManager
    {
        public List<Category> DefinedCategories { get; set; } = new List<Category>();
        private List<Belts> AllBelts { get; } = Enum.GetValues<Belts>().ToList();

        /// <summary>
        /// Rebuilds the visible categories from the complete participant list using editable age/weight bands.
        /// Call this before creating brackets; rebuilding intentionally resets category-level competition state.
        /// </summary>
        public void RebuildCategories(
            IEnumerable<Participant> participants,
            bool divideByAge,
            bool divideByBelt,
            bool divideByWeight,
            IReadOnlyList<AgeGroupRange> ageRanges,
            IReadOnlyList<WeightGroupRange> weightRanges)
        {
            var allBelts = Enum.GetValues<Belts>().ToList();
            var grouped = new Dictionary<(CategoryType Type, Sex Sex, int? MinAge, int? MaxAge, string? AgeLabel,
                Belts? Belt, decimal? MinWeight, decimal? MaxWeight, string? WeightLabel), Category>();

            foreach (var participant in participants)
            {
                foreach (var categoryType in participant.Categories.Distinct())
                {
                    AgeGroupRange? ageGroup = divideByAge
                        ? ageRanges.FirstOrDefault(range => range.Contains(participant.Age))
                        : null;
                    var ageLabel = divideByAge ? ageGroup?.Label ?? "Вне возрастной сетки" : null;
                    int? minAge = divideByAge ? ageGroup?.MinAge : null;
                    int? maxAge = divideByAge ? ageGroup?.MaxAge : null;

                    Belts? beltKey = divideByBelt ? participant.Belt : null;
                    var belts = divideByBelt ? new List<Belts> { participant.Belt } : allBelts;

                    WeightGroupRange? weightGroup = null;
                    string? weightLabel = null;
                    decimal? minWeight = null;
                    decimal? maxWeight = null;
                    if (divideByWeight && categoryType == CategoryType.Kumite)
                    {
                        if (!participant.WeightKg.HasValue)
                        {
                            weightLabel = "Вес не указан";
                        }
                        else
                        {
                            weightGroup = weightRanges.FirstOrDefault(range => range.Contains(participant.WeightKg.Value));
                            weightLabel = weightGroup?.Label ?? "Вне весовой сетки";
                            minWeight = weightGroup?.MinKg;
                            maxWeight = weightGroup?.MaxKg;
                        }
                    }

                    var key = (categoryType, participant.Sex, minAge, maxAge, ageLabel,
                        beltKey, minWeight, maxWeight, weightLabel);
                    if (!grouped.TryGetValue(key, out var category))
                    {
                        category = CreateGroupedCategory(
                            belts, categoryType, participant.Sex, minAge, maxAge,
                            minWeight, maxWeight, ageLabel, weightLabel);
                        grouped.Add(key, category);
                    }

                    if (!category.Participants.Any(existing => existing.Id == participant.Id))
                    {
                        category.Participants.Add(participant);
                    }
                }
            }

            DefinedCategories.Clear();
            DefinedCategories.AddRange(grouped.Values
                .OrderBy(category => category.CategoryType)
                .ThenBy(category => category.Sex)
                .ThenBy(category => category.MinAge ?? -1)
                .ThenBy(category => category.MinWeightKg ?? -1)
                .ThenBy(category => category.Name));
        }

        private static Category CreateGroupedCategory(
            List<Belts> belts,
            CategoryType type,
            Sex sex,
            int? minAge,
            int? maxAge,
            decimal? minWeight,
            decimal? maxWeight,
            string? ageLabel,
            string? weightLabel)
        {
            return type == CategoryType.Kumite
                ? new ShobuSanbonCategory(belts, type, sex, minAge, maxAge, minWeight, maxWeight, ageLabel, weightLabel)
                : new Category(belts, type, sex, minAge, maxAge, minWeight, maxWeight, ageLabel, weightLabel);
        }


        /// <summary>
        /// Merges several compatible, not-yet-started categories into the first selected category.
        /// Competition data is deliberately rejected because match references cannot be safely combined.
        /// </summary>
        public Category MergeCategories(IEnumerable<Category> categories)
        {
            var selected = categories?.DistinctBy(category => category.Id).ToList()
                ?? throw new ArgumentNullException(nameof(categories));

            if (selected.Count < 2)
                throw new ArgumentException("Для объединения выберите минимум две категории.", nameof(categories));

            if (selected.Any(category => !DefinedCategories.Contains(category)))
                throw new InvalidOperationException("Одна или несколько выбранных категорий больше не существуют.");

            var target = selected[0];
            if (selected.Any(category => category.CategoryType != target.CategoryType
                || category.GetType() != target.GetType()))
                throw new InvalidOperationException("Можно объединять только категории одной дисциплины и одного типа.");

            if (selected.Any(category => category.BracketMatches.Count > 0
                || category.ThirdPlaceMatch != null
                || category.IsFinished
                || category.FinalResults.Count > 0
                || category.JudgingScores.Count > 0))
                throw new InvalidOperationException("Нельзя объединять категории после создания жеребьёвки или начала соревнований.");

            foreach (var source in selected.Skip(1))
            {
                if (target is TeamCategory targetTeam && source is TeamCategory sourceTeam)
                    targetTeam.MergeWith(sourceTeam);
                else
                    target.MergeWith(source);

                DefinedCategories.Remove(source);
            }

            return target;
        }

        public void AssignParticipant(Participant p, bool DivideByBelt, bool DivideByAge) 
        {
            if (p.Age >= 18){ AssignSenior(p); return; }

            if (DivideByBelt && DivideByAge)
            {
                AssignByBoth(p);
                return;
            }

            if (DivideByAge)
            {
                AssignByAge(p);
                return;
            }

            if (DivideByBelt)
            {
                AssignByBelt(p);
            }
        }
        public void AssignTeam(Team t)
        {
            var targetCategory = (TeamCategory?)DefinedCategories.FirstOrDefault(c =>
                    c.MinAge >= 18 &&
                    (t.Sex == c.Sex || c.Sex == Sex.Unisex) &&
                    CategoryType.Team == c.CategoryType);
            if(targetCategory != null)
            {
                targetCategory.Teams.Add(t);
            }
            else
            {
                var cat = new TeamCategory(AllBelts, CategoryType.Team, t.Sex, t.Age, t.Age);
                cat.Teams.Add(t);
                DefinedCategories.Add(cat);
            }
        }
        public void AssignSenior(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => category.MinAge >= 18 && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(AllBelts, categoryType, p.Sex, 18, 99));
        }

        public void AssignByBelt(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => category.MaxAge < 18 && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(p.Belt, categoryType, p.Sex, 1, 17));
        }

        public void AssignByAge(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => p.Age >= category.MinAge && p.Age <= category.MaxAge,
                categoryType => CreateParticipantCategory(AllBelts, categoryType, p.Sex, p.Age, p.Age));
        }

        public void AssignByBoth(Participant p)
        {
            AssignParticipantToCategories(
                p,
                category => p.Age >= category.MinAge
                    && p.Age <= category.MaxAge
                    && category.AllowedBelts.Contains(p.Belt),
                categoryType => CreateParticipantCategory(p.Belt, categoryType, p.Sex, p.Age, p.Age));
        }

        private void AssignParticipantToCategories(
            Participant participant,
            Func<Category, bool> additionalMatch,
            Func<CategoryType, Category> createCategory)
        {
            foreach (var categoryType in participant.Categories)
            {
                var targetCategory = DefinedCategories.FirstOrDefault(category =>
                    category.CategoryType == categoryType
                    && (participant.Sex == category.Sex || category.Sex == Sex.Unisex)
                    && additionalMatch(category));

                var isNewCategory = targetCategory == null;
                targetCategory ??= createCategory(categoryType);
                targetCategory.Participants.Add(participant);

                if (isNewCategory)
                {
                    DefinedCategories.Add(targetCategory);
                }
            }
        }

        private static Category CreateParticipantCategory(
            List<Belts> belts,
            CategoryType categoryType,
            Sex sex,
            int minAge,
            int maxAge)
        {
            return categoryType == CategoryType.Kumite
                ? new ShobuSanbonCategory(belts, categoryType, sex, minAge, maxAge)
                : new Category(belts, categoryType, sex, minAge, maxAge);
        }

        private static Category CreateParticipantCategory(
            Belts belt,
            CategoryType categoryType,
            Sex sex,
            int minAge,
            int maxAge)
        {
            return categoryType == CategoryType.Kumite
                ? new ShobuSanbonCategory(belt, categoryType, sex, minAge, maxAge)
                : new Category(belt, categoryType, sex, minAge, maxAge);
        }
    }
}