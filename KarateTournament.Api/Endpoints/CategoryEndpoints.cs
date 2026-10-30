using System.Data;
using System.ComponentModel.DataAnnotations;
using KarateTournament.Api.Contracts;
using KarateTournament.Api.Data;
using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/categories", CreateCategory).WithName("CreateCategory");
        endpoints.MapGet("/api/categories", GetCategories).WithName("GetCategories");
        endpoints.MapGet("/api/categories/{categoryId:int}", GetCategory).WithName("GetCategory");
        return endpoints;
    }

    private static async Task<IResult> GetCategories(
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => new CategorySummaryDto(
                category.Id, category.Name, category.CategoryType, category.Sex,
                category.MinAge, category.MaxAge, category.IsFinished,
                category.ParticipantLinks.Count, category.Teams.Count))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetCategory(
        int categoryId,
        TournamentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // WithDetails runs several queries; all must see the same database snapshot.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var category = await dbContext.Categories.WithDetails()
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        var dto = category is null ? null : CategoryReadModel.ToDto(category);
        await transaction.CommitAsync(cancellationToken);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> CreateCategory(
        CreateCategoryRequest request,
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

        var participantIds = request.ParticipantIds!;
        var existingIds = await dbContext.Participants
            .Where(participant => participantIds.Contains(participant.Id))
            .Select(participant => participant.Id)
            .ToArrayAsync(cancellationToken);
        var missingIds = participantIds.Except(existingIds).ToArray();
        if (missingIds.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ParticipantIds)] = [$"Неизвестные идентификаторы спортсменов: {string.Join(", ", missingIds)}."]
            });

        var category = new Category
        {
            Name = request.Name!.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            Sex = request.Sex!.Value,
            CategoryType = request.CategoryType!.Value,
            AllowedBeltLinks = request.AllowedBelts!
                .Select((belt, index) => new CategoryAllowedBelt { Belt = belt, SortOrder = index })
                .ToList(),
            ParticipantLinks = participantIds
                .Select((id, index) => new CategoryParticipant { ParticipantId = id, SortOrder = index })
                .ToList()
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.CreatedAtRoute("GetCategory", new { categoryId = category.Id },
            CategoryReadModel.ToDto(category));
    }
}
