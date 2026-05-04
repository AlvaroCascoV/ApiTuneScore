using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
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
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text.Json;
using ApiTuneScore.Models;

var builder = WebApplication.CreateBuilder(args);

// Optional local secrets file (gitignored). In Azure, use Key Vault + app settings instead.
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());

    // SecretClient for imperative GetSecret (same style as ApiOAuthEmpleadosACV); no BuildServiceProvider().
    builder.Services.AddAzureClients(clientBuilder =>
    {
        clientBuilder.AddSecretClient(new Uri(keyVaultUri)).WithCredential(new DefaultAzureCredential());
    });
}

// ── Controllers & OpenAPI ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

// Request timing / path logging (see Logging:LogLevel in appsettings; Azure Log stream / console).
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

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
// App Service runs Windows; never fall back to TuneScoreDBLocal there (often empty and masks Key Vault issues).
var onAzureAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
string? connectionString = builder.Configuration.GetConnectionString("TuneScoreDBAzure");
if (string.IsNullOrWhiteSpace(connectionString) && !onAzureAppService)
{
    connectionString = OperatingSystem.IsWindows()
        ? builder.Configuration.GetConnectionString("TuneScoreDBLocal")
        : builder.Configuration.GetConnectionString("TuneScoreDBMac");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No database connection string was found. On Azure set ConnectionStrings__TuneScoreDBAzure or Key Vault secret ConnectionStrings--TuneScoreDBAzure. " +
        "Local: TuneScoreDBLocal / TuneScoreDBMac, or disable KeyVault:VaultUri and use appsettings.Secrets.json if you lack vault access.");
}

connectionString = connectionString.Trim().TrimStart('\uFEFF');

builder.Services.AddDbContext<TuneScoreContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
    }));

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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    context.Fail("Invalid principal.");
                    return Task.CompletedTask;
                }

                var userDataClaim = identity.FindFirst("UserData");
                if (userDataClaim != null)
                {
                    if (!HelperCifrado.IsConfigured)
                    {
                        context.Fail("Server is missing ClavesCrypto:Key; cannot validate encrypted token.");
                        return Task.CompletedTask;
                    }

                    try
                    {
                        var json = HelperCifrado.DescifrarString(userDataClaim.Value);
                        var payload = JsonSerializer.Deserialize<TokenUserPayload>(json, JwtTokenPayloadSerializer.Options);
                        if (payload == null)
                        {
                            context.Fail("Invalid token payload.");
                            return Task.CompletedTask;
                        }

                        identity.RemoveClaim(userDataClaim);
                        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.UserId.ToString()));
                        identity.AddClaim(new Claim(ClaimTypes.Name, payload.Username));
                        identity.AddClaim(new Claim(ClaimTypes.Email, payload.Email));
                        if (payload.ArtistId.HasValue)
                            identity.AddClaim(new Claim("ArtistId", payload.ArtistId.Value.ToString()));
                    }
                    catch
                    {
                        context.Fail("Invalid token payload.");
                    }

                    return Task.CompletedTask;
                }

                // Legacy tokens (plain NameIdentifier / Name / Email) issued before encrypted UserData
                if (identity.FindFirst(ClaimTypes.NameIdentifier) != null)
                    return Task.CompletedTask;

                context.Fail("Missing user identity in token.");
                return Task.CompletedTask;
            }
        };
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

// Load crypto key after the configuration host is built so Key Vault / env overrides are visible (builder.Configuration can miss KV secrets early).
try
{
    if (app.Configuration is IConfigurationRoot configurationRoot)
        configurationRoot.Reload();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Configuration reload skipped; using already-loaded configuration.");
}

string? cryptoKeyFromVault = null;
var cryptoSecretName = app.Configuration["ClavesCrypto:KeyVaultSecretName"];
if (!string.IsNullOrWhiteSpace(cryptoSecretName))
{
    var secretClient = app.Services.GetService<SecretClient>();
    if (secretClient == null)
    {
        app.Logger.LogWarning(
            "ClavesCrypto:KeyVaultSecretName is set but SecretClient is unavailable; configure KeyVault:VaultUri. Falling back to configuration only.");
    }
    else
    {
        try
        {
            cryptoKeyFromVault = secretClient.GetSecret(cryptoSecretName.Trim()).Value.Value;
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to read JWT crypto key from Key Vault secret {SecretName}. Falling back to configuration.", cryptoSecretName);
        }
    }
}

HelperCifrado.Initialize(app.Configuration, cryptoKeyFromVault);
if (!HelperCifrado.IsConfigured)
{
    app.Logger.LogWarning(
        "ClavesCrypto:Key is empty after configuration load. Options: Key Vault secret ClavesCrypto--Key (configuration), " +
        "set ClavesCrypto:KeyVaultSecretName for imperative GetSecret (vault secret name as stored), App Service ClavesCrypto__Key, " +
        "or flat config ClavesCrypto-Key. JWT login will fail until configured.");
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
    app.MapOpenApi();
    app.MapScalarApiReference();


app.UseHttpsRedirection();
app.UseHttpLogging();
app.UseCors("TuneScoreCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Redirect root to Scalar docs in development
app.MapGet("/", () => Results.Redirect("/scalar"))
   .ExcludeFromDescription();

app.Run();
