using System.ComponentModel.DataAnnotations;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class ParticipantEndpoints
{
    public static IEndpointRouteBuilder MapParticipantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/participants", CreateParticipant)
            .WithName("CreateParticipant");
        endpoints.MapGet("/api/participants/{participantId:int}", GetParticipant)
            .WithName("GetParticipant");
        return endpoints;
    }

    private static async Task<IResult> CreateParticipant(
        CreateParticipantRequest request,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            var errors = validationResults
                .SelectMany(result => result.MemberNames.Select(member => new
                {
                    Member = member,
                    Message = result.ErrorMessage ?? "Недопустимое значение."
                }))
                .GroupBy(error => error.Member)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
            return Results.ValidationProblem(errors);
        }

        var participant = new Participant
        {
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Age = request.Age!.Value,
            Club = string.IsNullOrWhiteSpace(request.Club) ? null : request.Club.Trim(),
            Belt = request.Belt!.Value,
            Sex = request.Sex!.Value,
            CategoryTypes = request.CategoryTypes!
                .Select(type => new ParticipantCategoryType { CategoryType = type })
                .ToList()
        };

        dbContext.Participants.Add(participant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/participants/{participant.Id}", ToDto(participant));
    }

    private static async Task<IResult> GetParticipant(
        int participantId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .Include(participant => participant.CategoryTypes)
            .SingleOrDefaultAsync(participant => participant.Id == participantId, cancellationToken);

        return participant is null ? Results.NotFound() : Results.Ok(ToDto(participant));
    }

    private static ParticipantDto ToDto(Participant participant) => new(
        participant.Id,
        participant.FirstName,
        participant.LastName,
        participant.Age,
        participant.Club,
        participant.Belt,
        participant.Sex,
        participant.CategoryTypes.OrderBy(link => link.CategoryType).Select(link => link.CategoryType).ToArray());
}
