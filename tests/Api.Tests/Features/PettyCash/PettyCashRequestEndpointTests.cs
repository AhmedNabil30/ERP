using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.PettyCash;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.PettyCash;

/// <summary>
/// KAFF-609b over HTTP. A junior drafts a عهدة request (the drafting screen is KAFF-609a, held on R2-Q6, so the tests seed
/// the draft); the supervising engineer submits or returns it. Role, level and assignment are all checked server side.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PettyCashRequestEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _finance;
    private Guid _junior;
    private Guid _supervisor;
    private Guid _supervisorUnassigned;

    public PettyCashRequestEndpointTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Client client = Client.Create(UniqueNames.Code("PC-C"), "عميل عهدة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = Make("pc-owner", Role.Owner);
        User finance = Make("pc-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User junior = Make("pc-jun", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("pc-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User unassigned = Make("pc-sup-un", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        context.Clients.Add(client);
        context.Users.AddRange(owner, finance, junior, supervisor, unassigned);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _junior = junior.Id;
        _supervisor = supervisor.Id;
        _supervisorUnassigned = unassigned.Id;

        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public void Both_routes_require_draft_submit_and_the_route_project()
    {
        foreach (string route in new[]
        {
            "/api/projects/{projectId:guid}/petty-cash-requests/{id:guid}/submission",
            "/api/projects/{projectId:guid}/petty-cash-requests/{id:guid}/return",
        })
        {
            var found = new List<PermissionRequirement>();
            foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
            {
                foreach (RouteEndpoint endpoint in source.Endpoints.OfType<RouteEndpoint>().Where(e => e.RoutePattern.RawText == route))
                {
                    foreach (IAuthorizeData data in endpoint.Metadata.OfType<IAuthorizeData>())
                    {
                        if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement) && requirement is not null)
                        {
                            found.Add(requirement);
                        }
                    }
                }
            }

            PermissionRequirement gate = found.Should().ContainSingle(route).Subject;
            gate.Permission.Should().Be(Permission.DraftSubmit);
            gate.Scope.Should().Be(ProjectScope.FromRoute());
        }
    }

    [Fact]
    public async Task The_supervisor_submits_a_draft_and_it_is_audited_and_posts_nothing()
    {
        (Guid project, Guid request) = await NewDraftAsync();
        long postings = await PostingCountAsync(project);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Submit(project, request), _supervisor);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(request)).Should().Be(PettyCashRequestStatus.Submitted);
        (await PostingCountAsync(project)).Should().Be(postings);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<AuditRecord> records = await reader.AuditRecords.Where(audit => audit.EntityId == request).ToListAsync(Ct);
        AuditRecord record = records.Single(audit => audit.ChangedProperties.Contains("Status"));
        record.ActorUserId.Should().Be(_supervisor);
        record.BeforeJson.Should().NotBeNull();
    }

    [Fact]
    public async Task A_junior_a_finance_user_the_owner_and_an_unassigned_engineer_are_refused_and_nothing_changes()
    {
        (Guid project, Guid request) = await NewDraftAsync();

        foreach (Guid refused in new[] { _junior, _finance, _owner, _supervisorUnassigned })
        {
            (await SendAsync(HttpMethod.Post, Submit(project, request), refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, Return(project, request), refused, new { reason = "x" })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        (await StatusOfAsync(request)).Should().Be(PettyCashRequestStatus.Draft);
    }

    [Fact]
    public async Task A_supervisor_cannot_submit_or_return_a_draft_he_drafted_himself()
    {
        (Guid project, Guid request) = await NewDraftAsync(drafter: _supervisor);

        HttpResponseMessage submit = await SendAsync(HttpMethod.Post, Submit(project, request), _supervisor);
        HttpResponseMessage back = await SendAsync(HttpMethod.Post, Return(project, request), _supervisor, new { reason = "x" });

        submit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        back.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(submit)).Should().Be(AuthorizationErrors.SameActorCreatedAndApproved.Code);
        (await StatusOfAsync(request)).Should().Be(PettyCashRequestStatus.Draft);
    }

    [Fact]
    public async Task A_return_stores_the_reason_and_a_blank_reason_is_refused()
    {
        (Guid project, Guid request) = await NewDraftAsync();

        HttpResponseMessage blank = await SendAsync(HttpMethod.Post, Return(project, request), _supervisor, new { reason = " " });
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(blank)).Should().Be(PettyCashErrors.ReasonRequired.Code);
        (await StatusOfAsync(request)).Should().Be(PettyCashRequestStatus.Draft);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Return(project, request), _supervisor, new { reason = "المبلغ ناقص" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using KaffDbContext reader = _database.CreateBareContext();
        PettyCashRequest stored = await reader.PettyCashRequests.SingleAsync(found => found.Id == request, Ct);
        stored.Status.Should().Be(PettyCashRequestStatus.Returned);
        stored.ReturnReason.Should().Be("المبلغ ناقص");
        stored.ReturnedByUserId.Should().Be(_supervisor);
        (await reader.AuditRecords.Where(audit => audit.EntityId == request).ToListAsync(Ct))
            .Any(audit => audit.ChangedProperties.Contains("ReturnReason"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_decided_request_cannot_be_decided_again_and_a_request_of_another_project_is_not_found()
    {
        (Guid project, Guid request) = await NewDraftAsync();
        (await SendAsync(HttpMethod.Post, Submit(project, request), _supervisor)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage again = await SendAsync(HttpMethod.Post, Submit(project, request), _supervisor);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(again)).Should().Be(PettyCashErrors.NotDraft.Code);

        (Guid other, _) = await NewDraftAsync();
        HttpResponseMessage crossed = await SendAsync(HttpMethod.Post, Submit(other, request), _supervisor);
        crossed.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private static string Submit(Guid project, Guid request) => $"/api/projects/{project}/petty-cash-requests/{request}/submission";

    private static string Return(Guid project, Guid request) => $"/api/projects/{project}/petty-cash-requests/{request}/return";

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
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

    private async Task<long> PostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<PettyCashRequestStatus> StatusOfAsync(Guid request)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.PettyCashRequests.Where(found => found.Id == request).Select(found => found.Status).SingleAsync(Ct);
    }

    /// <summary>An Active project with the junior and the supervisor assigned (supervisor level), and one draft by <paramref name="drafter"/>.</summary>
    private async Task<(Guid Project, Guid Request)> NewDraftAsync(Guid? drafter = null)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("PC-P"), UniqueNames.Code("مشروع عهدة"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2025, 1, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User junior = await context.Users.SingleAsync(user => user.Id == _junior, Ct);
        User supervisor = await context.Users.SingleAsync(user => user.Id == _supervisor, Ct);
        context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, junior, AssignmentLevel.Junior, _owner, Now).Value);
        context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, supervisor, AssignmentLevel.Supervisor, _owner, Now).Value);

        PettyCashRequest request = PettyCashRequest.Draft(project.Id, drafter ?? _junior, new Money(5_000m), "مواد", Now).Value;
        context.PettyCashRequests.Add(request);
        await context.SaveChangesAsync(Ct);
        return (project.Id, request.Id);
    }
}
