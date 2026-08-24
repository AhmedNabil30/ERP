using System.Text;
using System.Text.Json.Serialization;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Middleware;
using Kaff.Api.Common.Results;
using Kaff.Api.Identity;
using Kaff.Api.Options;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
//  Configuration
// ---------------------------------------------------------------------------------------------

string connectionString = builder.Configuration.GetConnectionString(DependencyInjection.ConnectionStringName)
    ?? throw new InvalidOperationException(
        $"Connection string '{DependencyInjection.ConnectionStringName}' is not configured.");

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");

// Karim's lockout numbers (spec.md §9 amendment, decisions.md D-049 ruling 3). Bound and validated
// here so a deployment cannot carry an out-of-range value; the sign-in handler that reads them is
// slice 1's, exactly as JwtOptions.InactivityMinutes was bound before token issuance existed.
builder.Services
    .AddOptions<LockoutOptions>()
    .Bind(builder.Configuration.GetSection(LockoutOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------------------------
//  Identity and infrastructure
//
//  ICurrentUser is registered before AddKaffInfrastructure so the HTTP-backed implementation wins
//  over the system actor that infrastructure registers with TryAdd.
// ---------------------------------------------------------------------------------------------

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddKaffInfrastructure(connectionString);

// ---------------------------------------------------------------------------------------------
//  Authentication
//
//  Token issuance is slice 1. Slice 0 validates what slice 1 will issue, and fixes the claim names
//  in KaffClaimTypes so the two cannot drift.
// ---------------------------------------------------------------------------------------------

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
            NameClaimType = KaffClaimTypes.DisplayName,
            RoleClaimType = KaffClaimTypes.Role,
        };

        // The token arrives in an HttpOnly cookie, not in an Authorization header.
        //
        // Nabil and the Architect, 2026-08-21 (decisions.md D-050): localStorage is readable by any
        // injected script, and this system holds real ledgers. The cookie is HttpOnly, so JavaScript
        // — including injected JavaScript — cannot read it.
        //
        // The Authorization header is still honoured, and deliberately so: it is what service-to-
        // service callers and the integration suite use, and neither is reachable by an XSS payload
        // in the SPA. JwtBearer reads the header by default, so this event only needs to supply the
        // cookie when the header is absent.
        //
        // SameSite=Strict on the cookie is the CSRF control. Because the browser will not attach the
        // cookie to a cross-site request at all, there is no anti-forgery token here; if that ever
        // relaxes to Lax or None, one is required the same day.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Cookies.TryGetValue(jwt.CookieName, out string? cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
        };
    });

// ---------------------------------------------------------------------------------------------
//  Authorization
//
//  spec.md §9: enforcement is server-side, always. The fallback policy makes every endpoint require
//  an authenticated caller unless it opts out with AllowAnonymous, so a slice author who forgets to
//  declare a permission gets a locked endpoint rather than an open one.
// ---------------------------------------------------------------------------------------------

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---------------------------------------------------------------------------------------------
//  Presentation
// ---------------------------------------------------------------------------------------------

// Every refusal carries the messageKey the Angular application translates — including the ones
// this application never writes itself.
//
// A 401 or a 403 is produced by the authentication and authorization middleware, not by
// ResultExtensions.Problem, so the body was a bare ProblemDetails and the Arabic UI had nothing to
// render for a refusal (AC-106-B; qa/slice-1/verification-2026-08-23.md finding V-A). CLAUDE.md:
// "No hardcoded user-facing strings. Everything through i18n from the first commit" — which the API
// can only honour by sending a key.
//
// This is the one placement that covers every route rather than the route that happened to be
// reported: CustomizeProblemDetails runs inside IProblemDetailsService, which is the single writer
// behind UseStatusCodePages, UseExceptionHandler and Results.Problem alike. A per-endpoint fix
// would leave every sibling endpoint refusing in silence.
//
// TryAdd rather than assignment: a handler that already named its own key — a domain Forbidden such
// as AuthorizationErrors.SameActorCreatedAndApproved — keeps the more specific one.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    Error? refusal = context.ProblemDetails.Status switch
    {
        StatusCodes.Status401Unauthorized => AuthorizationErrors.NotAuthenticated,
        StatusCodes.Status403Forbidden => AuthorizationErrors.Forbidden,
        _ => null,
    };

    if (refusal is null)
    {
        return;
    }

    context.ProblemDetails.Extensions.TryAdd(ResultExtensions.CodeExtension, refusal.Code);
    context.ProblemDetails.Extensions.TryAdd(ResultExtensions.MessageKeyExtension, refusal.MessageKey);
});
builder.Services.AddOpenApi();

