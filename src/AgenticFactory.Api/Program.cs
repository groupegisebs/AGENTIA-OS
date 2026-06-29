using AgenticFactory.Infrastructure;
using AgenticFactory.Infrastructure.Persistence;
using AgenticFactory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAgenticInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgenticFactoryDbContext>();
    if (string.Equals(app.Configuration["Database:Provider"], "postgres", StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.MigrateAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeedService>();
    await seeder.SeedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RunStatusHub>("/hubs/runs");
app.MapGet("/health", async (AgenticFactoryDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return Results.Ok(new { status = canConnect ? "healthy" : "degraded", timestamp = DateTime.UtcNow });
});

app.Run();

public partial class Program;
