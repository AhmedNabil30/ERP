using Kaff.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-201 <c>AC-201-A</c> — the catalogue never tracks a spreadsheet. spec.md §4.1: "Excel import
/// is not an ongoing sync." Rule 1: "no scheduled import, no watched folder, no 're-sync' control, and
/// no background job that reads a file."
/// </summary>
/// <remarks>
/// <para>
/// <b>Absence assertions need a positive control or they are vacuous</b> — decisions.md D-116. A test
/// asserting "no route matches sync/watch" against an application with no routes at all would pass for
/// the wrong reason. <see cref="No_sync_or_watch_shaped_route_is_mapped"/> is paired with
/// <see cref="ImportCatalogueTests"/> and <see cref="ReimportCatalogueTests"/>, which prove
/// human-triggered import and re-import routes exist and change the catalogue — so this filter is
/// known to be looking at a non-empty, real route table.
/// </para>
/// <para>
/// <b>Seen red.</b> Both assertions below were verified to fail — a dummy <c>IHostedService</c> and a
/// dummy <c>POST /api/catalogue-items/sync</c> route were added temporarily, each test went red against
/// it, and both were then removed. The catalogue import feature registers neither, today.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class CatalogueImportHasNoSyncTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;

    public CatalogueImportHasNoSyncTests(PostgresDatabase database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new KaffApiFactory(_database.ConnectionString);

        // Reading Services starts the host, which is what runs Program and maps the routes.
        _ = _factory.Services;

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void No_hosted_service_is_registered_for_the_catalogue()
    {
        IEnumerable<IHostedService> hostedServices = _factory.Services.GetServices<IHostedService>();

        List<string> names = [.. hostedServices.Select(service => service.GetType().FullName ?? service.GetType().Name)];

        names.Should().NotContain(
            name => name.Contains("Catalogue", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Sync", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Watch", StringComparison.OrdinalIgnoreCase),
            "the catalogue is loaded and re-loaded only by a human-triggered request (spec.md §4.1); "
            + "a hosted service naming the catalogue, a sync or a watcher would be exactly the "
            + "background re-read rule 1 forbids");
    }

    [Fact]
    public void No_sync_or_watch_shaped_route_is_mapped()
    {
        List<string> suspectRoutes = [];

        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint route)
                {
                    continue;
                }

                string path = route.RoutePattern.RawText ?? string.Empty;

                if (path.Contains("sync", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("watch", StringComparison.OrdinalIgnoreCase))
                {
                    suspectRoutes.Add(path);
                }
            }
        }

        suspectRoutes.Should().BeEmpty(
            "a second import is a deliberate, reviewed act (KAFF-201's own title) — there is no "
            + "'re-sync' control and nothing named sync or watch anywhere in the route table");

        // Positive control: the routes this filter would have to see and correctly ignore. If either
        // import route stopped being mapped, this would go green for the wrong reason.
        List<string> allRoutes = [.. _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(route => route.RoutePattern.RawText ?? string.Empty)];

        allRoutes.Should().Contain("/api/catalogue-items/import");
        allRoutes.Should().Contain("/api/catalogue-items/import/preview");
        allRoutes.Should().Contain("/api/catalogue-items/import/confirm");
    }
}
