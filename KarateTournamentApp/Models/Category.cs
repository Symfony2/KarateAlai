using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Serialization;
using KarateTournamentApp.Localization;

namespace KarateTournamentApp.Models
{
    [JsonDerivedType(typeof(Category), typeDiscriminator: "base")]
    [JsonDerivedType(typeof(ShobuSanbonCategory), typeDiscriminator: "shobu")]
    [JsonDerivedType(typeof(TeamCategory), typeDiscriminator: "team")]
    public class Category
    {
        private int _id = LocalIdGenerator.Next();

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                LocalIdGenerator.Reserve(value);
            }
        }
        public string Name { get; set; } = "";
        public ObservableCollection<Participant> Participants { get; set; } = new ObservableCollection<Participant>();
        public List<Belts> AllowedBelts { get; set; } = new List<Belts> { };
        public List<Match> BracketMatches { get; set; } = new List<Match>();
        /// <summary>Optional bronze-medal bout between the two semifinal losers.</summary>
        public Match? ThirdPlaceMatch { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public decimal? MinWeightKg { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public string? AgeGroupLabel { get; set; }
        public string? WeightGroupLabel { get; set; }
        public bool IsFinished { get; set; } = false;
        public Sex Sex { get; set; }
        public CategoryType CategoryType { get; set; }
        public List<(List<decimal> Scores, int ParticipantId)> JudgingScores { get; set; } = new List<(List<decimal>, int)>();
        public List<ParticipantResult> FinalResults { get; set; } = new List<ParticipantResult>();

        public Category(Belts belt, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null, decimal? minWeightKg = null, decimal? maxWeightKg = null, string? ageGroupLabel = null, string? weightGroupLabel = null)
        {
            MinAge = minAge;
            MaxAge = maxAge;
            MinWeightKg = minWeightKg;
            MaxWeightKg = maxWeightKg;
            AgeGroupLabel = ageGroupLabel;
            WeightGroupLabel = weightGroupLabel;
            AllowedBelts.Add(belt);
            Sex = sex;
            CategoryType = type;
            GenerateName();
        }

        public Category(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null, decimal? minWeightKg = null, decimal? maxWeightKg = null, string? ageGroupLabel = null, string? weightGroupLabel = null)
        {
            MinAge = minAge;
            MaxAge = maxAge;
            MinWeightKg = minWeightKg;
            MaxWeightKg = maxWeightKg;
            AgeGroupLabel = ageGroupLabel;
            WeightGroupLabel = weightGroupLabel;
            AllowedBelts = belts ?? new List<Belts>();
            Sex = sex;
            CategoryType = type;
            GenerateName();
        }

        public Category()
        {
        }

        protected void GenerateName()
        {
            var parts = new List<string>
            {
                AgeGroupLabel ?? (MinAge.HasValue || MaxAge.HasValue ? RussianText.FormatAgeRange(MinAge, MaxAge) : "Все возраста")
            };

            // Do not flood the category title with every belt when belt splitting is disabled.
            if (AllowedBelts.Count == 1)
            {
                parts.Add(RussianText.Belt(AllowedBelts[0]));
            }

            if (!string.IsNullOrWhiteSpace(WeightGroupLabel))
            {
                parts.Add(WeightGroupLabel);
            }
            else if (MinWeightKg.HasValue || MaxWeightKg.HasValue)
            {
                parts.Add($"{MinWeightKg?.ToString("0.##") ?? "0"}–{MaxWeightKg?.ToString("0.##") ?? "∞"} кг");
            }

            Name = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
        /// <summary>
        /// Combines the passed category with the current one.
        /// </summary>
        public void MergeWith(Category otherCategory)
        {
            if (otherCategory == null) return;
            foreach (var participant in otherCategory.Participants)
            {
                if (!Participants.Any(p => p.Id == participant.Id))
                {
                    Participants.Add(participant);
                }
            }
            if (Sex != otherCategory.Sex) Sex = Sex.Unisex;
            // Null boundaries mean an open-ended range. The union remains open-ended
            // whenever either source range has an open boundary.
            MinAge = MinAge.HasValue && otherCategory.MinAge.HasValue
                ? Math.Min(MinAge.Value, otherCategory.MinAge.Value) : null;
            MaxAge = MaxAge.HasValue && otherCategory.MaxAge.HasValue
                ? Math.Max(MaxAge.Value, otherCategory.MaxAge.Value) : null;
            MinWeightKg = MinWeightKg.HasValue && otherCategory.MinWeightKg.HasValue
                ? Math.Min(MinWeightKg.Value, otherCategory.MinWeightKg.Value) : null;
            MaxWeightKg = MaxWeightKg.HasValue && otherCategory.MaxWeightKg.HasValue
                ? Math.Max(MaxWeightKg.Value, otherCategory.MaxWeightKg.Value) : null;
            if (WeightGroupLabel != otherCategory.WeightGroupLabel) WeightGroupLabel = null;
            if (AgeGroupLabel != otherCategory.AgeGroupLabel) AgeGroupLabel = null;

            foreach (var belt in otherCategory.AllowedBelts)
            {
                if (!AllowedBelts.Contains(belt))
                {
                    AllowedBelts.Add(belt);
                }
            }
            GenerateName();
        }

        public void PromoteWinner(int currentMatchIndex)
        {
            if (currentMatchIndex == 0) { IsFinished = true; return; }

            int parentIndex = (currentMatchIndex - 1) / 2;
            var winnerId = BracketMatches[currentMatchIndex].WinnerId;
            if (currentMatchIndex % 2 != 0)
                BracketMatches[parentIndex].Aka = winnerId;
            else
                BracketMatches[parentIndex].Shiro = winnerId;
        }

        /// <summary>Creates the bronze-medal bout once both semifinals have real winners and losers.</summary>
        public void EnsureThirdPlaceMatch()
        {
            if (ThirdPlaceMatch != null || BracketMatches.Count < 3)
            {
                return;
            }

            var firstSemifinal = BracketMatches[1];
            var secondSemifinal = BracketMatches[2];
            var firstLoser = GetLoser(firstSemifinal);
            var secondLoser = GetLoser(secondSemifinal);

            if (!firstLoser.HasValue || !secondLoser.HasValue || firstLoser == secondLoser)
            {
                return;
            }

            ThirdPlaceMatch = BracketMatches.Any(match => match is ShobuSanbonMatch)
                ? new ShobuSanbonMatch(firstLoser, secondLoser)
                : new Match(firstLoser, secondLoser);
        }

        private static int? GetLoser(Match match)
        {
            if (!match.IsFinished || !match.WinnerId.HasValue || !match.Aka.HasValue || !match.Shiro.HasValue)
            {
                return null;
            }

            if (match.WinnerId == match.Aka)
            {
                return match.Shiro;
            }

            if (match.WinnerId == match.Shiro)
            {
                return match.Aka;
            }

            return null;
        }

        /// <summary>
        /// Initializes a bracket, semi-randomly
        /// </summary>
        public virtual void InitializeBracket()
        {
            BracketMatches = new List<Match>();
            ThirdPlaceMatch = null;
            FinalResults = new List<ParticipantResult>();
            IsFinished = false;
            var size = Participants.Count;

            if (size <= 2)
            {
                var singleMatch = new Match();
                BracketMatches.Add(singleMatch);
                if (size >= 1)
                {
                    singleMatch.Aka = Participants[0].Id;
                }
                if (size >= 2)
                {
                    singleMatch.Shiro = Participants[1].Id;
                }

                return;
            }

            int participantSlots = 1;
            while (participantSlots < size) participantSlots *= 2;

            int totalMatches = participantSlots - 1;
            int firstRoundStart = participantSlots / 2 - 1;
            for (int j = 0; j < totalMatches; j++)
            {
                BracketMatches.Add(new Match());
            }

            var shuffledParticipants = GetShuffledParticipants();

            for (int i = 0; i < size; i++)
            {
                // Fill every first-round match before adding opponents to avoid empty branches.
                int firstRoundMatchCount = participantSlots / 2;
                int bracketIndex = firstRoundStart + (i % firstRoundMatchCount);
                if (i < firstRoundMatchCount)
                {
                    BracketMatches[bracketIndex].Aka = shuffledParticipants[i].Id;
                }
                else
                {
                    BracketMatches[bracketIndex].Shiro = shuffledParticipants[i].Id;
                }
            }

            for (int i = firstRoundStart; i < totalMatches; i++)
            {
                var match = BracketMatches[i];
                bool hasBye = match.Aka.HasValue ^ match.Shiro.HasValue;
                if (hasBye)
                {
                    match.WinnerId = match.Aka ?? match.Shiro;
                    match.IsFinished = true;
                    PromoteWinner(i);
                }
            }
        }

        protected Participant[] GetShuffledParticipants()
        {
            var shuffledParticipants = Participants.ToArray();
            Random.Shared.Shuffle(shuffledParticipants.AsSpan());
            return shuffledParticipants;
        }
    }

    public class ShobuSanbonCategory : Category
    {
        public ShobuSanbonCategory(Belts belt, CategoryType type,  Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null, decimal? minWeightKg = null, decimal? maxWeightKg = null, string? ageGroupLabel = null, string? weightGroupLabel = null) : base(belt, type, sex, minAge, maxAge, minWeightKg, maxWeightKg, ageGroupLabel, weightGroupLabel)
        {  
        }

        public ShobuSanbonCategory(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null, decimal? minWeightKg = null, decimal? maxWeightKg = null, string? ageGroupLabel = null, string? weightGroupLabel = null) : base(belts, type, sex, minAge, maxAge, minWeightKg, maxWeightKg, ageGroupLabel, weightGroupLabel)
        {
        }

        public ShobuSanbonCategory() : base()
        {
        }
        /// <summary>
        /// Initializes a bracket, semi-randomly
        /// </summary>
        public override void InitializeBracket()
        {
            BracketMatches = new List<Match>();
            ThirdPlaceMatch = null;
            FinalResults = new List<ParticipantResult>();
            IsFinished = false;
            var size = Participants.Count;

            if (size <= 2)
            {
                var singleMatch = new ShobuSanbonMatch();
                BracketMatches.Add(singleMatch);

                if (size >= 1)
                {
                    singleMatch.Aka = Participants[0].Id;
                }
                if (size >= 2)
                {
                    singleMatch.Shiro = Participants[1].Id;
                }

                return;
            }

            int participantSlots = 1;
            while (participantSlots < size) participantSlots *= 2;

            int totalMatches = participantSlots - 1;
            int firstRoundStart = participantSlots / 2 - 1;
            for (int j = 0; j < totalMatches; j++)
            {
                BracketMatches.Add(new ShobuSanbonMatch());
            }

            var shuffledParticipants = GetShuffledParticipants();

            for (int i = 0; i < size; i++)
            {
                // Fill every first-round match before adding opponents to avoid empty branches.
                int firstRoundMatchCount = participantSlots / 2;
                int bracketIndex = firstRoundStart + (i % firstRoundMatchCount);
                if (i < firstRoundMatchCount)
                {
                    BracketMatches[bracketIndex].Aka = shuffledParticipants[i].Id;
                }
                else
                {
                    BracketMatches[bracketIndex].Shiro = shuffledParticipants[i].Id;
                }
            }

            for (int i = firstRoundStart; i < totalMatches; i++)
            {
                var match = BracketMatches[i];
                bool hasBye = match.Aka.HasValue ^ match.Shiro.HasValue;
                if (hasBye)
                {
                    match.WinnerId = match.Aka ?? match.Shiro;
                    match.IsFinished = true;
                    PromoteWinner(i);
                }
            }
        }
    }

    public class TeamCategory : Category
    {
        public ObservableCollection<Team> Teams { get; set; } = new ObservableCollection<Team>();
        public TeamCategory(List<Belts> belts, CategoryType type, Sex sex = Sex.Unisex, int? minAge = null, int? maxAge = null) : base(belts, type, sex, minAge, maxAge)
        {
            GenerateName();
        }
        public TeamCategory() : base()
        {
        }
        private void GenerateName()
        {
            if (MinAge >= 18)
            {
                Name = "Командное ката — взрослые";
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"Командное ката, {RussianText.FormatAgeRange(MinAge, MaxAge)}");
            foreach (var belt in AllowedBelts)
            {
                sb.Append($", {RussianText.Belt(belt)}");
            }
            Name = sb.ToString();
        }
        /// <summary>
        /// Combines the passed category with the current one.
        /// </summary>
        public void MergeWith(TeamCategory otherCategory)
        {
            if (otherCategory == null) return;
            foreach (var team in otherCategory.Teams)
            {
                if (!Teams.Any(t => t.Id == team.Id))
                {
                    Teams.Add(team);
                }
            }
            if (Sex != otherCategory.Sex) Sex = Sex.Unisex;
            MinAge = MinAge.HasValue && otherCategory.MinAge.HasValue
                ? Math.Min(MinAge.Value, otherCategory.MinAge.Value) : null;
            MaxAge = MaxAge.HasValue && otherCategory.MaxAge.HasValue
                ? Math.Max(MaxAge.Value, otherCategory.MaxAge.Value) : null;

            foreach (var belt in otherCategory.AllowedBelts)
            {
                if (!AllowedBelts.Contains(belt))
                {
                    AllowedBelts.Add(belt);
                }
            }
            if (MinAge >= 18)
            {
                Name = "Командное ката — взрослые";
            }
            else
            {
                GenerateName();
            }
        }
    }
}
