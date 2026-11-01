using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs/boq-lines</c> — the money-free list of BOQ lines a Site Engineer
/// records a quantity against (D-191). Gated <c>DailyLogWrite</c> with the route's project: role and assignment.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ListBoqLinesTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _engineerOnA;
    private Guid _engineerUnassigned;
    private Guid _hr;

    public ListBoqLinesTests(PostgresDatabase database) => _database = database;

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
    public async Task The_assigned_engineer_reads_the_lines_with_their_three_quantities_and_no_money()
    {
        HttpResponseMessage response = await GetAsync(_projectA, _engineerOnA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync(Ct);
        using JsonDocument body = JsonDocument.Parse(json);
        JsonElement line = body.RootElement.GetProperty("lines").EnumerateArray().Single();

        line.GetProperty("position").GetInt32().Should().Be(1);
        Quantity(line, "quantityEstimated").Should().Be(12m);
        Quantity(line, "quantitySurveyed").Should().Be(10m);
        Quantity(line, "quantityExecuted").Should().Be(0m);

        Regex.IsMatch(json, "cost|rate|price|amount|total|markup", RegexOptions.IgnoreCase)
            .Should().BeFalse("a site-facing read carries no money (spec.md section 4.2)");
    }

    [Fact]
    public async Task An_engineer_without_an_assignment_is_refused()
        => (await GetAsync(_projectA, _engineerUnassigned)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    [Fact]
    public async Task An_assignment_to_another_project_does_not_carry_over()
        => (await GetAsync(_projectB, _engineerOnA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    [Fact]
    public async Task A_role_without_the_permission_is_refused()
        => (await GetAsync(_projectA, _hr)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    [Fact]
    public async Task An_anonymous_call_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RouteFor(_projectA));

        (await _client.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Reflection, so the rule keeps holding when someone adds a field later: no member of the response, at any
    /// depth, is named for money or typed as <see cref="Money"/> or <see cref="Percentage"/>.
    /// </summary>
    [Fact]
    public void The_response_type_has_no_money_member()
    {
        var offenders = new List<string>();
        Walk(typeof(Kaff.Api.Features.DailyLogs.ListBoqLines.Response), offenders, []);

        offenders.Should().BeEmpty();
    }

    private static void Walk(Type type, List<string> offenders, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties())
        {
            Type inner = property.PropertyType.IsGenericType
                ? property.PropertyType.GetGenericArguments()[0]
                : property.PropertyType;
            inner = Nullable.GetUnderlyingType(inner) ?? inner;

            if (inner == typeof(Money) || inner == typeof(Percentage)
                || Regex.IsMatch(property.Name, "cost|rate|price|amount|total|markup", RegexOptions.IgnoreCase))
            {
                offenders.Add($"{type.Name}.{property.Name}");
            }

            if (inner.Namespace?.StartsWith("Kaff.Api", StringComparison.Ordinal) == true)
            {
                Walk(inner, offenders, seen);
            }
        }
    }

    // The API writes decimals as JSON strings, so read either form.
    private static decimal Quantity(JsonElement line, string name)
    {
        JsonElement value = line.GetProperty(name);

        return value.ValueKind == JsonValueKind.String
            ? decimal.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : value.GetDecimal();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static Uri RouteFor(Guid projectId)
        => new($"/api/projects/{projectId}/daily-logs/boq-lines", UriKind.Relative);

    private async Task<HttpResponseMessage> GetAsync(Guid projectId, Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RouteFor(projectId));

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("BL-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Project projectA = Project.Create(
            UniqueNames.Code("BL-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("BL-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        User engineerOnA = User.Create(
            UniqueNames.Code("bl-eng-a"), "bl-eng-a", UniqueNames.Phone(), Role.SiteEngineer, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        User engineerUnassigned = User.Create(
            UniqueNames.Code("bl-eng-un"), "bl-eng-un", UniqueNames.Phone(), Role.SiteEngineer, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        User hr = User.Create(
            UniqueNames.Code("bl-hr"), "bl-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;

        Bab bab = Bab.Create(UniqueNames.Code("BL-BAB"), "باب", "Bab", Percentage.FromPercent(11m)).Value;
        Boq boq = Boq.Open(projectA, Now).Value;
        boq.AddCustomLine(
            "X-1", "بند", null, "m2", bab, Money.From(100m), Money.From(150m),
            quantityEstimated: 12m, quantitySurveyed: 10m).IsSuccess.Should().BeTrue();

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(engineerOnA, engineerUnassigned, hr);
        context.Babs.Add(bab);
        context.Boqs.Add(boq);
        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, engineerOnA, AssignmentLevel.Junior, Guid.CreateVersion7(), Now).Value);
        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _engineerOnA = engineerOnA.Id;
        _engineerUnassigned = engineerUnassigned.Id;
        _hr = hr.Id;
    }
}
