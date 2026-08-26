using System.Reflection;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// A-04. Every route the application maps is gated, or is a named member of the allow-list below.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this test exists.</b> <c>PUT /api/users/{userId}/department</c> shipped with
/// <c>.WithName()</c>, <c>.WithTags()</c> and no <c>RequirePermission</c> — decisions.md D-067. The
/// fallback policy admitted every authenticated caller, so any user could move any user between
/// departments, and a department is one of the two axes a permission is granted against (spec.md §9).
/// Its own XML comment claimed the check was there. Prose a reviewer would rely on to answer a safety
/// question is not documentation; decisions.md D-068 ruled that the answer is a machine rather than a
/// fifth rule, and this is the machine.
/// </para>
/// <para>
/// <b>Endpoint metadata is the source of truth, not the source text.</b> The requirement is read from
/// the routes the host actually built, so a route mapped anywhere by any means is enumerated — a
/// grep over <c>Endpoint.cs</c> files would see what somebody wrote, which is precisely the artefact
/// that was wrong in D-067. <c>RequirePermission</c> records itself as an <see cref="IAuthorizeData"/>
/// whose policy name round-trips through <see cref="PermissionRequirement.TryParse"/>
/// [Verified: 2026-08-24 @ <c>PermissionPolicyProvider.cs</c> -&gt; <c>RequirePermission</c>], and
/// nothing else in the pipeline produces such a policy name.
/// </para>
/// <para>
/// <b>Authenticated is not authorized.</b> The check is for a <see cref="PermissionRequirement"/>
/// policy specifically, never for authorization in general: the fallback policy already requires an
/// authenticated caller on every endpoint
/// [Verified: 2026-08-24 @ <c>Program.cs</c> -&gt; <c>SetFallbackPolicy</c>], and that is exactly what
/// let D-067 through.
/// </para>
/// <para>
/// The test host adds routes of its own [Verified: 2026-08-24 @ <c>ProbeEndpoint.cs</c> -&gt;
/// <c>Map</c>], including a deliberately anonymous one. Only handlers declared in the
/// <c>Kaff.Api</c> assembly are shipped surface, and an endpoint whose handler cannot be identified
/// is treated as shipped — the filter fails closed.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class EndpointPermissionCoverageTests : IAsyncLifetime
{
    /// <summary>
    /// The endpoints that may ship with no permission requirement. One member today; each is a decision.
    /// </summary>
    /// <remarks>
    /// <b>Adding a member is the decision, not a formality.</b> An allow-list that grows by accident
    /// is the defect wearing the fix's clothes (decisions.md D-068), so each entry names the method,
    /// the route and the reason it is reachable unauthenticated, and the entry is what a reviewer
    /// reads. Sign-in (KAFF-101a) was expected to become the fourth member and was deliberately
    /// <b>not</b> pre-listed: the test going red on the day that route was mapped was the visible
    /// act. It went red on 2026-08-26 with <c>{"POST /api/auth/sign-in"}</c>, and the fourth entry
    /// below is the answer to it.
    /// </remarks>
    private static readonly AnonymousEndpoint[] AllowList =
    [
        new(
            "GET",
            "/api/health",
            "A liveness probe carries no credentials, and it is the answer to an operational question "
            + "an unauthenticated caller must be able to ask: are the PostgreSQL guards installed on "
            + "this deployment (decisions.md D-033). It discloses whether the database answers and "
            + "which guards are missing, and nothing else."),
        new(
            "GET",
            "/api/setup",
            "KAFF-100. The SPA must learn whether the one-time setup screen may still be reached "
            + "before anybody has signed in, or it can never route to it. The answer is exactly "
            + "!Users.AnyAsync() — the same emptiness test the gate below enforces — and discloses "
            + "nothing a stranger could not already infer from the setup screen itself."),
        new(
            "POST",
            "/api/setup",
            "KAFF-100 — the most privileged endpoint the system will ever have, and it is anonymous by "
            + "construction: there is no identity to check on a database with no users. Its gate is "
            + "not RequirePermission but two properties of the database itself — the users table is "
            + "empty (rule 4/5) and ux_users_bootstrap_owner_once, a unique index that turns two "
            + "concurrent requests into one Owner and one refusal (rule 6, decisions.md D-051 Q31)."),
        new(
            "POST",
            "/api/auth/sign-in",
            "KAFF-101a — the staff door, and the one endpoint whose whole job is to produce the "
            + "identity every other endpoint requires. There is nothing to check a permission "
            + "against. Its gate is the credential itself: PasswordHasher.Verify runs before "
            + "anything else decides the response (rule 14a, decisions.md D-072 §1), and every "
            + "refusal it can give is the same 401 / errors.auth.invalid_credentials bar one — a "
            + "locked account whose password was correct, which gets 423 and can only be seen by "
            + "somebody who already holds that password (D-065, D-072 §1). It discloses nothing "
            + "about which usernames exist, by status code or by clock."),
        new(
            "POST",
            "/api/auth/sign-out",
            "KAFF-102 — story rule 7: \"signing out when already signed out is not an error worth a "
            + "refusal.\" Behind the fallback policy an unauthenticated caller would be refused 401 "
            + "before the handler ran, which is exactly the refusal the rule forbids. There is no "
            + "permission to check either way: any authenticated role may end its own device's "
            + "session (story bullet \"authenticated, any role\"), and a caller who holds no session "
            + "gets the same 204 with nothing disclosed about any account."),
    ];

    /// <summary>
    /// The endpoints that may ship with no permission requirement <b>and</b> no <c>AllowAnonymous()</c>
    /// — reachable only by an authenticated caller, acting on nothing but their own row.
    /// </summary>
    /// <remarks>
    /// <b>Distinct from <see cref="AllowList"/>, and deliberately not folded into it.</b> That list is
    /// for a route with no identity to check at all; <see cref="Every_allow_list_member_is_mapped_and_says_so_in_its_own_file"/>
    /// requires every member to carry <c>AllowAnonymous()</c>, which would make an unauthenticated
    /// caller reach it too — exactly wrong for an endpoint like KAFF-103's, whose whole point is that
    /// only the signed-in holder of the row may call it. There is no catalogue <c>Permission</c> for
    /// "act on yourself alone", and inventing one would misstate the rule: this is not a grant any role
    /// holds over anyone. KAFF-100 added a narrow <c>AllowList</c> exemption and KAFF-108 shipped with
    /// no gate at all (D-067) — this is the same discipline applied to the shape D-067 did not
    /// anticipate: authenticated, but with no permission because there is nothing to grant.
    /// </remarks>
    private static readonly SelfOnlyEndpoint[] SelfOnlyEndpoints =
    [
        new(
            "POST",
            "/api/auth/change-password",
            "KAFF-103. \"Authenticated as the user themselves. Not UserManage — only the person "
            + "changes it.\" The handler re-reads the caller's own row and re-checks IsActive and the "
            + "security stamp itself (decisions.md D-048), because no permission gate runs here to do "
            + "it — the same freshness PermissionSubjectReader applies to every RequirePermission "
            + "route. A future endpoint that wants this shape for anything wider than a caller's own "
            + "row is not this one; it needs a real permission."),
        new(
            "GET",
            "/api/auth/me",
            "KAFF-105a. \"Authenticated, any role, no assignment. It returns only the caller's own "
            + "facts.\" There is no catalogue Permission for reading your own profile, and this route "
            + "must be reachable while MustChangePassword is true (AC-105a-C, decisions.md D-072 §2) — "
            + "a RequirePermission here would run PermissionEvaluator's PasswordChangeRequired check "
            + "(D-086) and refuse exactly the call D-072 §2 requires to succeed. The handler re-reads "
            + "IsActive and the security stamp itself, the same freshness D-048 applies everywhere "
            + "else."),
    ];

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;

    public EndpointPermissionCoverageTests(PostgresDatabase database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new KaffApiFactory(_database.ConnectionString);

        // Reading Services starts the host, which is what runs Program and maps the routes.
        _ = _factory.Services;

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void Every_mapped_endpoint_carries_a_permission_requirement()
    {
        List<string> ungated = [];

        foreach (MappedEndpoint mapped in ShippedEndpoints())
        {
            if (IsAllowListed(mapped) || IsSelfOnlyListed(mapped) || DeclaredPermissions(mapped).Count > 0)
            {
                continue;
            }

            ungated.Add(mapped.ToString());
        }

        ungated.Should().BeEmpty(
            "every mapped endpoint must declare RequirePermission(...), be a named member of AllowList "
            + "with the reason it is reachable unauthenticated, or be a named member of "
            + "SelfOnlyEndpoints with the reason it needs no permission beyond being signed in. An "
            + "endpoint behind the fallback policy alone with none of the three is open to every "
            + "authenticated caller for whatever it does — decisions.md D-067, where that was a "
            + "privilege-escalation primitive");
    }

    /// <summary>
    /// Every <see cref="SelfOnlyEndpoints"/> member is mapped, requires authentication, and carries no
    /// <c>RequirePermission</c> of its own.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="Every_allow_list_member_is_mapped_and_says_so_in_its_own_file"/>, but
    /// asserting the opposite metadata: <b>no</b> <see cref="IAllowAnonymous"/> — an unauthenticated
    /// caller must still be refused by the fallback policy — and <b>no</b> parseable
    /// <see cref="PermissionRequirement"/>, so a route that already declares a real permission is not
    /// also (redundantly, or misleadingly) named here.
    /// </remarks>
    [Fact]
    public void Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own()
    {
        List<MappedEndpoint> shipped = [.. ShippedEndpoints()];

        foreach (SelfOnlyEndpoint entry in SelfOnlyEndpoints)
        {
            MappedEndpoint? mapped = shipped.SingleOrDefault(
                candidate => string.Equals(entry.Method, candidate.Method, StringComparison.Ordinal)
                             && string.Equals(entry.Route, candidate.Route, StringComparison.Ordinal));

            mapped.Should().NotBeNull(
                "SelfOnlyEndpoints names {0} {1}, which no endpoint maps. A dead exemption is one "
                + "nobody re-reads, and it silently pre-authorises whatever claims that route next",
                entry.Method,
                entry.Route);

            mapped!.Endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().BeNull(
                "{0} {1} is listed as self-only, not anonymous — an unauthenticated caller must still "
                + "be refused by the fallback policy",
                entry.Method,
                entry.Route);

            DeclaredPermissions(mapped).Should().BeEmpty(
                "{0} {1} is listed as self-only because it carries no RequirePermission. If it has "
                + "grown one, it belongs to the ordinary gated set instead and should come off this list",
                entry.Method,
                entry.Route);

            mapped.Endpoint.Metadata.GetMetadata<LiveSession.Marker>().Should().NotBeNull(
                "{0} {1} is outside the permission gate, so nothing upstream re-reads IsActive, the "
                + "security stamp, or whether the role may hold a staff session at all. It must "
                + "declare RequireLiveSession(), which is the only thing that adds this metadata — "
                + "the entry above records why the route is exempt, and this records that it paid "
                + "what the exemption costs (V-26-B, decisions.md D-089)",
                entry.Method,
                entry.Route);
        }
    }

    /// <summary>
    /// <c>V-26-B</c>, the pattern rather than the instance: no endpoint re-implements "who is calling"
    /// from the token by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is how the defect was written, three times.</b> <c>WhoAmI</c>, <c>ChangePassword</c> and
    /// <c>SignOut</c> each carried their own <c>ReadUserId(ClaimsPrincipal)</c> over
    /// <c>KaffClaimTypes.UserId</c> and then decided, per file, which of the gate's checks to re-apply.
    /// Two of three applied two of three; one applied none. Every one of them is a route the gate does
    /// not run on, and every one of them was free to be one item short.
    /// </para>
    /// <para>
    /// <b>Why a source scan and not metadata.</b> The metadata assertion above covers the authenticated
    /// exemption, because a filter can be required there. It cannot cover the anonymous one — sign-out
    /// must answer <c>204</c> to a caller holding no session at all (rule 7), so no refusing filter can
    /// sit in front of it — and sign-out is precisely where the hand-roll did the most damage
    /// (<c>V-26-C</c>: a permanent audit row on a dead token). What is checkable there is the hand-roll
    /// itself: turning a token into a caller is <c>LiveSession</c>'s job and no feature's, so no file
    /// under <c>src/Api/Features/</c> has any business naming a claim type. D-067's warning about
    /// source text does not apply — this asserts what the text is, not that a comment describes the
    /// code correctly.
    /// </para>
    /// <para>
    /// <b>Its ceiling, named.</b> A handler could still load its own caller's row through
    /// <c>ICurrentUser.UserId</c> without naming a claim type. Nothing here catches that; the reviewer
    /// does. It catches the shape all three defective handlers actually had.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_feature_handler_reads_the_callers_identity_from_the_token_itself()
    {
        DirectoryInfo features = new(Path.Combine(RepositoryRoot().FullName, "src", "Api", "Features"));

        features.Exists.Should().BeTrue("this test reads the shipped feature slices from disk");

        List<string> handRolled = [.. features
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file.FullName).Contains("KaffClaimTypes", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(features.FullName, file.FullName))];

        handRolled.Should().BeEmpty(
            "a route outside the permission gate that answers 'who is calling' from the token itself "
            + "also decides, alone, which of the gate's checks to re-apply — and three of them did, "
            + "each one item short (V-26-B, V-26-C). LiveSession.ResolveAsync is the one answer, and "
            + "it applies all three");
    }

    /// <summary>The directory holding <c>KaffErp.sln</c>, walked up from the test binary.</summary>
    private static DirectoryInfo RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KaffErp.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException(
            $"No KaffErp.sln above '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void Every_allow_list_member_is_mapped_and_says_so_in_its_own_file()
    {
        List<MappedEndpoint> shipped = [.. ShippedEndpoints()];

        foreach (AnonymousEndpoint entry in AllowList)
        {
            MappedEndpoint? mapped = shipped.SingleOrDefault(
                candidate => string.Equals(entry.Method, candidate.Method, StringComparison.Ordinal)
                             && string.Equals(entry.Route, candidate.Route, StringComparison.Ordinal));

            mapped.Should().NotBeNull(
                "the allow-list names {0} {1}, which no endpoint maps. A dead exemption is an "
                + "exemption nobody re-reads, and it silently pre-authorises whatever claims that "
                + "route next",
                entry.Method,
                entry.Route);

            mapped!.Endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().NotBeNull(
                "{0} {1} is allow-listed here, so its own slice must say AllowAnonymous() rather than "
                + "leaving the reader of that file to discover the exemption in a test",
                entry.Method,
                entry.Route);
        }
    }

    /// <summary>
    /// V-C / W-1, closed mechanically: a permission is declared at the scope its catalogue row names.
    /// </summary>
    /// <remarks>
    /// A company-wide permission declared with <c>ProjectScope.FromRoute()</c> is granted by
    /// <c>PermissionEvaluator</c> before the project is looked at, and the gate then hands
    /// <c>ProjectAccessPath.None</c> to the audit context, which the check constraint turns into a
    /// 500 [Verified: 2026-08-24 @ <c>PermissionAuthorizationHandler.cs</c> -&gt;
    /// <c>HandleRequirementAsync</c>; @ <c>PermissionEvaluator.cs</c> -&gt; <c>Evaluate</c>]. The
    /// inverse — a project-scoped permission declared with no scope — is worse and is the same
    /// mismatch: the assignment half of "role × assignment" would never be evaluated. Both are
    /// unreachable while the two agree, and that is what this asserts.
    /// </remarks>
    [Fact]
    public void Every_permission_requirement_declares_the_scope_its_catalogue_row_names()
    {
        List<string> mismatched = [];

        foreach (MappedEndpoint mapped in ShippedEndpoints())
        {
            foreach (PermissionRequirement requirement in DeclaredPermissions(mapped))
            {
                PermissionScope row = PermissionCatalogue.Of(requirement.Permission).Scope;
                bool routeNamesAProject = requirement.Scope.Source != ProjectScopeSource.None;

                if (routeNamesAProject != (row == PermissionScope.ProjectScoped))
                {
                    mismatched.Add(
                        $"{mapped} declares {requirement.Permission} with project scope "
                        + $"{requirement.Scope.Source}, but its catalogue row is {row}");
                }
            }
        }

        mismatched.Should().BeEmpty(
            "an endpoint's declared scope and its catalogue row are two statements of the same fact, "
            + "and the gate believes both");
    }

    /// <summary>
    /// KAFF-114 <c>AC-114-F</c> — "revocation is not deletion" is a claim about the absence of an
    /// endpoint, and the only place that claim can be checked is against every route the host actually
    /// maps, the same source of truth <see cref="Every_mapped_endpoint_carries_a_permission_requirement"/>
    /// reads. A grep over <c>Endpoint.cs</c> files would only see what somebody wrote — the artefact
    /// D-067 shows is not trustworthy on its own.
    /// </summary>
    /// <remarks>
    /// Matches on the HTTP method and on the route containing <c>"assignments"</c> rather than on one
    /// exact path, so the assertion survives the revoke route's shape changing and still catches a
    /// delete-shaped endpoint added anywhere under <c>/api/projects/.../assignments</c> —
    /// <c>POST .../assignments</c> (KAFF-113) and <c>POST .../assignments/{id}/revoke</c> (KAFF-114)
    /// both pass today; a <c>DELETE</c> verb on either shape would fail this.
    /// </remarks>
    [Fact]
    public void No_endpoint_deletes_a_project_assignment()
    {
        List<string> deleteShaped = [.. ShippedEndpoints()
            .Where(mapped =>
                mapped.Route.Contains("assignments", StringComparison.Ordinal)
                && mapped.Method.Contains("DELETE", StringComparison.Ordinal))
            .Select(mapped => mapped.ToString())];

        deleteShaped.Should().BeEmpty(
            "CLAUDE.md forbids deleting a ProjectAssignment row — revocation (KAFF-114) is the only "
            + "way this codebase closes one, and it is a POST that stamps RevokedAt rather than a "
            + "DELETE that removes the row");
    }

    private static List<PermissionRequirement> DeclaredPermissions(MappedEndpoint mapped)
    {
        List<PermissionRequirement> requirements = [];

        foreach (IAuthorizeData data in mapped.Endpoint.Metadata.OfType<IAuthorizeData>())
        {
            if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement)
                && requirement is not null)
            {
                requirements.Add(requirement);
            }
        }

        return requirements;
    }

    private static bool IsAllowListed(MappedEndpoint mapped) =>
        AllowList.Any(entry =>
            string.Equals(entry.Method, mapped.Method, StringComparison.Ordinal)
            && string.Equals(entry.Route, mapped.Route, StringComparison.Ordinal));

    /// <summary>
    /// Named on the self-only list <b>and</b> carrying the checks that exemption owes.
    /// </summary>
    /// <remarks>
    /// <b>The second half is the point.</b> Being on the list is a claim; carrying
    /// <see cref="LiveSession.Marker"/> is the claim being paid for, and only
    /// <c>RequireLiveSession()</c> adds it. A route added to <see cref="SelfOnlyEndpoints"/> without it
    /// is not exempt here — it falls through to
    /// <see cref="Every_mapped_endpoint_carries_a_permission_requirement"/> as an ungated endpoint,
    /// which is what makes skipping the checks impossible to do quietly.
    /// </remarks>
    private static bool IsSelfOnlyListed(MappedEndpoint mapped) =>
        SelfOnlyEndpoints.Any(entry =>
            string.Equals(entry.Method, mapped.Method, StringComparison.Ordinal)
            && string.Equals(entry.Route, mapped.Route, StringComparison.Ordinal))
        && mapped.Endpoint.Metadata.GetMetadata<LiveSession.Marker>() is not null;

    private IEnumerable<MappedEndpoint> ShippedEndpoints()
    {
        Assembly shipped = typeof(PermissionRequirement).Assembly;

        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint route)
                {
                    continue;
                }

                Assembly? handler = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly;

                if (handler is not null && handler != shipped)
                {
                    continue;
                }

                HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

                yield return new MappedEndpoint(
                    methods is null ? "*" : string.Join('|', methods.HttpMethods),
                    route.RoutePattern.RawText ?? string.Empty,
                    endpoint);
            }
        }
    }

    /// <summary>One route the host built, and the metadata it carries.</summary>
    private sealed record MappedEndpoint(string Method, string Route, Endpoint Endpoint)
    {
        public override string ToString() => Method + " " + Route;
    }

    /// <summary>One deliberate exemption: what it is, and why it may be reached without a permission.</summary>
    private sealed record AnonymousEndpoint(string Method, string Route, string Reason);

    /// <summary>
    /// One deliberate exemption: an endpoint reachable by any authenticated caller, acting on nothing
    /// but their own row, with no catalogue permission to declare.
    /// </summary>
    /// <remarks>
    /// <b><see cref="Reason"/> says why the route is exempt. It does not say what the exemption owes,
    /// and it must not have to.</b> Two entries recorded their reason well and both shipped one check
    /// short of the gate they replaced (<c>V-26-B</c>) — prose a reviewer reads is the artefact D-067
    /// showed is not trustworthy. What each member owes is
    /// <c>LiveSession.RequireLiveSession()</c>, asserted from the route's own metadata by
    /// <see cref="Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own"/>
    /// and enforced by <see cref="IsSelfOnlyListed"/>.
    /// </remarks>
    private sealed record SelfOnlyEndpoint(string Method, string Route, string Reason);
}
