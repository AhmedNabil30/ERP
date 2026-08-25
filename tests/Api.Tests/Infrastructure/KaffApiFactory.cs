using System.Net;
using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real application against the test database.
/// </summary>
/// <remarks>
/// Only two things are substituted: the connection string, and authentication. Authorization,
/// the audit interceptor, the database guards and the endpoint conventions are the shipped ones.
///
/// The environment is "Testing" rather than "Development" on purpose — that is what makes
/// <c>Program</c> refuse to start if the database guards are missing, so a broken guard fails the
/// build here rather than in production.
/// </remarks>
public sealed class KaffApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// What every request through this factory's client carries as its connection address — see
    /// <see cref="FakeConnectionStartupFilter"/>.
    /// </summary>
    public static readonly IPAddress TestRemoteAddress = IPAddress.Parse("203.0.113.42");

    private readonly string _connectionString;

    public KaffApiFactory(string connectionString)
    {
        _connectionString = connectionString;

        // Set as environment variables, not through ConfigureAppConfiguration.
        //
        // Program.cs reads the connection string and the JWT settings immediately after
        // WebApplication.CreateBuilder(args), which happens before WebApplicationFactory's
        // deferred host configuration is applied — so a value supplied through
        // ConfigureAppConfiguration arrives too late and Program sees an empty string.
        // CreateBuilder does read the environment, so this lands in time.
        Environment.SetEnvironmentVariable("ConnectionStrings__KaffDatabase", connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "kaff-erp-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "kaff-erp-tests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "tests-only-signing-key-long-enough-for-hmac-sha256");
        Environment.SetEnvironmentVariable("Kaff__ApplyMigrationsOnStartup", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:KaffDatabase"] = _connectionString,
                ["Jwt:Issuer"] = "kaff-erp-tests",
                ["Jwt:Audience"] = "kaff-erp-tests",
                ["Jwt:SigningKey"] = "tests-only-signing-key-long-enough-for-hmac-sha256",
                ["Kaff:ApplyMigrationsOnStartup"] = "false",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEndpoint, ProbeEndpoint>();

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // TestServer never populates Connection.RemoteIpAddress — there is no real socket behind
            // it — so a test asserting on decisions.md D-063 §2 needs a stand-in for what a real
            // connection always carries. Registered as a startup filter, not a feature set in a test,
            // so it runs ahead of AuditCorrelationMiddleware exactly the way a real connection would.
            services.AddSingleton<IStartupFilter, FakeConnectionStartupFilter>();
        });
    }

    /// <summary>See the comment where this is registered above.</summary>
    private sealed class FakeConnectionStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= TestRemoteAddress;
                return nextMiddleware();
            });

            next(app);
        };
    }
}
