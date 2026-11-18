using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Npgsql;

namespace Kaff.Api.Tests;

/// <summary>
/// The application's own database role cannot switch the guards off, and the host refuses to start on one that can
/// (qa/slice-3/verification-2026-11-18.md F-1).
/// </summary>
/// <remarks>
/// Every statement here runs on <see cref="PostgresDatabase.ConnectionString"/>, which is the role the running
/// application connects as. The append-only triggers already refuse a plain <c>UPDATE</c>; what these tests pin is
/// that the role cannot get past them by disabling them, and that the refusal comes from the privilege system
/// (<c>42501</c>) before any trigger is reached.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class LeastPrivilegeTests
{
    private const string InsufficientPrivilege = "42501";

    private readonly PostgresDatabase _database;

    public LeastPrivilegeTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_application_role_cannot_switch_triggers_off_for_its_session()
        => await RefusedByPrivilegeAsync("SET session_replication_role = replica");

    [Theory]
    [InlineData("postings")]
    [InlineData("audit_records")]
    public async Task The_application_role_cannot_disable_the_append_only_triggers(string table)
        => await RefusedByPrivilegeAsync($"ALTER TABLE {table} DISABLE TRIGGER ALL");

    /// <summary>
    /// <c>WHERE false</c> touches no row, so no row trigger can fire: a refusal here is the grant, not the trigger.
    /// </summary>
    [Theory]
    [InlineData("UPDATE postings SET amount = amount WHERE false")]
    [InlineData("DELETE FROM postings WHERE false")]
    [InlineData("TRUNCATE postings")]
    [InlineData("UPDATE audit_records SET action = action WHERE false")]
    [InlineData("DELETE FROM audit_records WHERE false")]
    [InlineData("TRUNCATE audit_records")]
    public async Task The_application_role_has_no_privilege_to_change_or_remove_postings_or_audit_records(string sql)
        => await RefusedByPrivilegeAsync(sql);

    [Theory]
    [InlineData("postings")]
    [InlineData("audit_records")]
    public async Task The_application_role_cannot_create_a_trigger_on_the_append_only_tables(string table)
        => await RefusedByPrivilegeAsync(
            $"CREATE TRIGGER trg_probe BEFORE INSERT ON {table} FOR EACH ROW EXECUTE FUNCTION kaff_postings_validate()");

    [Fact]
    public async Task Health_reports_the_application_connection_as_least_privilege()
    {
        await using var factory = new KaffApiFactory(_database);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        string body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("leastPrivilege").GetBoolean().Should().BeTrue(body);
        json.RootElement.GetProperty("excessPrivileges").GetArrayLength().Should().Be(0, body);
    }

    /// <summary>D-033's refusal, for privilege: a superuser application connection does not boot, in any environment.</summary>
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public void The_host_refuses_to_start_when_the_application_connects_as_the_owner(string environment)
    {
        Action start = () =>
        {
            using var factory = new KaffApiFactory(
                _database.AdminConnectionString, _database.AdminConnectionString, environment: environment);
        };

        start.Should().Throw<Exception>()
            .Where(exception => Flatten(exception).Contains("least privilege", StringComparison.Ordinal));
    }

    [Fact]
    public void The_host_refuses_to_start_without_an_owner_connection()
    {
        Action start = () =>
        {
            using var factory = new KaffApiFactory(_database.ConnectionString, ownerConnectionString: null);
        };

        start.Should().Throw<Exception>()
            .Where(exception => Flatten(exception).Contains("KaffDatabaseOwner", StringComparison.Ordinal));
    }

    private async Task RefusedByPrivilegeAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);

        Func<Task> run = () => command.ExecuteNonQueryAsync(Ct);

        (await run.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(InsufficientPrivilege, sql);
    }

    private static string Flatten(Exception exception)
    {
        List<string> messages = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
