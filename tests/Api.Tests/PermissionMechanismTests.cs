using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// The permission gate, exercised through real routes.
/// </summary>
/// <remarks>
/// spec.md §9: "Enforcement is server-side; hiding UI elements is presentation, not security."
/// These tests hit endpoints directly, which is how CLAUDE.md says permission tests must be written.
///
/// The full per-role matrix belongs to the Verifier in a fresh session. What is proved here is that
/// the mechanism itself works end to end: the policy is generated, the project is resolved from the
/// route, the assignment is read from the database, and the decision is enforced.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class PermissionMechanismTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectId;
    private Guid _otherProjectId;
    private Guid _clientId;
    private Guid _ownerAssigned;
    private Guid _ownerUnassigned;
    private Guid _financeAssigned;
    private Guid _financeUnassigned;
    private Guid _juniorEngineer;
    private Guid _supervisingEngineer;
    private Guid _marketing;
    private Guid _portalClient;
    private Guid _hr;

    public PermissionMechanismTests(PostgresDatabase database) => _database = database;

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

    [Fact]
    public async Task An_anonymous_caller_reaches_only_an_anonymous_endpoint()
    {
        (await _client.GetAsync(new Uri(ProbeEndpoint.AnonymousRoute, UriKind.Relative), Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.GetAsync(new Uri(ProbeEndpoint.CompanyRoute, UriKind.Relative), Ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A refusal from the gate names an i18n key, so the Arabic UI has something to show.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AC-106-B and TC-1-050 require the key as well as the status, and until 2026-08-24 the gate's
    /// refusals carried none: 401 and 403 are written by the authentication and authorization
    /// middleware rather than by <c>ResultExtensions.Problem</c>, so the body was a bare
    /// ProblemDetails [qa/slice-1/verification-2026-08-23.md, finding V-A].
    /// </para>
    /// <para>
    /// <b>It is asserted here rather than only in <c>CreateUserTests</c> because the fix is not the
    /// create-user endpoint's.</b> It is one <c>CustomizeProblemDetails</c> callback on the shared
    /// problem-details writer [Verified: 2026-08-24 @ <c>Program.cs</c> -&gt;
    /// <c>AddProblemDetails</c>], and these two probe routes carry no feature code at all — so a
    /// later per-endpoint patch that left every sibling refusing in silence would turn this red.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_refusal_from_the_gate_names_a_key_the_ui_can_render()
    {
        HttpResponseMessage anonymous =
            await _client.GetAsync(new Uri(ProbeEndpoint.CompanyRoute, UriKind.Relative), Ct);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await MessageKeyAsync(anonymous)).Should().Be("errors.auth.not_authenticated");

        HttpResponseMessage refused = await SendAsync(
            Read(_projectId), _financeUnassigned, Role.Finance, Department.Finance);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(refused)).Should().Be("errors.auth.forbidden");
    }

    [Fact]
    public async Task The_right_role_without_an_assignment_is_refused()
    {
        // spec.md §9: "A user MUST be assigned to a project to open it or act on it." Finance holds
        // the permission and is still refused on a project it is not assigned to.
        HttpResponseMessage response = await SendAsync(
            Read(_projectId), _financeUnassigned, Role.Finance, Department.Finance);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_right_role_with_an_assignment_is_allowed()
    {
        HttpResponseMessage response = await SendAsync(
            Read(_projectId), _financeAssigned, Role.Finance, Department.Finance);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_assignment_to_one_project_does_not_open_another()
    {
        HttpResponseMessage response = await SendAsync(
            Read(_otherProjectId), _financeAssigned, Role.Finance, Department.Finance);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_owner_reaches_every_project_without_an_assignment()
    {
        // Karim, 2026-08-17: "owner role is like the admin so yes global." decisions.md D-010.
        // The unassigned Owner reaches both projects.
        (await SendAsync(Approve(_projectId), _ownerUnassigned, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(Approve(_otherProjectId), _ownerUnassigned, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_explicit_owner_assignment_is_still_harmless()
    {
        // Owner assignment rows are now optional rather than required. Rows created before the rule
        // changed must keep working, so the answer needs no data migration.
        HttpResponseMessage response = await SendAsync(
            Approve(_projectId), _ownerAssigned, Role.Owner);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_owners_global_reach_is_not_unconditional()
    {
        // A project identifier that names nothing is still refused, so a typo cannot pass as access.
        HttpResponseMessage response = await SendAsync(
            Approve(Guid.CreateVersion7()), _ownerUnassigned, Role.Owner);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Global_reach_does_not_mean_every_capability()
    {
        // The Owner reaches every project, but still holds only what the catalogue grants.
        // spec.md §9: "Technical Office gates quantities, never money" — and the Owner does not gate
        // quantities either.
        HttpResponseMessage response = await SendAsync(
            Submit(_projectId), _ownerUnassigned, Role.Owner);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_junior_engineer_cannot_submit_what_a_supervisor_submits()
    {
        // spec.md §9: "a junior engineer raises requests as drafts; the supervisor submits them."
        HttpResponseMessage junior = await SendAsync(
            Submit(_projectId), _juniorEngineer, Role.SiteEngineer,
            Department.Operations, OperationsSubDepartment.Technical);

        junior.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage supervisor = await SendAsync(
            Submit(_projectId), _supervisingEngineer, Role.SiteEngineer,
            Department.Operations, OperationsSubDepartment.Technical);

        supervisor.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_site_engineer_cannot_approve_money()
    {
        // spec.md §9: "Site engineers approve nothing financial."
        HttpResponseMessage response = await SendAsync(
            Approve(_projectId), _supervisingEngineer, Role.SiteEngineer,
            Department.Operations, OperationsSubDepartment.Technical);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_company_wide_permission_needs_no_assignment()
    {
        HttpResponseMessage response = await SendAsync(
            new Uri(ProbeEndpoint.CompanyRoute, UriKind.Relative), _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_portal_client_reaches_only_its_own_project()
    {
        // spec.md §12: "The client MUST NEVER see … any other client's data." The portal user holds
        // no assignment; access comes from Project.ClientId matching User.ClientId — read from the
        // database, never from the request.
        HttpResponseMessage own = await SendAsync(
            Portal(_projectId), _portalClient, Role.Client, clientId: _clientId);

        own.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage other = await SendAsync(
            Portal(_otherProjectId), _portalClient, Role.Client, clientId: _clientId);

        other.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_portal_client_cannot_reach_an_internal_project_endpoint()
    {
        // decisions.md D-035. Role.Client used to hold ProjectRead, which meant any internal endpoint
        // requiring only that permission — a project header, a summary, a BOQ view — was reachable by
        // a portal user on their own project. The portal now goes through PortalRead alone.
        HttpResponseMessage response = await SendAsync(
            Read(_projectId), _portalClient, Role.Client, clientId: _clientId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_deactivated_user_is_refused_at_the_next_request()
    {
        // The token says nothing about whether the account is still live. Slice 0 read role and
        // client scope from claims and never revalidated them, so deactivating a user had no effect
        // until their token expired — and the Owner, who reaches everything, was the account that
        // most needed to be revocable.
        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _financeAssigned, Ct);
            _ = user.Deactivate(Now);
            await context.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await SendAsync(
            Read(_projectId), _financeAssigned, Role.Finance, Department.Finance);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_deactivated_user_loses_company_wide_permissions_too()
    {
        // The gap the project-scoped test above could not see. Until 2026-08-20 the handler only
        // consulted the database when the request named a project, so a CompanyWide permission was
        // decided entirely from token claims. A deactivated Owner kept UserManage, and a deactivated
        // Finance user kept TreasuryPostCompany — which moves money — until their token expired.
        //
        // Two tests appeared to cover revocation and both used project-scoped routes, which is why
        // this survived. See decisions.md D-048.
        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _marketing, Ct);
            _ = user.Deactivate(Now);
            await context.SaveChangesAsync(Ct);
        }

        // ProbeEndpoint.CompanyRoute requires ClientManage: company-wide, no project, no assignment.
        HttpResponseMessage company = await SendAsync(
            new Uri(ProbeEndpoint.CompanyRoute, UriKind.Relative), _marketing, Role.MarketingSales, Department.Marketing);

        company.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_deactivated_owner_cannot_administer_users()
    {
        // The sharpest case of the same defect, stated separately because it is the one that matters
        // most: the Owner reaches everything, so the Owner is the account that most needs to be
        // revocable — and UserManage is how an attacker would make the revocation permanent.
        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _ownerUnassigned, Ct);
            _ = user.Deactivate(Now);
            await context.SaveChangesAsync(Ct);
        }

        (await SendAsync(UserAdmin, _ownerUnassigned, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rotating_the_security_stamp_kills_every_existing_session()
    {
        // Karim, 2026-08-21 (D-049 ruling 3): "a password change or account deactivation must kill
        // all active tokens". The Architect chose stamp rotation over a session table (D-051, N5),
        // accepting that revocation is all-or-nothing rather than per-device.
        //
        // The mechanism was declared and NOT implemented until 2026-08-22 — the stamp claim existed,
        // User rotated it, and nothing ever compared the two. See decisions.md D-053.
        string stampBefore = await CurrentStampAsync(_financeAssigned);

        // The token still works, which is what makes the next assertion mean something.
        (await SendAsync(Read(_projectId), _financeAssigned, Role.Finance, Department.Finance,
                securityStamp: stampBefore))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // A password change. The account stays ACTIVE — this is not the deactivation path, and if it
        // were, IsActive would refuse the request and prove nothing about the stamp.
        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _financeAssigned, Ct);
            user.SetOwnPassword("a-new-hash").IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        string stampAfter = await CurrentStampAsync(_financeAssigned);
        stampAfter.Should().NotBe(stampBefore, "SetOwnPassword must rotate the stamp");

        // The device that was signed in before the change — a phone left on site, say.
        (await SendAsync(Read(_projectId), _financeAssigned, Role.Finance, Department.Finance,
                securityStamp: stampBefore))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the old session is dead everywhere");

        // Signing in again works, and the user is still active.
        (await SendAsync(Read(_projectId), _financeAssigned, Role.Finance, Department.Finance,
                securityStamp: stampAfter))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_request_with_no_security_stamp_is_refused()
    {
        // D-051 named the trap: a revocation check with a "skip when the claim is absent" fallback
        // reads as protection and grants none. A token without the claim is not a token this system
        // issued, so it is refused rather than waved through.
        using var request = new HttpRequestMessage(HttpMethod.Get, Read(_projectId));
        request.Headers.Add(TestAuthHandler.UserIdHeader, _financeAssigned.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.Finance.ToString());
        request.Headers.Add(TestAuthHandler.DepartmentHeader, Department.Finance.ToString());
        // No stamp header.

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_stale_department_claim_grants_nothing()
    {
        // Department is the second grant axis, and some grants name a department and no role at all
        // (SiteExpenseConfirm, PhotoPublish). It was taken from the token and never revalidated, so a
        // user moved out of a department kept its permissions until their token expired.
        //
        // Here the claim says Marketing — which does hold ClientManage — while the stored department
        // is Finance, which does not. The stored value must win.
        HttpResponseMessage response = await SendAsync(
            new Uri(ProbeEndpoint.CompanyRoute, UriKind.Relative),
            _financeAssigned,
            Role.MarketingSales,
            Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "role and department come from the users table, not from the token");
    }

    [Fact]
    public async Task A_token_claiming_a_role_the_user_no_longer_holds_is_refused()
    {
        // Nothing stops a stale or forged claim reaching the handler; what stops it mattering is that
        // the policy compares the token's role against the database and refuses a mismatch. Without
        // this, minting role=Owner would have granted reach to every project in the company.
        HttpResponseMessage response = await SendAsync(
            Approve(_projectId), _marketing, Role.Owner);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_staffs_a_project_it_was_never_assigned_to()
    {
        // Karim, 2026-08-20: "HR has Global Reach for project assignments … HR does not need to be
        // assigned to a project first in order to staff it."
        //
        // This is not merely a convenience. Requiring an assignment in order to create assignments
        // is circular: on a brand-new project nobody is assigned, so nobody could ever make the
        // first one. The seeded HR user holds no ProjectAssignment row at all.
        HttpResponseMessage response = await SendAsync(
            Assign(_projectId), _hr, Role.Hr, Department.Hr);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hr_reaches_every_project_and_can_read_none_of_them()
    {
        // The other half of the same ruling: "HR is strictly administrative and must have zero
        // financial visibility (cannot see project costs, margins, or the safe)."
        //
        // Global reach makes this the sharpest test in the file. The access policy waves HR through
        // on any project — so the ONLY thing standing between HR and every project's financial data
        // is the absence of a ProjectRead grant. Same user, same project, one line apart.
        (await SendAsync(Assign(_projectId), _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(Read(_projectId), _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(Read(_otherProjectId), _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(Approve(_projectId), _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_global_reach_still_stops_at_a_project_that_does_not_exist()
    {
        // Global reach is reach, not a bypass. It is bounded by the project existing, exactly as the
        // Owner's is — otherwise a typo'd identifier would be an authorization success.
        HttpResponseMessage response = await SendAsync(
            Assign(Guid.CreateVersion7()), _hr, Role.Hr, Department.Hr);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_the_owner_administers_users()
    {
        // Karim, 2026-08-20: "The UserManage permission is strictly Global and held exclusively by
        // the Owner." Note the Owner passes with NO assignment — the permission is company-wide.
        (await SendAsync(UserAdmin, _ownerUnassigned, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // HR staffs projects with users that already exist. It does not mint them, and it does not
        // hand out roles — which is what would let HR grant itself the financial visibility the
        // ruling denies it.
        (await SendAsync(UserAdmin, _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(UserAdmin, _financeAssigned, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(UserAdmin, _marketing, Role.MarketingSales, Department.Marketing))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- KAFF-116: what the gate records about how it let the caller in -----------------------
    //
    // These live here rather than beside the other audit tests because the value is produced by this
    // gate and nowhere else: the access policy names the path, the handler hands it to the audit
    // context, and the interceptor writes it. Seeding four kinds of actor is exactly what this
    // fixture already does.

    /// <summary>AC-116-A. The ordinary case, and the one the other three must not look like.</summary>
    [Fact]
    public async Task An_assigned_actor_leaves_the_assignment_path_on_the_record()
    {
        (await SendAsync(Write(_projectId), _financeAssigned, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.ProjectId.Should().Be(_projectId);
        change.GrantPath.Should().Be(ProjectAccessPath.Assignment);
    }

    /// <summary>
    /// AC-116-B, and AC-116-E in the same request.
    /// </summary>
    /// <remarks>
    /// The Owner is the whole reason the field exists: global reach means no assignment row exists
    /// to point at, so nothing else in the system can answer "by what authority". The second half is
    /// the same save's company-level record — no project, therefore no path, rather than the
    /// caller's path by default.
    /// </remarks>
    [Fact]
    public async Task The_owners_reach_is_named_although_it_leaves_no_row()
    {
        (await SendAsync(WriteAsOwner(_projectId), _ownerUnassigned, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, AuditRecord companyWide) = await TheWritesRecordsAsync();

        change.GrantPath.Should().Be(ProjectAccessPath.OwnerGlobal, "not Assignment, and not null");

        companyWide.ProjectId.Should().BeNull();
        companyWide.GrantPath.Should().BeNull("a company-wide act went through no access policy");
    }

    /// <summary>
    /// AC-116-C. HR's reach and the Owner's used to be one branch, so the trail could not tell
    /// staffing from ownership. They are two paths now and the record says which one ran.
    /// </summary>
    [Fact]
    public async Task Hrs_global_reach_is_distinguishable_from_an_assigned_actors()
    {
        (await SendAsync(WriteAsHr(_projectId), _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.GrantPath.Should().Be(ProjectAccessPath.HrGlobal);
    }

    /// <summary>
    /// AC-116-D. A separate boundary gets a separate name: no assignment row exists and the match is
    /// Project.ClientId against User.ClientId (decisions.md D-035).
    /// </summary>
    [Fact]
    public async Task A_portal_clients_record_names_the_portal_boundary()
    {
        (await SendAsync(WriteAsPortalClient(_projectId), _portalClient, Role.Client, clientId: _clientId))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.GrantPath.Should().Be(ProjectAccessPath.PortalClient);
    }

    /// <summary>
    /// decisions.md D-063 §2. Written by the same middleware that already writes the request path,
    /// from <c>HttpContext.Connection.RemoteIpAddress</c> — this is the one test in the suite that
    /// actually goes through that middleware rather than constructing an <c>AuditContext</c> by hand.
    /// </summary>
    [Fact]
    public async Task A_write_through_a_real_request_records_the_connections_address()
    {
        (await SendAsync(Write(_projectId), _financeAssigned, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.IpAddress.Should().Be(KaffApiFactory.TestRemoteAddress);
    }

    /// <summary>
    /// With no proxy network configured — the shipped default — a forwarded header is not read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the guard on decisions.md D-079's one footgun. <c>ForwardedHeadersMiddleware</c> reads
    /// "no known proxies and no known networks" as <i>check nobody</i>, i.e. trust every peer that
    /// sends the header — so registering it unconditionally would turn an empty allowlist into
    /// universal trust, which is the opposite of what an empty allowlist means everywhere else in
    /// this application [Verified: 2026-08-25 @ <c>Program.cs</c> -&gt; the
    /// <c>Kaff:TrustedProxyNetworks</c> block]. <c>Program.cs</c> therefore registers it only when
    /// the list is non-empty, and this test is what turns red if somebody lifts it out of that
    /// conditional to tidy the pipeline.
    /// </para>
    /// <para>
    /// It also holds D-063 §2's original rule for development, CI and the test host: those have no
    /// proxy, and a header there is a caller-supplied string and nothing more.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_forwarded_header_is_ignored_when_no_proxy_network_is_trusted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Write(_projectId));
        request.Headers.Add("X-Forwarded-For", "198.51.100.7");
        await StampAsync(request, _financeAssigned, Role.Finance, Department.Finance);

        (await _client.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.IpAddress.Should().Be(KaffApiFactory.TestRemoteAddress);
    }

    /// <summary>
    /// Behind a trusted proxy the audited address is the caller's, and a forged entry does not
    /// displace it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// decisions.md D-079. Staging's API is reachable only from nginx
    /// [Verified: 2026-08-25 @ <c>deploy/docker-compose.staging.yml</c> -&gt; the <c>api</c> service's
    /// <c>expose</c>], so without this every audit row there would carry the proxy's container
    /// address — including the failed sign-ins the column exists for.
    /// </para>
    /// <para>
    /// <b>The header shape is nginx's, not a convenient one.</b> <c>$proxy_add_x_forwarded_for</c>
    /// appends the peer's address to the <i>right</i> of whatever the caller sent
    /// [Verified: 2026-08-25 @ <c>src/Web/nginx.conf.template</c> -&gt; the <c>location /api/</c>
    /// block], so a caller who forges <c>198.51.100.9</c> produces exactly the two-entry header
    /// below. Asserting that the <i>rightmost</i> entry is recorded is therefore asserting that a
    /// forged one is not — a single-entry header would prove only half of that.
    /// </para>
    /// <para>
    /// <b>What stops the forgery is the allowlist, not <c>ForwardLimit</c>.</b> Raising the limit to
    /// 2 was tried on 2026-08-25 and this test stayed green: the middleware stops as soon as the
    /// address it would consume next is not a known proxy, and <c>198.51.100.7</c> is not in
    /// <c>192.0.2.0/24</c>. Recorded because the first draft of this remark claimed the opposite,
    /// and a plausible wrong reason in a test's own documentation is what a later session copies.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Behind_a_trusted_proxy_the_recorded_address_is_the_caller_not_the_proxy()
    {
        IPAddress proxy = IPAddress.Parse("192.0.2.10");
        IPAddress caller = IPAddress.Parse("198.51.100.7");

        await using var proxied = new KaffApiFactory(
            _database.ConnectionString,
            remoteAddress: proxy,
            trustedProxyNetwork: "192.0.2.0/24");

        using HttpClient client = proxied.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, Write(_projectId));
        request.Headers.Add("X-Forwarded-For", $"198.51.100.9, {caller}");
        await StampAsync(request, _financeAssigned, Role.Finance, Department.Finance);

        (await client.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (AuditRecord change, _) = await TheWritesRecordsAsync();

        change.IpAddress.Should().Be(caller);
        change.IpAddress.Should().NotBe(proxy);
    }

    /// <summary>
    /// The two records one probe write leaves: the change to the project, and the company-level
    /// client created in the same save.
    /// </summary>
    private async Task<(AuditRecord Change, AuditRecord CompanyWide)> TheWritesRecordsAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        // Modified, because seeding wrote a Created record for the same project with no request
        // behind it and therefore no grant path.
        AuditRecord change = await reader.AuditRecords
            .SingleAsync(record => record.EntityId == _projectId && record.Action == AuditAction.Modified, Ct);

        AuditRecord companyWide = await reader.AuditRecords
            .SingleAsync(
                record => record.CorrelationId == change.CorrelationId
                    && record.EntityType == nameof(Client),
                Ct);

        return (change, companyWide);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static Uri Write(Guid projectId) =>
        new($"/probe/projects/{projectId}/write", UriKind.Relative);

    private static Uri WriteAsOwner(Guid projectId) =>
        new($"/probe/projects/{projectId}/write-owner", UriKind.Relative);

    private static Uri WriteAsHr(Guid projectId) =>
        new($"/probe/projects/{projectId}/write-hr", UriKind.Relative);

    private static Uri WriteAsPortalClient(Guid projectId) =>
        new($"/probe/portal/projects/{projectId}/write", UriKind.Relative);

    private static Uri Read(Guid projectId) =>
        new($"/probe/projects/{projectId}", UriKind.Relative);

    private static Uri Approve(Guid projectId) =>
        new($"/probe/projects/{projectId}/approve", UriKind.Relative);

    private static Uri Submit(Guid projectId) =>
        new($"/probe/projects/{projectId}/submit", UriKind.Relative);

    private static Uri Portal(Guid projectId) =>
        new($"/probe/portal/projects/{projectId}", UriKind.Relative);

    private static Uri Assign(Guid projectId) =>
        new($"/probe/projects/{projectId}/assign", UriKind.Relative);

    private static readonly Uri UserAdmin = new(ProbeEndpoint.UserAdminRoute, UriKind.Relative);

    /// <summary>
    /// Issues a request as <paramref name="userId"/>.
    /// </summary>
    /// <param name="securityStamp">
    /// The stamp the caller's token was issued against. When omitted the user's <b>current</b> stamp
    /// is read from the database, which is what a token minted a moment ago would carry. A test that
    /// wants to prove the global sign-out works passes a stale one deliberately.
    /// </param>
    private async Task<HttpResponseMessage> SendAsync(
        Uri route,
        Guid userId,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null,
        string? securityStamp = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        await StampAsync(request, userId, role, department, subDepartment, clientId, securityStamp);

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>
    /// The identity headers <see cref="TestAuthHandler"/> reads, put on a request the caller owns.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="SendAsync"/> so the two decisions.md D-079 tests can add a forwarded
    /// header — and one of them use its own client — without a second copy of the header block that
    /// would drift the moment a claim is added.
    /// </remarks>
    private async Task StampAsync(
        HttpRequestMessage request,
        Guid userId,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null,
        string? securityStamp = null)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, role.ToString());
        request.Headers.Add(
            TestAuthHandler.SecurityStampHeader,
            securityStamp ?? await CurrentStampAsync(userId));

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }

        if (subDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.SubDepartmentHeader, subDepartment.Value.ToString());
        }

        if (clientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, clientId.Value.ToString());
        }
    }

    /// <summary>The stamp stored for this user right now, or a placeholder if they no longer exist.</summary>
    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext context = _database.CreateContext();

        string? stamp = await context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .FirstOrDefaultAsync(Ct);

        // A deleted user has no stamp, and the request must still be refused rather than throw.
        return stamp ?? "no-such-user";
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = MakeClient("PERM-C1", "عميل الصلاحيات");
        Client otherClient = MakeClient("PERM-C2", "عميل آخر");

        Project project = MakeProject("PERM-P1", client.Id);
        Project otherProject = MakeProject("PERM-P2", otherClient.Id);

        User ownerAssigned = MakeUser("perm-owner-assigned", Role.Owner);
        User ownerUnassigned = MakeUser("perm-owner-unassigned", Role.Owner);
        User financeAssigned = MakeUser("perm-finance-assigned", Role.Finance, Department.Finance);
        User financeUnassigned = MakeUser("perm-finance-unassigned", Role.Finance, Department.Finance);
        User junior = MakeUser("perm-junior", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User supervisor = MakeUser("perm-supervisor", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("perm-marketing", Role.MarketingSales, Department.Marketing);
        User portal = MakeUser("perm-portal", Role.Client, clientId: client.Id);

        // Deliberately never assigned to any project. That is the whole point of the HR tests:
        // Karim, 2026-08-20 — "HR does not need to be assigned to a project first in order to
        // staff it." An assignment row here would make those tests pass for the wrong reason.
        User hr = MakeUser("perm-hr", Role.Hr, Department.Hr);

        context.Clients.AddRange(client, otherClient);
        context.Projects.AddRange(project, otherProject);
        context.Users.AddRange(
            ownerAssigned, ownerUnassigned, financeAssigned, financeUnassigned,
            junior, supervisor, marketing, portal, hr);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(project.Id, ownerAssigned, AssignmentLevel.Standard, ownerAssigned.Id, Now).Value,
            ProjectAssignment.Create(project.Id, financeAssigned, AssignmentLevel.Standard, ownerAssigned.Id, Now).Value,
            ProjectAssignment.Create(project.Id, junior, AssignmentLevel.Junior, ownerAssigned.Id, Now).Value,
            ProjectAssignment.Create(project.Id, supervisor, AssignmentLevel.Supervisor, ownerAssigned.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _projectId = project.Id;
        _otherProjectId = otherProject.Id;
        _ownerAssigned = ownerAssigned.Id;
        _ownerUnassigned = ownerUnassigned.Id;
        _financeAssigned = financeAssigned.Id;
        _financeUnassigned = financeUnassigned.Id;
        _juniorEngineer = junior.Id;
        _supervisingEngineer = supervisor.Id;
        _marketing = marketing.Id;
        _portalClient = portal.Id;
        _hr = hr.Id;
    }

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Ambient test cancellation, threaded through every database and HTTP call.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The <c>messageKey</c> extension the API puts on every refusal, so the client can translate it.</summary>
    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private static Client MakeClient(string code, string name)
    {
        return Client.Create(UniqueNames.Code(code), name, UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
    }

    private static Project MakeProject(string code, Guid clientId) =>
        Project.Create(UniqueNames.Code(code), "مشروع", clientId, ContractType.LumpSum, Now).Value;

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
    {
        return User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment,
            clientId).Value;
    }
}
