using System.Net;
using System.Net.Http;
using Kaff.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Tests;

/// <summary>
/// A malformed request body is the client's defect, and it answers <c>400</c> in every environment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on 2026-08-28 driving the sign-in screen.</b> A body with unquoted property names sent to
/// <c>POST /api/setup</c> produced <c>BadHttpRequestException</c> and came back as <c>500</c>. Probed
/// against the running API on 5080 before anything was changed: <c>/api/setup</c> and
/// <c>/api/auth/sign-in</c> both <c>500</c>, for four different malformed bodies each.
/// <b>It was never about <c>/api/setup</c></b> — it is every endpoint that binds a JSON body.
/// <c>POST /api/auth/change-password</c> was the only one that did not, and only because the fallback
/// policy refuses an unauthenticated caller before binding runs.
/// </para>
/// <para>
/// <b>Two independent causes, and the tests below are one per cause.</b>
/// <c>RouteHandlerOptions.ThrowOnBadRequest</c> defaults to <c>true</c> in Development and
/// <c>false</c> elsewhere, so a developer and a client saw different status codes for the same
/// request — and the client's typo was logged as <i>"An unhandled exception has occurred while
/// executing the request."</i> Separately, <c>UseExceptionHandler</c> had no
/// <c>StatusCodeSelector</c>, so any <see cref="BadHttpRequestException"/> from middleware this
/// application does not control — a body over the size limit, a malformed <c>Content-Length</c> —
/// was reported as a server fault too.
/// </para>
/// <para>
/// <b>The first test would pass without either fix, and that is why it is not the only one.</b> The
/// test host runs as <c>Testing</c>, where <c>ThrowOnBadRequest</c> was already <c>false</c>, so a
/// malformed body has always bound to a <c>400</c> here. That is exactly the vacuity <c>V-27-A</c> is
/// about: a suite satisfying a rule it never exercises. <see cref="BadRequestThrowRoute"/> is the one
/// that fails without the selector, and the options assertion is the one that fails if the explicit
/// setting is removed and the environment starts deciding again.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class MalformedRequestTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    public MalformedRequestTests(PostgresDatabase database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// A <see cref="BadHttpRequestException"/> keeps the status code it carries.
    /// </summary>
    /// <remarks>
    /// <b>This is the assertion that fails without the fix.</b> Remove the
    /// <c>StatusCodeSelector</c> from <c>Program</c> and this answers <c>500</c> — the defect exactly,
    /// reproduced through the pipeline rather than described.
    /// </remarks>
    [Fact]
    public async Task An_exception_carrying_a_client_error_status_is_answered_with_that_status()
    {
        HttpResponseMessage response = await _client.GetAsync(
            new Uri(ProbeEndpoint.BadRequestThrowRoute, UriKind.Relative),
            Ct);

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "BadHttpRequestException carries the status it means — 400 for an unreadable body, 413 for "
            + "one over the size limit. UseExceptionHandler calls every exception a 500 unless it is "
            + "given a selector, which reports a client's defect as a server fault and puts it in the "
            + "log as an unhandled exception");
    }

    /// <summary>
    /// A body that is not JSON is a <c>400</c>, and the environment does not get a vote.
    /// </summary>
    /// <remarks>
    /// The exact body the defect was found with — unquoted property names, which is what a hand-written
    /// fetch call produces. Against the running Development API this was a <c>500</c> on every endpoint
    /// that binds a body.
    /// </remarks>
    [Theory]
    [InlineData("{value: \"x\"}")]
    [InlineData("{\"value\":\"x\"}}}")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task A_malformed_json_body_is_refused_as_a_client_error(string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync(
            new Uri(ProbeEndpoint.BodyBindingRoute, UriKind.Relative),
            content,
            Ct);

        ((int)response.StatusCode).Should().BeInRange(
            400,
            499,
            "a request body the client got wrong is the client's defect. It must not be a 500, and it "
            + "must not depend on which environment answered");
    }

    /// <summary>
    /// <c>ThrowOnBadRequest</c> is set explicitly, so Development and production agree.
    /// </summary>
    /// <remarks>
    /// Its framework default is <c>true</c> in Development and <c>false</c> everywhere else — an
    /// environment-dependent status code for the same request, which is how a <c>500</c> reached the
    /// screen while Staging was answering <c>400</c>. Asserted from the built host's own options so
    /// that removing the line in <c>Program</c> fails here rather than only on somebody's machine.
    /// </remarks>
    [Fact]
    public void The_bad_request_behaviour_is_set_explicitly_rather_than_by_environment()
    {
        RouteHandlerOptions options = _factory.Services
            .GetRequiredService<IOptions<RouteHandlerOptions>>().Value;

        options.ThrowOnBadRequest.Should().BeFalse(
            "a malformed body must produce the same 400 in every environment, and a client's typo must "
            + "not be logged as an unhandled exception");
    }

    /// <summary>
    /// A malformed body against a real, shipped endpoint — not only the test host's probe route — is a
    /// client error.
    /// </summary>
    /// <remarks>
    /// <b>Closes half of <c>V-30-G</c>.</b> Every assertion above runs against
    /// <see cref="ProbeEndpoint.BodyBindingRoute"/>, which exists only in this test host. The Verifier
    /// found nothing in the suite exercised a shipped route and closed the gap by hand, driving
    /// <c>POST /api/auth/sign-in</c> live in Development: *"one case against
    /// <c>POST /api/auth/sign-in</c> would close it."* This is that case, so a regression — the fix
    /// deleted, or reintroduced some other way — fails a test instead of needing to be re-discovered by
    /// hand. <c>/api/auth/sign-in</c> is <c>AllowAnonymous</c> (KAFF-101a), so the request reaches
    /// binding before any authorization gate could intercept it, exactly like
    /// <see cref="ProbeEndpoint.BodyBindingRoute"/> does.
    /// </remarks>
    [Theory]
    [InlineData("{value: \"x\"}")]
    [InlineData("{\"userName\":\"x\"}}}")]
    [InlineData("[]")]
    public async Task A_malformed_json_body_on_the_shipped_sign_in_route_is_refused_as_a_client_error(string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync(new Uri("/api/auth/sign-in", UriKind.Relative), content, Ct);

        ((int)response.StatusCode).Should().BeInRange(
            400,
            499,
            "the shipped sign-in route binds a JSON body exactly as the test-host probe does, and a "
            + "malformed one must not depend on which route happened to be tested");
    }

    /// <summary>
    /// The fix holds when the host itself runs as <c>Development</c> — not only in the <c>Testing</c>
    /// environment every other assertion in this file uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Answers meetings/2026-09-01-sprint-2-refinement.md §2.3 item 1</b>, tried rather than reasoned
    /// about: the Api test host <i>can</i> run as <c>Development</c> without tripping <c>Program</c>'s
    /// start-up guard refusal, because that refusal is conditioned on
    /// <c>!app.Environment.IsDevelopment()</c> — Development is the one environment the refusal never
    /// fires in, regardless of guard state. Building this factory with
    /// <c>environment: "Development"</c> and reaching this assertion is the proof; if the guard check
    /// ever changed to also refuse in Development, a missing or misconfigured guard on this database
    /// would throw during <see cref="InitializeAsync"/> instead.
    /// </para>
    /// <para>
    /// <b>And it closes the other half of <c>V-30-G</c>.</b> Before <c>45a939d</c>,
    /// <c>RouteHandlerOptions.ThrowOnBadRequest</c> defaulted to <c>true</c> in Development, which is
    /// exactly where the original defect — a <c>500</c> where <c>Staging</c> answered <c>400</c> — was
    /// found. Every other test in this file runs where the framework default already agreed with the
    /// fix, so none of them would notice the fix being deleted <i>and</i> the environment reverting to
    /// deciding. This one runs where the two used to disagree.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_malformed_json_body_is_refused_as_a_client_error_when_the_host_runs_as_development()
    {
        await using var factory = new KaffApiFactory(_database.ConnectionString, environment: "Development");
        using HttpClient client = factory.CreateClient();

        using var content = new StringContent("{value: \"x\"}", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(
            new Uri(ProbeEndpoint.BodyBindingRoute, UriKind.Relative),
            content,
            Ct);

        ((int)response.StatusCode).Should().BeInRange(
            400,
            499,
            "the Development host boots (proving the guard refusal is not tripped by this environment) "
            + "and Configure<RouteHandlerOptions> applies regardless of environment, so a malformed "
            + "body must not become a 500 here either");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
