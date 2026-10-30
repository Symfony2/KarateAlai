using System.IO;
using KarateTournamentApp.Models;

namespace KarateTournamentApp.Services
{
    internal static class TournamentIdentityNormalizer
    {
        private readonly record struct ParticipantIdentity(
            int OriginalId,
            string FirstName,
            string LastName,
            int Age,
            string? Club,
            Belts Belt,
            Sex Sex,
            string Categories);

        private sealed record ParticipantSource(Participant Participant, ParticipantIdentity Identity);

        public static void Normalize(IReadOnlyCollection<Category> categories)
        {
            ArgumentNullException.ThrowIfNull(categories);

            foreach (var category in categories)
            {
                if (category is null
                    || category.Participants is null
                    || category.BracketMatches is null
                    || category.JudgingScores is null
                    || category.FinalResults is null)
                {
                    throw new InvalidDataException("Данные турнира содержат неполную категорию.");
                }

                if (category is TeamCategory teamCategory && teamCategory.Teams is null)
                {
                    throw new InvalidDataException($"В категории «{category.Name}» отсутствует список команд.");
                }

                if (category.Participants.Any(participant => participant is null)
                    || category.BracketMatches.Any(match => match is null)
                    || category.FinalResults.Any(result => result is null || result.Participant is null))
                {
                    throw new InvalidDataException($"Категория «{category.Name}» содержит неполные результаты или поединки.");
                }
            }

            var teams = categories
                .OfType<TeamCategory>()
                .SelectMany(category => category.Teams)
                .ToList();
            if (teams.Any(team => team is null || team.Members is null || team.Members.Any(member => member is null)))
            {
                throw new InvalidDataException("Данные турнира содержат неполную команду или участника команды.");
            }

            var participantObjects = categories
                .SelectMany(category => category.Participants
                    .Concat(category.FinalResults.Select(result => result.Participant)))
                .Concat(teams.SelectMany(team => team.Members))
                .Distinct()
                .ToList();

            var participantSources = participantObjects
                .Select(participant => new ParticipantSource(participant, CreateIdentity(participant)))
                .ToList();
            var identityByParticipant = participantSources.ToDictionary(
                source => source.Participant,
                source => source.Identity);

            var highestExistingId = categories
                .Select(category => category.Id)
                .Concat(teams.Select(team => team.Id))
                .Concat(participantObjects.Select(participant => participant.Id))
                .DefaultIfEmpty()
                .Max();
            LocalIdGenerator.Reserve(highestExistingId);

            var identityCountByOriginalId = participantSources
                .Select(source => source.Identity)
                .Distinct()
                .GroupBy(identity => identity.OriginalId)
                .ToDictionary(group => group.Key, group => group.Count());
            var canonicalByIdentity = new Dictionary<ParticipantIdentity, Participant>();

            foreach (var source in participantSources)
            {
                if (!canonicalByIdentity.TryGetValue(source.Identity, out var canonicalParticipant))
                {
                    canonicalParticipant = source.Participant;
                    canonicalParticipant.Id = source.Identity.OriginalId > 0
                        && identityCountByOriginalId[source.Identity.OriginalId] == 1
                            ? source.Identity.OriginalId
                            : LocalIdGenerator.Next();
                    canonicalByIdentity.Add(source.Identity, canonicalParticipant);
                }

                source.Participant.Id = canonicalParticipant.Id;
            }

            foreach (var category in categories)
            {
                var categoryParticipants = category.Participants
                    .Select(participant => identityByParticipant[participant])
                    .Distinct()
                    .ToList();
                var participantIdsByOriginalId = categoryParticipants
                    .GroupBy(identity => identity.OriginalId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                var normalizedParticipants = categoryParticipants
                    .Select(identity => canonicalByIdentity[identity])
                    .DistinctBy(participant => participant.Id)
                    .ToList();
                category.Participants.Clear();
                foreach (var participant in normalizedParticipants)
                {
                    category.Participants.Add(participant);
                }

                for (var index = 0; index < category.BracketMatches.Count; index++)
                {
                    var match = category.BracketMatches[index];
                    match.Aka = ResolveParticipantId(match.Aka, category, participantIdsByOriginalId, canonicalByIdentity);
                    match.Shiro = ResolveParticipantId(match.Shiro, category, participantIdsByOriginalId, canonicalByIdentity);
                    match.WinnerId = ResolveParticipantId(match.WinnerId, category, participantIdsByOriginalId, canonicalByIdentity);
                }

                if (category.ThirdPlaceMatch is { } thirdPlaceMatch)
                {
                    thirdPlaceMatch.Aka = ResolveParticipantId(thirdPlaceMatch.Aka, category, participantIdsByOriginalId, canonicalByIdentity);
                    thirdPlaceMatch.Shiro = ResolveParticipantId(thirdPlaceMatch.Shiro, category, participantIdsByOriginalId, canonicalByIdentity);
                    thirdPlaceMatch.WinnerId = ResolveParticipantId(thirdPlaceMatch.WinnerId, category, participantIdsByOriginalId, canonicalByIdentity);
                }

                for (var index = 0; index < category.JudgingScores.Count; index++)
                {
                    var (scores, participantId) = category.JudgingScores[index];
                    var normalizedId = ResolveParticipantId(
                        participantId,
                        category,
                        participantIdsByOriginalId,
                        canonicalByIdentity);
                    category.JudgingScores[index] = (scores, normalizedId!.Value);
                }

                foreach (var result in category.FinalResults)
                {
                    var identity = identityByParticipant[result.Participant];
                    result.Participant = canonicalByIdentity[identity];
                }
            }

            foreach (var team in teams)
            {
                var normalizedMembers = team.Members
                    .Select(member => canonicalByIdentity[identityByParticipant[member]])
                    .DistinctBy(member => member.Id)
                    .ToList();
                team.Members.Clear();
                team.Members.AddRange(normalizedMembers);
            }

            EnsureUniqueIds(categories.Distinct(), category => category.Id, (category, id) => category.Id = id);
            EnsureUniqueIds(teams.Distinct(), team => team.Id, (team, id) => team.Id = id);
        }

        private static ParticipantIdentity CreateIdentity(Participant participant)
        {
            if (participant.FirstName is null || participant.LastName is null || participant.Categories is null)
            {
                throw new InvalidDataException("В данных турнира у спортсмена неполностью указаны идентификационные сведения.");
            }

            return new ParticipantIdentity(
                participant.Id,
                participant.FirstName,
                participant.LastName,
                participant.Age,
                participant.Club,
                participant.Belt,
                participant.Sex,
                string.Join(",", participant.Categories.OrderBy(categoryType => categoryType)));
        }

        private static int? ResolveParticipantId(
            int? originalId,
            Category category,
            IReadOnlyDictionary<int, List<ParticipantIdentity>> participantIdsByOriginalId,
            IReadOnlyDictionary<ParticipantIdentity, Participant> canonicalByIdentity)
        {
            if (!originalId.HasValue)
            {
                return null;
            }

            if (!participantIdsByOriginalId.TryGetValue(originalId.Value, out var identities))
            {
                throw new InvalidDataException(
                    $"Категория «{category.Name}» ссылается на отсутствующего спортсмена с ID {originalId.Value}.");
            }

            if (identities.Count != 1)
            {
                throw new InvalidDataException(
                    $"В категории «{category.Name}» неоднозначный старый ID спортсмена {originalId.Value}; сетку или оценки невозможно безопасно восстановить.");
            }

            return canonicalByIdentity[identities[0]].Id;
        }

        private static void EnsureUniqueIds<T>(
            IEnumerable<T> entities,
            Func<T, int> getId,
            Action<T, int> setId)
        {
            var entityList = entities.ToList();
            var idCounts = entityList
                .GroupBy(getId)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach (var entity in entityList)
            {
                var id = getId(entity);
                if (id > 0 && idCounts[id] == 1)
                {
                    continue;
                }

                setId(entity, LocalIdGenerator.Next());
            }
        }
    }
}
