using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kaff.Api.Common;
using Kaff.Api.Features.Catalogue.ImportCatalogue;
using Kaff.Api.Features.Catalogue.ReimportCatalogue;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-201 — <c>POST /api/catalogue-items/import/preview</c> and
/// <c>POST /api/catalogue-items/import/confirm</c>.
/// </summary>
/// <remarks>
/// Every uploaded file is built by <c>TestWorkbook</c>, the same discipline <c>ImportCatalogueTests</c>
/// uses and for the same reason: no checked-in binary duplicates <see cref="CatalogueTemplate"/>'s
/// column list.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ReimportCatalogueTests : IAsyncLifetime
{
    private const string PreviewRoute = "/api/catalogue-items/import/preview";
    private const string ConfirmRoute = "/api/catalogue-items/import/confirm";

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _technicalOffice;
    private string _babCode = null!;

    public ReimportCatalogueTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-201-B · a second import states what it will do before it does it --------------------

    [Fact]
    public async Task Preview_names_the_counts_and_changes_nothing()
    {
        string existingCode = UniqueNames.Code("REI-B-EXIST");
        await SeedItemAsync(existingCode, cost: 100m, sell: 150m);

        string newCode = UniqueNames.Code("REI-B-NEW");

        byte[] file = BuildFile(
            [
                Row(newCode, "خرسانة جديدة", null, "م٣", _babCode, "10", "20"),
                Row(existingCode, "خرسانة معاد تسعيرها", null, "م٣", _babCode, "200", "300"),
            ]);

        HttpResponseMessage response = await PostFileAsync(PreviewRoute, file);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        PreviewResponse body = await ReadAsync<PreviewResponse>(response);

        body.WillCreateCount.Should().Be(1);
        body.WillAffectCount.Should().Be(1);
        body.Creates.Should().ContainSingle(c => c.Code == newCode);
        body.Reprices.Should().ContainSingle(
            r => r.Code == existingCode && r.OldCostPrice == 100m && r.OldBaseSellRate == 150m
                 && r.NewCostPrice == 200m && r.NewBaseSellRate == 300m);

        // Nothing changed at the moment the preview is shown — AC-201-B.
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.AnyAsync(i => i.Code == newCode, Ct)).Should().BeFalse(
            "a preview names what would happen; it must not have happened yet");

        CatalogueItem stillOld = await reader.CatalogueItems.SingleAsync(i => i.Code == existingCode, Ct);
        stillOld.CostPrice.Amount.Should().Be(100.0000m);
        stillOld.BaseSellRate.Amount.Should().Be(150.0000m);
    }

    [Fact]
    public async Task Not_confirming_leaves_the_catalogue_byte_for_byte_as_it_was()
    {
        string existingCode = UniqueNames.Code("REI-CANCEL");
        await SeedItemAsync(existingCode, cost: 11m, sell: 22m);

        byte[] file = BuildFile([Row(existingCode, "وصف", null, "م٣", _babCode, "999", "999")]);

        (await PostFileAsync(PreviewRoute, file)).StatusCode.Should().Be(HttpStatusCode.OK);

        // "Cancelling" is simply never calling /confirm — there is no server-side state to roll back.
        await using KaffDbContext reader = _database.CreateBareContext();
        CatalogueItem item = await reader.CatalogueItems.SingleAsync(i => i.Code == existingCode, Ct);
        item.CostPrice.Amount.Should().Be(11.0000m);
        item.BaseSellRate.Amount.Should().Be(22.0000m);
    }

    // ---- AC-201-F · a second import creates, re-prices, and only after confirmation ---------------

    [Fact]
    public async Task Confirm_creates_new_codes_and_reprices_existing_ones_and_leaves_every_other_item_untouched()
    {
        string untouchedCode = UniqueNames.Code("REI-F-UNTOUCHED");
        await SeedItemAsync(untouchedCode, cost: 5m, sell: 9m);

        string repriceCodeA = UniqueNames.Code("REI-F-REP-A");
        string repriceCodeB = UniqueNames.Code("REI-F-REP-B");
        await SeedItemAsync(repriceCodeA, cost: 10m, sell: 20m);
        await SeedItemAsync(repriceCodeB, cost: 30m, sell: 40m);

        string newCodeA = UniqueNames.Code("REI-F-NEW-A");
        string newCodeB = UniqueNames.Code("REI-F-NEW-B");
        string newCodeC = UniqueNames.Code("REI-F-NEW-C");

        byte[] file = BuildFile(
            [
                Row(newCodeA, "جديد أ", null, "م٣", _babCode, "1", "2"),
                Row(newCodeB, "جديد ب", null, "م٣", _babCode, "3", "4"),
                Row(newCodeC, "جديد ج", null, "م٣", _babCode, "5", "6"),
                Row(repriceCodeA, "أعيد تسعيره", null, "م٣", _babCode, "100", "200"),
                Row(repriceCodeB, "أعيد تسعيره", null, "م٣", _babCode, "300", "400"),
            ]);

        // The preview must name exactly these five changes before anything is confirmed.
        PreviewResponse preview = await ReadAsync<PreviewResponse>(await PostFileAsync(PreviewRoute, file));
        preview.WillCreateCount.Should().Be(3);
        preview.WillAffectCount.Should().Be(2);

        HttpResponseMessage confirmResponse = await PostFileAsync(ConfirmRoute, file);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        ConfirmResponse confirmed = await ReadAsync<ConfirmResponse>(confirmResponse);
        confirmed.CreatedCount.Should().Be(3);
        confirmed.RepricedCount.Should().Be(2);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.CatalogueItems.CountAsync(
            i => i.Code == newCodeA || i.Code == newCodeB || i.Code == newCodeC, Ct)).Should().Be(3);

        CatalogueItem repricedA = await reader.CatalogueItems.SingleAsync(i => i.Code == repriceCodeA, Ct);
        repricedA.CostPrice.Amount.Should().Be(100.0000m);
        repricedA.BaseSellRate.Amount.Should().Be(200.0000m);

        CatalogueItem repricedB = await reader.CatalogueItems.SingleAsync(i => i.Code == repriceCodeB, Ct);
        repricedB.CostPrice.Amount.Should().Be(300.0000m);
        repricedB.BaseSellRate.Amount.Should().Be(400.0000m);

        CatalogueItem untouched = await reader.CatalogueItems.SingleAsync(i => i.Code == untouchedCode, Ct);
        untouched.CostPrice.Amount.Should().Be(5.0000m, "every other item is untouched by this import");
        untouched.BaseSellRate.Amount.Should().Be(9.0000m);
    }

    // ---- AC-201-E · the second import is audited with before and after ----------------------------

    [Fact]
    public async Task Confirm_writes_a_before_and_after_audit_record_for_every_repriced_item()
    {
        string code = UniqueNames.Code("REI-E");
        await SeedItemAsync(code, cost: 7m, sell: 8m);

        byte[] file = BuildFile([Row(code, "وصف", null, "م٣", _babCode, "70", "80")]);

        string fileName = UniqueNames.Code("second") + ".xlsx";
        (await PostFileAsync(ConfirmRoute, file, fileName)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        CatalogueItem item = await reader.CatalogueItems.SingleAsync(i => i.Code == code, Ct);

        AuditRecord priceChange = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(CatalogueItem) && r.EntityId == item.Id && r.Action == AuditAction.Modified,
            Ct);

        priceChange.ActorUserId.Should().Be(_technicalOffice);
        priceChange.BeforeJson.Should().NotBeNull().And.Contain("7");
        priceChange.AfterJson.Should().NotBeNull().And.Contain("70");
        priceChange.ChangedProperties.Should().Contain(nameof(CatalogueItem.CostPrice))
            .And.Contain(nameof(CatalogueItem.BaseSellRate));

        AuditRecord importEvent = await reader.AuditRecords.SingleAsync(
            r => r.EventType == AuditEventKind.CatalogueImported && r.Reason != null && r.Reason.Contains(fileName),
            Ct);

        importEvent.ActorUserId.Should().Be(_technicalOffice);
        importEvent.Reason.Should().Contain("0 item(s) created").And.Contain("1 item(s) re-priced");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static IReadOnlyList<string?> Row(
        string code, string descriptionAr, string? descriptionEn, string unit, string bab, string cost, string sell)
        => [code, descriptionAr, descriptionEn, unit, bab, cost, sell];

    private static byte[] BuildFile(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(TestWorkbook.Row(1, HeaderCellsXml()));

        int rowNumber = 2;

        foreach (IReadOnlyList<string?> row in rows)
        {
            var cells = new StringBuilder();

            for (int i = 0; i < CatalogueTemplate.Columns.Count && i < row.Count; i++)
            {
                if (row[i] is { } value)
                {
                    cells.Append(TestWorkbook.InlineText(i, rowNumber, value));
                }
            }

            sb.Append(TestWorkbook.Row(rowNumber, cells.ToString()));
            rowNumber++;
        }

        return TestWorkbook.Build(sb.ToString());
    }

    private static string HeaderCellsXml()
    {
        var sb = new StringBuilder();

        for (int i = 0; i < CatalogueTemplate.Columns.Count; i++)
        {
            sb.Append(TestWorkbook.InlineText(i, 1, CatalogueTemplate.Columns[i]));
        }

        return sb.ToString();
    }

    private async Task<HttpResponseMessage> PostFileAsync(string route, byte[] file, string fileName = "catalogue.xlsx")
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(XlsxTemplateWriter.ContentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = content };
        await StampAsync(request);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(HttpRequestMessage request)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, _technicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.TechnicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(_technicalOffice));
        request.Headers.Add(TestAuthHandler.DepartmentHeader, WellKnownDepartments.OperationsId.ToString());
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync(Ct);

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task SeedItemAsync(string code, decimal cost, decimal sell)
    {
        await using KaffDbContext context = _database.CreateContext();

        CatalogueItem item = CatalogueItem.Create(
            code, "وصف أصلي", "م٣", (await context.Babs.SingleAsync(b => b.Code == _babCode, Ct)).Id,
            Money.From(cost), Money.From(sell)).Value;

        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        _babCode = UniqueNames.Code("REI-BAB");
        Bab bab = Bab.Create(_babCode, "باب", "Bab", Percentage.FromPercent(15m)).Value;

        User technicalOffice = User.Create(
            UniqueNames.Code("rei-tech"), "rei-tech", UniqueNames.Phone(), Role.TechnicalOffice, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical, null).Value;

        context.Babs.Add(bab);
        context.Users.Add(technicalOffice);

        await context.SaveChangesAsync(Ct);

        _technicalOffice = technicalOffice.Id;
    }

    private static DateTimeOffset Now => new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
