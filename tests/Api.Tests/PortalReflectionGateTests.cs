using System.Reflection;
using Kaff.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-811, the gate (D-035 "still owed"; spec.md section 12: "The client MUST NEVER see costs, margins, catalogue, subcontractors,
/// internal notes, or any other client's data").
/// </summary>
/// <remarks>
/// The check walks the declared response type of every mapped <c>/api/portal/*</c> route and everything reachable from it. A route
/// whose response is a body must declare it with <c>Produces</c>, because a route the test cannot see is a route the gate does not
/// cover. Two rules then hold for every reachable type: it lives in the portal's own namespace (so a staff response type, a Domain
/// entity and a <c>Money</c> value can never be reached), and no property name carries a banned word.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class PortalReflectionGateTests : IAsyncLifetime
{
    private const string PortalNamespace = "Kaff.Api.Features.Portal.";

    // The proof types below stand in for portal-owned ones, so the walk treats this assembly's namespace as 'owned'.
    private const string TestNamespace = "Kaff.Api.Tests";

    /// <summary>
    /// Routes that answer with no JSON body, each with the reason. Everything else under /api/portal must declare a response type.
    /// </summary>
    private static readonly Dictionary<string, string> BodylessRoutes = new()
    {
        ["POST /api/portal/auth/sign-in"] = "204 and a Set-Cookie header; the body carries nothing.",
        ["POST /api/portal/auth/change-password"] = "204 and a Set-Cookie header; the body carries nothing.",
        ["GET /api/portal/projects/{projectId:guid}/photos/{photoId:guid}"] = "A file stream of one published photo; no JSON.",
    };

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;

    public PortalReflectionGateTests(PostgresDatabase database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new KaffApiFactory(_database);
        _ = _factory.Services;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>AC-811-A and AC-811-B over the real route table.</summary>
    [Fact]
    public void No_portal_response_reaches_a_staff_type_a_domain_entity_or_a_cost_shaped_field()
    {
        List<string> findings = [];
        int routes = 0;

        foreach (RouteEndpoint route in PortalRoutes())
        {
            routes++;
            string key = Key(route);
            Type[] declared =
            [
                .. route.Metadata.OfType<IProducesResponseTypeMetadata>()
                    .Where(meta => meta.StatusCode is >= 200 and < 300 && meta.Type is not null && meta.Type != typeof(void))
                    .Select(meta => meta.Type!),
            ];

            if (declared.Length == 0)
            {
                if (!BodylessRoutes.ContainsKey(key))
                {
                    findings.Add($"{key}: declares no response type and is not a named bodyless route");
                }

                continue;
            }

            foreach (Type type in declared)
            {
                findings.AddRange(PortalLeakCheck.Violations(type).Select(finding => $"{key}: {finding}"));
            }
        }

        routes.Should().BeGreaterThan(5, "the test must actually see the portal's routes");
        findings.Should().BeEmpty("a portal response may carry only portal-owned types and no cost-shaped field");
    }

    [Fact]
    public void Every_bodyless_route_named_here_is_mapped()
    {
        string[] mapped = [.. PortalRoutes().Select(Key)];

        BodylessRoutes.Keys.Should().BeSubsetOf(mapped, "a dead exemption is one nobody notices growing");
    }

    /// <summary>AC-811-C: a deliberately added cost field fails the check.</summary>
    [Fact]
    public void A_cost_price_added_to_a_portal_response_is_caught()
    {
        PortalLeakCheck.Violations(typeof(Kaff.Api.Features.Portal.ListChangeOrders.PortalChangeOrder)).Should().BeEmpty();
        PortalLeakCheck.Violations(typeof(LeakyBecauseOfCost), TestNamespace).Should().Contain(finding => finding.Contains("CostPrice", StringComparison.Ordinal));
        PortalLeakCheck.Violations(typeof(LeakyBecauseOfNesting), TestNamespace).Should().Contain(finding => finding.Contains("MarginPercent", StringComparison.Ordinal));
    }

    [Fact]
    public void A_staff_response_type_a_domain_entity_and_a_money_value_are_all_caught()
    {
        PortalLeakCheck.Violations(typeof(Kaff.Api.Features.Projects.GetProject.Response)).Should().NotBeEmpty();
        PortalLeakCheck.Violations(typeof(LeakyBecauseOfEntity), TestNamespace).Should().Contain(finding => finding.Contains("Kaff.Domain.Projects.Project", StringComparison.Ordinal));
        PortalLeakCheck.Violations(typeof(LeakyBecauseOfMoney), TestNamespace).Should().Contain(finding => finding.Contains("Money", StringComparison.Ordinal));
    }

    private static string Key(RouteEndpoint route) =>
        string.Join('|', route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"]) + " " + route.RoutePattern.RawText;

    private IEnumerable<RouteEndpoint> PortalRoutes() =>
        _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(route => route.RoutePattern.RawText?.StartsWith("/api/portal/", StringComparison.OrdinalIgnoreCase) == true);

    // ---- deliberately wrong types, to prove the check fails -------------------------------------------------------

    public sealed record LeakyBecauseOfCost(string Name, string CostPrice);

    public sealed record LeakyBecauseOfNesting(string Name, Inner Detail);

    public sealed record Inner(string MarginPercent);

    public sealed record LeakyBecauseOfEntity(Kaff.Domain.Projects.Project Project);

    public sealed record LeakyBecauseOfMoney(Kaff.Domain.Common.Money Total);

    /// <summary>The walk. Pure reflection, so the proof types above exercise exactly what the route table does.</summary>
    internal static class PortalLeakCheck
    {
        /// <summary>
        /// Names a portal property may never carry (stories.md KAFF-811, AC-811-B). Lower case, matched as a substring. An entry that
        /// must be let through goes in <see cref="Allowed"/> with a reason; there is none today.
        /// </summary>
        private static readonly string[] Banned =
        [
            "cost", "margin", "markup", "profit", "catalogue", "subcontractor", "supplier", "internal", "note", "audit", "ncr", "qc",
            "technicaloffice", "petty", "safe", "treasury",
        ];

        /// <summary>"Type.Property" to the reason it may carry a banned word. Empty on purpose.</summary>
        private static readonly Dictionary<string, string> Allowed = [];

        private static readonly HashSet<Type> Scalars =
        [
            typeof(string), typeof(bool), typeof(int), typeof(long), typeof(decimal), typeof(Guid), typeof(DateOnly), typeof(DateTimeOffset),
        ];

        public static IReadOnlyList<string> Violations(Type root, string ownedPrefix = PortalNamespace)
        {
            List<string> findings = [];
            Walk(root, [], findings, ownedPrefix);
            return findings;
        }

        private static void Walk(Type type, HashSet<Type> seen, List<string> findings, string ownedPrefix)
        {
            type = Unwrap(type);

            if (Scalars.Contains(type) || !seen.Add(type))
            {
                return;
            }

            if (type.Namespace is null || !type.Namespace.StartsWith(ownedPrefix, StringComparison.Ordinal))
            {
                findings.Add($"reaches {type.FullName}, which is not a portal-owned type");
                return;
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string name = property.Name;
                string key = type.Name + "." + name;

                if (!Allowed.ContainsKey(key)
                    && Banned.FirstOrDefault(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)) is { } hit)
                {
                    findings.Add($"{key} carries the banned word '{hit}'");
                }

                Walk(property.PropertyType, seen, findings, ownedPrefix);
            }
        }

        private static Type Unwrap(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } inner)
            {
                return Unwrap(inner);
            }

            if (type.IsArray)
            {
                return Unwrap(type.GetElementType()!);
            }

            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                return Unwrap(type.GetGenericArguments()[0]);
            }

            return type;
        }
    }
}
