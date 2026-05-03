using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using ApiTuneScore.Data;
using ApiTuneScore.HealthChecks;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services;
using ApiTuneScore.Services.Interfaces;
using ApiTuneScore.Helpers;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Optional local secrets file (gitignored). In Azure, use Key Vault + app settings instead.
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

// ── Controllers & OpenAPI ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("TuneScoreCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ── Database ──────────────────────────────────────────────────────────────────
string? connectionString =
    builder.Configuration.GetConnectionString("TuneScoreDBAzure")
    ?? (OperatingSystem.IsWindows()
        ? builder.Configuration.GetConnectionString("TuneScoreDBLocal")
        : builder.Configuration.GetConnectionString("TuneScoreDBMac"));

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No database connection string was found. Configure ConnectionStrings:TuneScoreDBAzure " +
        "or a local fallback (TuneScoreDBLocal/TuneScoreDBMac).");
}

builder.Services.AddDbContext<TuneScoreContext>(options =>
    options.UseSqlServer(connectionString));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("API is running."))
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// ── JWT Authentication ────────────────────────────────────────────────────────
var oauthHelper = new HelperActionOAuthService(builder.Configuration);

builder.Services.AddSingleton<HelperActionOAuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = oauthHelper.GetTokenValidationParameters();
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ArtistOnly", policy => policy.RequireRole("Artist", "Admin"));
});

// ── Email ─────────────────────────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<EmailService>();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRepositoryArtists, RepositoryArtists>();
builder.Services.AddScoped<IRepositoryAlbums, RepositoryAlbums>();
builder.Services.AddScoped<IRepositorySongs, RepositorySongs>();
builder.Services.AddScoped<IRepositoryPlaylists, RepositoryPlaylists>();
builder.Services.AddScoped<ISongCommentRepository, SongCommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserSaltRepository, UserSaltRepository>();
builder.Services.AddScoped<IArtistLinkRequestRepository, ArtistLinkRequestRepository>();
builder.Services.AddScoped<IRepositoryGenres, RepositoryGenres>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IArtistLinkRequestService, ArtistLinkRequestService>();
builder.Services.AddScoped<IContentVisibilityService, ContentVisibilityService>();
builder.Services.AddHttpClient<IGeocodingService, GeocodingService>();

builder.Services.AddHttpContextAccessor();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
    app.MapOpenApi();
    app.MapScalarApiReference();


app.UseHttpsRedirection();
app.UseCors("TuneScoreCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Redirect root to Scalar docs in development
app.MapGet("/", () => Results.Redirect("/scalar"))
   .ExcludeFromDescription();

app.Run();
