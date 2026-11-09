using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.Closure;

/// <summary>
/// The permission gate over every closure route: that no project route is mapped without a permission, and that each
/// closure route answers 403 to exactly the callers the permission catalogue does not grant, over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes this file replaces, both of which reported a safety they did not have. The first collected only routes
/// that carried a parsable permission policy, so a new <c>/api/projects/{id}/snags/...</c> route with no permission was
/// never collected and the "no other route exists" assertion passed over it. The second walked a hand-written list of
/// route segments, so a route under a new first segment was never scanned (the subcontractor retention release was
/// already outside it). Here the universe is every route under <c>/api/projects/{...}</c>, and a route with no
/// permission is a failure rather than an omission. The closure set is the routes whose handler lives in the closure
/// feature namespace, not a list of segment names.
/// </para>
/// <para>
/// The 403 matrix is derived: the expected answer for each caller on each route comes from the permission catalogue
/// through <see cref="PermissionEvaluator"/>, over a caller set that covers every staff role, both assignment levels
/// and the unassigned holder of each role that has any grant. A route added to the closure namespace is in the matrix
/// without anyone editing this file.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ClosureRouteGateTests : IAsyncLifetime
{
    private const string ClosureNamespace = "Kaff.Api.Features.Closure";
    private const string ProjectRoutePrefix = "/api/projects/{";

    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    private static readonly Regex GuidPlaceholder = new(@"\{(?<name>\w+):guid\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// The permission each closure route must demand. This is the intent being pinned: the permission is the contract,
    /// so it is written down. The ROUTE SET is not hand-listed anywhere else: it is found by namespace and compared to
    /// these keys in both directions.
    /// </summary>
    private static readonly Dictionary<string, Permission> ExpectedClosureRoutes = new(StringComparer.Ordinal)
    {
        ["POST /api/projects/{projectId:guid}/practical-completion"] = Permission.ProjectPracticalCompletionDeclare,
        ["POST /api/projects/{projectId:guid}/handover"] = Permission.ProjectHandover,
        ["POST /api/projects/{projectId:guid}/warranty"] = Permission.ProjectHandover,
        ["POST /api/projects/{projectId:guid}/hold-release"] = Permission.HoldRelease,
        ["POST /api/projects/{projectId:guid}/close"] = Permission.ProjectClose,
        ["POST /api/projects/{projectId:guid}/termination"] = Permission.ProjectManage,
        ["POST /api/projects/{projectId:guid}/snags"] = Permission.SnagManage,
        ["POST /api/projects/{projectId:guid}/snags/{id:guid}/fix"] = Permission.SnagManage,
        ["POST /api/projects/{projectId:guid}/snags/{id:guid}/verification"] = Permission.SnagManage,
        ["POST /api/projects/{projectId:guid}/snags/{id:guid}/resolution"] = Permission.SnagManage,
        ["GET /api/projects/{projectId:guid}/snags"] = Permission.ProjectRead,
        ["POST /api/projects/{projectId:guid}/warranty-callbacks"] = Permission.WarrantyCallbackManage,
        ["POST /api/projects/{projectId:guid}/warranty-callbacks/{id:guid}/resolution"] = Permission.WarrantyCallbackManage,
        ["GET /api/projects/{projectId:guid}/warranty-callbacks"] = Permission.ProjectRead,
        ["POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release"] = Permission.SubcontractorRetentionRelease,
    };

    /// <summary>
    /// Permissions that exist only for the closure acts. A route that demands one of these is a closure route whatever
    /// namespace it was written in, so moving a handler out of the closure folder cannot hide it from the check.
    /// </summary>
    private static readonly Permission[] ClosureOnlyPermissions =
    [
        Permission.ProjectPracticalCompletionDeclare,
        Permission.ProjectHandover,
        Permission.SnagManage,
        Permission.HoldRelease,
        Permission.WarrantyCallbackManage,
        Permission.ProjectClose,
        Permission.SubcontractorRetentionRelease,
    ];

    /// <summary>
    /// Project routes that may be mapped without a permission. Empty, deliberately: every route under a project is
    /// gated by role and assignment (spec.md section 9). An entry here needs a reason in prose beside it.
    /// </summary>
    private static readonly HashSet<string> UngatedProjectRouteAllowList = new(StringComparer.Ordinal);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private List<Caller> _callers = [];

    public ClosureRouteGateTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- the route set: every project route is gated, the closure set is exactly what is pinned ----------

    [Fact]
    public void Every_route_under_a_project_demands_a_permission_scoped_to_a_project()
    {
        List<string> ungated = UngatedProjectRoutes(ProjectRoutes());

        ungated.Should().BeEmpty(
            "every route under /api/projects/ with a project id must carry RequirePermission with a project scope from the route "
            + "(role AND assignment, spec.md section 9). A route with no permission was invisible to the check this "
            + "replaced. If one genuinely needs none, name it in UngatedProjectRouteAllowList with the reason");
    }

    [Fact]
    public void The_route_check_sees_a_project_route_with_no_permission_or_the_wrong_scope()
    {
        // The positive control: the check above must be able to fail. Synthetic routes are fed to the same function.
        RouteEndpoint ungated = SyntheticRoute("/api/projects/{projectId:guid}/snags/{id:guid}/probe", policy: null);
        RouteEndpoint companyScoped = SyntheticRoute(
            "/api/projects/{projectId:guid}/probe-company",
            PermissionRequirement.ToPolicyName(Permission.SnagManage, ProjectScope.None));
        RouteEndpoint gated = SyntheticRoute(
            "/api/projects/{projectId:guid}/probe-gated",
            PermissionRequirement.ToPolicyName(Permission.SnagManage, ProjectScope.FromRoute()));
        RouteEndpoint notAProjectRoute = SyntheticRoute("/api/clients", policy: null);

        List<string> found = UngatedProjectRoutes([ungated, companyScoped, gated, notAProjectRoute]);

        found.Should().BeEquivalentTo(
        [
            "POST /api/projects/{projectId:guid}/snags/{id:guid}/probe",
            "POST /api/projects/{projectId:guid}/probe-company",
        ]);
    }

    [Fact]
    public void The_closure_routes_are_exactly_the_pinned_set_each_with_its_permission_and_the_route_project()
    {
        Dictionary<string, PermissionRequirement?> actual = [];
        foreach (RouteEndpoint route in ProjectRoutes().Where(IsInClosureNamespace))
        {
            foreach (string method in MethodsOf(route))
            {
                actual[$"{method} {route.RoutePattern.RawText}"] = RequirementOf(route);
            }
        }

        actual.Keys.Should().BeEquivalentTo(ExpectedClosureRoutes.Keys, "every closure route is pinned, and no other exists");
        foreach ((string key, Permission permission) in ExpectedClosureRoutes)
        {
            actual[key].Should().NotBeNull($"{key} carries a permission");
            actual[key]!.Permission.Should().Be(permission, key);
            actual[key]!.Scope.Should().Be(ProjectScope.FromRoute(), $"{key} checks the assignment to the route's project");
        }
    }

    [Fact]
    public void A_route_demanding_a_closure_permission_is_a_closure_route_wherever_it_was_written()
    {
        List<string> outside = [];
        foreach (RouteEndpoint route in ProjectRoutes().Where(candidate => !IsInClosureNamespace(candidate)))
        {
            PermissionRequirement? requirement = RequirementOf(route);
            if (requirement is not null && ClosureOnlyPermissions.Contains(requirement.Permission))
            {
                outside.Add($"{string.Join('|', MethodsOf(route))} {route.RoutePattern.RawText} demands {requirement.Permission}");
            }
        }

        outside.Should().BeEmpty("a closure permission outside the closure feature escapes the pinned set above");
    }

    // ---- the 403 matrix, derived from the catalogue ------------------------------------------------------

    [Fact]
    public async Task Each_closure_route_is_refused_to_every_caller_the_catalogue_does_not_grant_and_to_every_unassigned_caller()
    {
        List<RouteEndpoint> routes = [.. ProjectRoutes().Where(IsInClosureNamespace)];
        routes.Should().NotBeEmpty("the matrix is vacuous over no routes");

        List<string> wrong = [];
        int granted = 0;
        int refused = 0;

        foreach (RouteEndpoint route in routes)
        {
            RequirementOfClosureRoute(route, out PermissionRequirement requirement);
            PermissionDefinition definition = PermissionCatalogue.Of(requirement.Permission);
            Guid project = await NewProjectAsync();

            foreach (string method in MethodsOf(route))
            {
                foreach (Caller caller in _callers)
                {
                    bool expectGranted = PermissionEvaluator.Evaluate(
                        caller.Subject, definition, project, caller.Access()) == PermissionDecision.Granted;

                    string url = GuidPlaceholder.Replace(
                        route.RoutePattern.RawText!,
                        match => match.Groups["name"].Value == "projectId" ? project.ToString() : Guid.NewGuid().ToString());

                    HttpResponseMessage response = await SendAsync(new HttpMethod(method), url, caller.Id);
                    bool gotRefused = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;

                    if (expectGranted)
                    {
                        granted++;
                    }
                    else
                    {
                        refused++;
                    }

                    if (expectGranted == gotRefused)
                    {
                        wrong.Add($"{method} {route.RoutePattern.RawText} as {caller.Name}: catalogue says "
                            + $"{(expectGranted ? "granted" : "refused")}, answered {(int)response.StatusCode}");
                    }
                }
            }
        }

        wrong.Should().BeEmpty("the answer to each caller on each route is the catalogue's, server-side");
        granted.Should().BeGreaterThan(0, "a matrix in which nobody is granted proves nothing about being refused");
        refused.Should().BeGreaterThan(granted, "most callers hold none of these permissions");
    }

    [Fact]
    public async Task The_unassigned_holder_of_a_role_is_refused_while_the_assigned_holder_of_the_same_role_is_not()
    {
        Guid project = await NewProjectAsync();

        // Same role, same department; only the assignment differs, on a route each of them holds the permission for.
        Caller assigned = _callers.Single(caller => caller.Name == "finance");
        Caller unassigned = _callers.Single(caller => caller.Name == "finance-unassigned");
        string route = $"/api/projects/{project}/hold-release";

        (await SendAsync(HttpMethod.Post, route, unassigned.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, route, assigned.Id)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        Caller assignedOffice = _callers.Single(caller => caller.Name == "technical-office");
        Caller unassignedOffice = _callers.Single(caller => caller.Name == "technical-office-unassigned");
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", unassignedOffice.Id)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/handover", unassignedOffice.Id)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/handover", assignedOffice.Id)).StatusCode
            .Should().NotBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <c>ProjectClose</c> is held by the Owner alone, and the Owner reaches every project without an assignment
    /// (spec.md section 9, ruling 1: reach, not capability). So the assignment half of that gate cannot be shown by an
    /// unassigned Owner being refused; it is shown by the Owner being refused a project that does not exist, which is
    /// the one thing the Owner's global reach is bounded by.
    /// </summary>
    [Fact]
    public async Task The_owner_reaches_a_project_without_an_assignment_but_not_one_that_does_not_exist()
    {
        Guid project = await NewProjectAsync();
        Guid missing = Guid.NewGuid();

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner)).StatusCode
            .Should().NotBe(HttpStatusCode.Forbidden, "the Owner is globally scoped: no assignment row is needed");
        (await SendAsync(HttpMethod.Post, $"/api/projects/{missing}/close", _owner)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "global reach is bounded by the project existing");
        (await SendAsync(HttpMethod.Get, $"/api/projects/{missing}/snags", _owner)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- route enumeration --------------------------------------------------------------------------------

    private IEnumerable<RouteEndpoint> ProjectRoutes() =>
        _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints.OfType<RouteEndpoint>())
            .Where(route => route.RoutePattern.RawText?.StartsWith(ProjectRoutePrefix, StringComparison.Ordinal) == true);

    private static bool IsInClosureNamespace(RouteEndpoint route) =>
        route.Metadata.GetMetadata<System.Reflection.MethodInfo>()?.DeclaringType?.Namespace
            ?.StartsWith(ClosureNamespace, StringComparison.Ordinal) == true;

    private static IEnumerable<string> MethodsOf(RouteEndpoint route) =>
        route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"];

    /// <summary>
    /// The route's permission requirement. A route may carry more than one (every policy must pass); the project-scoped
    /// one is returned when there is one, since that is the one this file checks (KAFF-415's conversion route carries a
    /// company-wide ProjectCreate and a project-scoped ProjectManage).
    /// </summary>
    private static PermissionRequirement? RequirementOf(RouteEndpoint route)
    {
        PermissionRequirement? first = null;
        foreach (IAuthorizeData data in route.Metadata.OfType<IAuthorizeData>())
        {
            if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement)
                && requirement is not null)
            {
                if (requirement.Scope.Source != ProjectScopeSource.None)
                {
                    return requirement;
                }

                first ??= requirement;
            }
        }

        return first;
    }

    private static void RequirementOfClosureRoute(RouteEndpoint route, out PermissionRequirement requirement) =>
        requirement = RequirementOf(route)
            ?? throw new InvalidOperationException($"{route.RoutePattern.RawText} carries no permission.");

    /// <summary>Every route that has no permission, or a permission that does not take its project from the route.</summary>
    private static List<string> UngatedProjectRoutes(IEnumerable<RouteEndpoint> candidates)
    {
        List<string> ungated = [];
        foreach (RouteEndpoint route in candidates)
        {
            string pattern = route.RoutePattern.RawText ?? string.Empty;
            if (!pattern.StartsWith(ProjectRoutePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            PermissionRequirement? requirement = RequirementOf(route);
            foreach (string method in MethodsOf(route))
            {
                string key = $"{method} {pattern}";
                if ((requirement is null || requirement.Scope.Source == ProjectScopeSource.None)
                    && !UngatedProjectRouteAllowList.Contains(key))
                {
                    ungated.Add(key);
                }
            }
        }

        return ungated;
    }

    private static RouteEndpoint SyntheticRoute(string pattern, string? policy)
    {
        List<object> metadata = [new HttpMethodMetadata(["POST"])];
        if (policy is not null)
        {
            metadata.Add(new AuthorizeAttribute(policy));
        }

        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(metadata),
            displayName: pattern);
    }

    // ---- callers ------------------------------------------------------------------------------------------

    private sealed record Caller(
        Guid Id, string Name, PermissionSubject Subject, bool Assigned, AssignmentLevel Level, Guid? ClientOfProject)
    {
        public ProjectAccess Access() => Subject.Role switch
        {
            Role.Owner => new ProjectAccess(ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor),
            Role.Hr => new ProjectAccess(ProjectAccessPath.HrGlobal, AssignmentLevel.Standard),
            Role.Client => new ProjectAccess(ProjectAccessPath.PortalClient, AssignmentLevel.Standard),
            _ => Assigned ? new ProjectAccess(ProjectAccessPath.Assignment, Level) : ProjectAccess.Denied,
        };
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(new { });
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>An Active Lump Sum project with every assigned caller assigned at their own level.</summary>
    private async Task<Guid> NewProjectAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("GT-P"), "مشروع بوابة", _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2026, 9, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        project.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 9, 1)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        foreach (Caller caller in _callers.Where(candidate => candidate.Assigned && candidate.Subject.Role is not (Role.Owner or Role.Hr or Role.Client)))
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == caller.Id, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, caller.Level, _owner, Now).Value);
        }

        await context.SaveChangesAsync(Ct);
        return project.Id;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(UniqueNames.Code("GT-C"), "عميل البوابة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        context.Clients.Add(client);
        await context.SaveChangesAsync(Ct);
        _clientId = client.Id;

        (string Name, Role Role, Guid? Department, OperationsSubDepartment? Sub, bool Assigned, AssignmentLevel Level)[] plan =
        [
            ("owner", Role.Owner, null, null, false, AssignmentLevel.Standard),
            ("finance", Role.Finance, WellKnownDepartments.FinanceId, null, true, AssignmentLevel.Standard),
            ("finance-unassigned", Role.Finance, WellKnownDepartments.FinanceId, null, false, AssignmentLevel.Standard),
            ("technical-office", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, true, AssignmentLevel.Standard),
            ("technical-office-unassigned", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, false, AssignmentLevel.Standard),
            ("site-supervisor", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, true, AssignmentLevel.Supervisor),
            ("site-junior", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, true, AssignmentLevel.Junior),
            ("site-unassigned", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, false, AssignmentLevel.Standard),
            ("hr", Role.Hr, WellKnownDepartments.HrId, null, false, AssignmentLevel.Standard),
            ("marketing", Role.MarketingSales, null, null, true, AssignmentLevel.Standard),
            ("head-of-design", Role.HeadOfDesign, null, null, true, AssignmentLevel.Standard),
        ];

        List<Caller> callers = [];
        foreach ((string name, Role role, Guid? department, OperationsSubDepartment? sub, bool assigned, AssignmentLevel level) in plan)
        {
            User user = User.Create(UniqueNames.Code("gt-" + name), name, UniqueNames.Phone(), role, Now, department, sub).Value;
            context.Users.Add(user);
            callers.Add(new Caller(
                user.Id, name, new PermissionSubject(user.Id, role, department, sub, ClientId: null, name), assigned, level, null));
        }

        User portal = User.Create(UniqueNames.Code("gt-client"), "client", UniqueNames.Phone(), Role.Client, Now, clientId: client.Id).Value;
        context.Users.Add(portal);
        callers.Add(new Caller(
            portal.Id, "client-portal", new PermissionSubject(portal.Id, Role.Client, null, null, client.Id, "client"), false, AssignmentLevel.Standard, client.Id));

        await context.SaveChangesAsync(Ct);

        _callers = callers;
        _owner = callers.Single(caller => caller.Name == "owner").Id;
    }
}
