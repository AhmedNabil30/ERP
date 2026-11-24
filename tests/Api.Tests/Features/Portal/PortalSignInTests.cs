using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Portal;

/// <summary>
/// R5-A and KAFF-810 against the shipped JWT scheme: the client's door, the forced password change, the two session kinds refused at each
/// other's doors, and the portal host answering only under /api/portal. Cookies are carried by hand, as <c>SignInTests</c> does.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PortalSignInTests : IAsyncLifetime
{
    private const string Password = "client-password-1";
    private const string Temporary = "temporary-pass-1";
    private const string NewPassword = "a-brand-new-password";
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private string _clientName = null!;
    private string _freshClientName = null!;
    private string _otherClientName = null!;
    private string _ownerName = null!;
    private string _subcontractorName = null!;
    private Guid _project;

    public PortalSignInTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client own = Client.Create(UniqueNames.Code("PS-C1"), "عميل الدخول", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Client other = Client.Create(UniqueNames.Code("PS-C2"), "عميل آخر", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Project project = Project.Create(UniqueNames.Code("PS-P1"), "مشروع الدخول", own.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        User client = User.Create(UniqueNames.Code("ps-client"), "عميل", UniqueNames.Phone(), Role.Client, Now, clientId: own.Id).Value;
        client.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User fresh = User.Create(UniqueNames.Code("ps-fresh"), "عميل جديد", UniqueNames.Phone(), Role.Client, Now, clientId: own.Id).Value;
        fresh.SetTemporaryPassword(PasswordHasher.Hash(Temporary)).IsSuccess.Should().BeTrue();

        User otherClient = User.Create(UniqueNames.Code("ps-other"), "عميل آخر", UniqueNames.Phone(), Role.Client, Now, clientId: other.Id).Value;
        otherClient.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User owner = User.Create(UniqueNames.Code("ps-owner"), "مالك", UniqueNames.Phone(), Role.Owner, Now).Value;
        owner.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User subcontractor = User.Create(UniqueNames.Code("ps-sub"), "مقاول", UniqueNames.Phone(), Role.Subcontractor, Now).Value;

        context.Clients.AddRange(own, other);
        context.Projects.Add(project);
        context.Users.AddRange(client, fresh, otherClient, owner, subcontractor);
        await context.SaveChangesAsync(Ct);

        _project = project.Id;
        _clientName = client.UserName;
        _freshClientName = fresh.UserName;
        _otherClientName = otherClient.UserName;
        _ownerName = owner.UserName;
        _subcontractorName = subcontractor.UserName;

        _factory = new KaffApiFactory(_database, useRealAuthentication: true);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task A_client_signs_in_at_the_portal_door_and_reads_his_own_projects_with_the_cookie_alone()
    {
        HttpResponseMessage signIn = await SignInAsync(_clientName, Password);

        signIn.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await signIn.Content.ReadAsStringAsync(Ct)).Should().BeEmpty("the body carries no token");
        string cookie = Cookie(signIn);

        HttpResponseMessage list = await GetAsync("/api/portal/projects", cookie);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync(Ct)).Should().Contain(_project.ToString());

        (await GetAsync($"/api/portal/projects/{_project}", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_portal_cookie_opens_no_staff_route()
    {
        string cookie = Cookie(await SignInAsync(_clientName, Password));

        foreach (string route in new[] { "/api/projects", $"/api/projects/{_project}", "/api/clients", "/api/users", "/api/auth/me" })
        {
            (await GetAsync(route, cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden, route);
        }
    }

    [Fact]
    public async Task A_staff_session_opens_no_portal_route()
    {
        HttpResponseMessage staff = await _client.PostAsJsonAsync("/api/auth/sign-in", new { userName = _ownerName, password = Password }, Ct);
        staff.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string cookie = Cookie(staff);

        (await GetAsync("/api/portal/projects", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync($"/api/portal/projects/{_project}", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_staff_account_a_subcontractor_and_a_wrong_password_all_get_the_same_refusal_at_the_portal_door()
    {
        HttpResponseMessage wrong = await SignInAsync(_clientName, "not-the-password");
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string expected = WithoutTraceId(await wrong.Content.ReadAsStringAsync(Ct));

        foreach (string name in new[] { _ownerName, _subcontractorName, "nobody-by-this-name" })
        {
            HttpResponseMessage refused = await SignInAsync(name, Password);
            refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            refused.Headers.Should().NotContain(header => header.Key == "Set-Cookie");
            WithoutTraceId(await refused.Content.ReadAsStringAsync(Ct)).Should().Be(expected);
        }
    }

    [Fact]
    public async Task A_client_credential_at_the_staff_door_is_refused()
    {
        HttpResponseMessage refused = await _client.PostAsJsonAsync("/api/auth/sign-in", new { userName = _clientName, password = Password }, Ct);

        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        refused.Headers.Should().NotContain(header => header.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_a_client_as_they_lock_staff()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            (await SignInAsync(_otherClientName, "wrong-password")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await SignInAsync(_otherClientName, "wrong-password")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        HttpResponseMessage locked = await SignInAsync(_otherClientName, Password);
        locked.StatusCode.Should().Be((HttpStatusCode)423);
        locked.Headers.Should().NotContain(header => header.Key == "Set-Cookie");
    }

    [Fact]
    public async Task A_first_sign_in_succeeds_and_reads_are_refused_until_the_client_sets_his_own_password()
    {
        HttpResponseMessage signIn = await SignInAsync(_freshClientName, Temporary);
        signIn.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string cookie = Cookie(signIn);

        HttpResponseMessage refused = await GetAsync("/api/portal/projects", cookie);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.password_change_required");
        (await GetAsync($"/api/portal/projects/{_project}", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage wrongCurrent = await PostWithCookieAsync(
            "/api/portal/auth/change-password", cookie, new { currentPassword = "not-it", newPassword = NewPassword });
        wrongCurrent.StatusCode.Should().NotBe(HttpStatusCode.NoContent);

        HttpResponseMessage changed = await PostWithCookieAsync(
            "/api/portal/auth/change-password", cookie, new { currentPassword = Temporary, newPassword = NewPassword });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string fresh = Cookie(changed);

        (await GetAsync("/api/portal/projects", fresh)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync("/api/portal/projects", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the old session ended with the stamp");
        (await SignInAsync(_freshClientName, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_sign_in_and_the_password_change_are_audited_with_the_client_as_actor()
    {
        string cookie = Cookie(await SignInAsync(_freshClientName, Temporary));
        (await PostWithCookieAsync("/api/portal/auth/change-password", cookie, new { currentPassword = Temporary, newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        Guid userId = await reader.Users.Where(user => user.UserName == _freshClientName).Select(user => user.Id).SingleAsync(Ct);
        List<AuditRecord> trail = await reader.Set<AuditRecord>().Where(record => record.EntityId == userId).ToListAsync(Ct);

        trail.Should().Contain(record => record.EventType == AuditEventKind.SignedIn && record.ActorUserId == userId);
        trail.Should().Contain(record => record.Action == AuditAction.Modified && record.ActorUserId == userId, "the password change");
    }

    [Fact]
    public async Task On_the_portal_host_only_the_portal_routes_answer()
    {
        using WebApplicationFactoryHost portal = new(_factory, "portal.kaff.test");
        HttpClient client = portal.Client;

        (await client.GetAsync("/api/health", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/projects", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/auth/sign-in", new { userName = _ownerName, password = Password }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "the staff door is not on the client's address");
        (await client.GetAsync("/index.html", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await client.PostAsJsonAsync("/api/portal/auth/sign-in", new { userName = _clientName, password = Password }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The staff host is unchanged.
        (await _client.GetAsync("/api/health", Ct)).StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SignInAsync(string userName, string password) =>
        _client.PostAsJsonAsync("/api/portal/auth/sign-in", new { userName, password }, Ct);

    private async Task<HttpResponseMessage> GetAsync(string route, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("Cookie", cookie);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> PostWithCookieAsync(string route, string cookie, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", cookie);
        return await _client.SendAsync(request, Ct);
    }

    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie").Last().Split(';')[0];

    private static string WithoutTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"\"");

    /// <summary>The same application, told which host name is the portal's, and a client that arrives on that name.</summary>
    private sealed class WebApplicationFactoryHost : IDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _derived;

        public WebApplicationFactoryHost(KaffApiFactory factory, string host)
        {
            _derived = factory.WithWebHostBuilder(builder => builder.UseSetting("Kaff:PortalHost", host));
            Client = _derived.CreateClient();
            Client.DefaultRequestHeaders.Host = host;
        }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _derived.Dispose();
        }
    }
}
