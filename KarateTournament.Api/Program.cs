using KarateTournament.Api.Data;
using KarateTournament.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("TournamentDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Настройте ConnectionStrings:TournamentDatabase через user secrets или переменную окружения ConnectionStrings__TournamentDatabase.");
}

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
    if (await dbContext.Database.EnsureCreatedAsync())
    {
        app.Logger.LogInformation("Создана схема базы данных турнира.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTournamentEndpoints();
app.MapParticipantEndpoints();
app.MapCategoryEndpoints();

app.UseHttpsRedirection();

app.MapGet("/health/database", async (
    TournamentDbContext dbContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!await dbContext.Database.CanConnectAsync(cancellationToken))
    {
        logger.LogWarning("Проверка подключения к PostgreSQL не пройдена.");
        return Results.Problem(
            title: "База данных недоступна",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "healthy" });
})
.WithName("GetDatabaseHealth");

app.Run();

public partial class Program { }
