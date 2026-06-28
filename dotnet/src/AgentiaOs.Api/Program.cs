using System.Security.Claims;
using System.Text;
using AgentiaOs.Api.Security;
using AgentiaOs.Application.Abstractions;
using AgentiaOs.Application.Contracts.Auth;
using AgentiaOs.Application.Contracts.Conversations;
using AgentiaOs.Application.Contracts.Deploy;
using AgentiaOs.Domain.Entities;
using AgentiaOs.Infrastructure;
using AgentiaOs.Infrastructure.Configuration;
using AgentiaOs.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentiaDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AgentiaOs.Api",
    utc = DateTime.UtcNow
}));

var authGroup = app.MapGroup("/api/auth");
authGroup.MapPost("/register", async (
    RegisterRequest request,
    AgentiaDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) =>
{
    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var exists = await db.Users.AnyAsync(u => u.Email == normalizedEmail);
    if (exists)
    {
        return Results.Conflict(new { message = "Email already exists." });
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = normalizedEmail,
        DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "User" : request.DisplayName.Trim(),
        PasswordHash = passwordHasher.Hash(request.Password),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    var token = jwtTokenGenerator.CreateToken(user);
    return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName, token));
});

authGroup.MapPost("/login", async (
    LoginRequest request,
    AgentiaDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) =>
{
    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);
    if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = jwtTokenGenerator.CreateToken(user);
    return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName, token));
});

var conversationGroup = app.MapGroup("/api/conversations").RequireAuthorization();
conversationGroup.MapPost(string.Empty, async (
    CreateConversationRequest request,
    ClaimsPrincipal principal,
    AgentiaDbContext db) =>
{
    var userId = principal.GetUserId();
    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var now = DateTime.UtcNow;
    var conversation = new Conversation
    {
        Id = Guid.NewGuid(),
        OwnerUserId = userId,
        Title = string.IsNullOrWhiteSpace(request.Title) ? "Nouvelle conversation" : request.Title.Trim(),
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    db.Conversations.Add(conversation);
    await db.SaveChangesAsync();
    return Results.Ok(conversation);
});

conversationGroup.MapGet(string.Empty, async (ClaimsPrincipal principal, AgentiaDbContext db) =>
{
    var userId = principal.GetUserId();
    var conversations = await db.Conversations
        .Where(c => c.OwnerUserId == userId)
        .OrderByDescending(c => c.UpdatedAtUtc)
        .ToListAsync();

    return Results.Ok(conversations);
});

conversationGroup.MapGet("/{conversationId:guid}", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    AgentiaDbContext db) =>
{
    var userId = principal.GetUserId();
    var conversation = await db.Conversations
        .Include(c => c.Messages)
        .SingleOrDefaultAsync(c => c.Id == conversationId && c.OwnerUserId == userId);

    if (conversation is not null)
    {
        conversation.Messages = conversation.Messages.OrderBy(m => m.CreatedAtUtc).ToList();
    }

    return conversation is null ? Results.NotFound() : Results.Ok(conversation);
});

conversationGroup.MapPost("/{conversationId:guid}/messages", async (
    Guid conversationId,
    PostMessageRequest request,
    ClaimsPrincipal principal,
    AgentiaDbContext db) =>
{
    var userId = principal.GetUserId();
    var conversation = await db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId && c.OwnerUserId == userId);
    if (conversation is null)
    {
        return Results.NotFound();
    }

    var now = DateTime.UtcNow;
    var userMessage = new ConversationMessage
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = "user",
        Content = request.Content.Trim(),
        CreatedAtUtc = now
    };
    var assistantMessage = new ConversationMessage
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = "assistant",
        Content = $"Reponse mock: {request.Content.Trim()}",
        CreatedAtUtc = now.AddMilliseconds(1)
    };

    conversation.UpdatedAtUtc = now;
    db.ConversationMessages.AddRange(userMessage, assistantMessage);
    await db.SaveChangesAsync();

    return Results.Ok(new[] { userMessage, assistantMessage });
});

app.MapGet("/api/blueprint", () => Results.Ok(new
{
    name = "AgentiaOS Blueprint Phase 1",
    version = "1.0.0-phase1",
    capabilities = new[] { "auth", "conversations", "blueprint", "deploy" }
}));

var deployGroup = app.MapGroup("/api/deploy").RequireAuthorization();
deployGroup.MapPost("/", async (CreateDeploymentRequest request, AgentiaDbContext db) =>
{
    var now = DateTime.UtcNow;
    var job = new DeploymentJob
    {
        Id = Guid.NewGuid(),
        ConversationId = request.ConversationId,
        TargetEnvironment = string.IsNullOrWhiteSpace(request.TargetEnvironment) ? "dev" : request.TargetEnvironment.Trim(),
        Status = "pending",
        CreatedAtUtc = now
    };

    db.DeploymentJobs.Add(job);
    await db.SaveChangesAsync();
    return Results.Accepted($"/api/deploy/{job.Id}", job);
});

deployGroup.MapGet("/{deploymentId:guid}", async (Guid deploymentId, AgentiaDbContext db) =>
{
    var job = await db.DeploymentJobs.SingleOrDefaultAsync(d => d.Id == deploymentId);
    if (job is null)
    {
        return Results.NotFound();
    }

    if (job.Status == "pending" && DateTime.UtcNow - job.CreatedAtUtc > TimeSpan.FromSeconds(2))
    {
        job.Status = "succeeded";
        job.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return Results.Ok(job);
});

app.Run();

public partial class Program;