// Enums travel as their member names, not as numbers.
//
// KaffJson already says it is "the single JSON configuration used for audit before/after snapshots
// and for API payloads" — but nothing had wired it into the HTTP pipeline, because slice 0 shipped
// one endpoint whose payload carried no enum. The audit trail stores enums as text so a row is
// readable without today's code, and the UI keys server enums as enum.<Type>.<Member>
// (ux/rtl-and-i18n.md); a numeric wire form would be the one place the same value is a number.
//
// KaffJson.Options is frozen by MakeReadOnly, so the converter is added to the pipeline's own
// instance rather than the options object being shared.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddKaffEndpoints(typeof(Program).Assembly);

string[] allowedOrigins = builder.Configuration.GetSection("Kaff:AllowedOrigins").Get<string[]>() ?? [];

// AllowCredentials is required for the SPA's cookie to travel (decisions.md D-050), and it is the
// reason the origin list must be explicit: a browser rejects a wildcard origin outright when
// credentials are in play. An empty Kaff:AllowedOrigins therefore means no browser origin may call
// the API at all — which is the correct default, not an oversight.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(AuditCorrelationMiddleware.HeaderName)));

WebApplication app = builder.Build();

// ---------------------------------------------------------------------------------------------
//  Database
//
//  The guards are verified on every start-up, in every environment. A deployment that lost its
//  triggers would serve traffic normally and pass every application-level test, while the rule
//  spec.md §6.1 requires to live in the database would simply be absent. Outside Development that is
//  a reason to refuse to start.
// ---------------------------------------------------------------------------------------------

bool applyMigrations = app.Configuration.GetValue("Kaff:ApplyMigrationsOnStartup", app.Environment.IsDevelopment());

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

    if (applyMigrations)
    {
        await initializer.InitialiseAsync(SchemaStrategy.Migrate);
        await scope.ServiceProvider.GetRequiredService<AccountTreeSeeder>().SeedAsync();
    }
    else
    {
        await initializer.ApplyGuardsAsync();
    }

    IReadOnlyList<string> missingGuards = await initializer.FindMissingGuardsAsync();

    if (missingGuards.Count > 0 && !app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Refusing to start: database guards are missing — "
            + string.Join(", ", missingGuards)
            + ". The append-only and non-negative-balance rules are not enforced on this database.");
    }
}

// ---------------------------------------------------------------------------------------------
//  Pipeline
// ---------------------------------------------------------------------------------------------

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseMiddleware<AuditCorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapKaffEndpoints();

// Startup log: the permission rows spec.md leaves unresolved. Visible on every boot so an
// unanswered question stays a question instead of becoming a silent default.
foreach (PermissionDefinition definition in PermissionCatalogue.Unresolved)
{
    app.Logger.LogWarning(
        "Permission {Permission} is unresolved in spec.md ({Reference}). Grants: {GrantCount}.",
        definition.Permission,
        definition.SpecReference,
        definition.Grants.Count);
}

await app.RunAsync();

/// <summary>Exposed so Kaff.Api.Tests can drive the real host through WebApplicationFactory.</summary>
public partial class Program
{
    protected Program()
    {
    }
}
