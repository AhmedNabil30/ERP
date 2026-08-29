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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
