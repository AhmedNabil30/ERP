using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kaff.Api.Common;
using Kaff.Api.Features.Catalogue.ImportCatalogue;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-200 — <c>POST /api/catalogue-items/import</c> and <c>GET /api/catalogue-items/import-template</c>.
/// </summary>
/// <remarks>
/// <c>XlsxSheetReaderTests</c> pins the format-plumbing edge cases (D-136's own list); this file
/// covers the story's acceptance criteria end to end, through real HTTP, against a real database.
/// Every uploaded file is built by <c>TestWorkbook</c> rather than checked in as a binary — decisions.md
/// D-136 rejected a checked-in template for the same reason a test fixture would be worse: a second
/// copy of the column list that only <see cref="CatalogueTemplate"/> should own.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ImportCatalogueTests : IAsyncLifetime
{
    private const string ImportRoute = "/api/catalogue-items/import";
    private const string TemplateRoute = "/api/catalogue-items/import-template";

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _hr;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _marketing;
    private string _babCode = null!;
    private Guid _babId;

    public ImportCatalogueTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-200-A · a clean file becomes a catalogue --------------------------------------------

    [Fact]
    public async Task A_clean_file_becomes_a_catalogue_with_both_descriptions_and_is_active()
    {
        string code = UniqueNames.Code("IMP-A");

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [
                Row(code, "خرسانة عادية", "Plain concrete", "م٣", _babCode, "100.0000", "150.0000"),
            ]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Response body = await ReadResponseAsync(response);
        body.CreatedCount.Should().Be(1);
        body.Failures.Should().BeEmpty();

        await using KaffDbContext reader = _database.CreateBareContext();
        CatalogueItem item = await reader.CatalogueItems.SingleAsync(i => i.Code == code, Ct);

        item.DescriptionAr.Should().Be("خرسانة عادية");
        item.DescriptionEn.Should().Be("Plain concrete");
        item.Unit.Should().Be("م٣");
        item.BabId.Should().Be(_babId);
        item.CostPrice.Amount.Should().Be(100.0000m);
        item.BaseSellRate.Amount.Should().Be(150.0000m);
        item.Status.Should().Be(CatalogueItemStatus.Active);
    }

    [Fact]
    public async Task The_owner_may_also_import()
    {
        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [Row(UniqueNames.Code("IMP-OWNER"), "خرسانة", null, "م٣", _babCode, "10", "20")]);

        (await ImportAsync(_owner, Role.Owner, file)).StatusCode.Should().Be(
            HttpStatusCode.OK, "D-129 §1 — the Owner keeps every company-wide grant, CatalogueManage included");
    }

    // ---- AC-200-B · four decimals survive, five decimals store through Money, no refusal --------

    [Fact]
    public async Task Four_decimals_survive_and_five_decimals_store_through_money_without_refusal()
    {
        string codeA = UniqueNames.Code("IMP-B1");
        string codeB = UniqueNames.Code("IMP-B2");

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [
                Row(codeA, "خرسانة", null, "م٣", _babCode, "987.6543", "1234.5678"),
                Row(codeB, "خرسانة", null, "م٣", _babCode, "10", "1234.56785"),
            ]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        Response body = await ReadResponseAsync(response);

        body.Failures.Should().BeEmpty("D-144 §6 — a rate beyond four decimals is stored through Money, not refused");
        body.CreatedCount.Should().Be(2);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.CatalogueItems.SingleAsync(i => i.Code == codeA, Ct)).BaseSellRate.Amount
            .Should().Be(1234.5678m);

        (await reader.CatalogueItems.SingleAsync(i => i.Code == codeB, Ct)).BaseSellRate.Amount
            .Should().Be(1234.5679m, "AwayFromZero rounding of the fifth decimal, through Money, same as any other caller");
    }

    // ---- AC-200-C · an unknown باب is not invented ------------------------------------------------

    [Fact]
    public async Task An_unknown_bab_creates_no_item_and_no_bab_and_names_the_row()
    {
        string code = UniqueNames.Code("IMP-C");
        string unknownBab = "NOPE-" + UniqueNames.Code("X");

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [Row(code, "خرسانة", null, "م٣", unknownBab, "10", "20")]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        Response body = await ReadResponseAsync(response);

        body.CreatedCount.Should().Be(0);
        body.Failures.Should().ContainSingle(f => f.RowNumber == 2 && f.MessageKey == "errors.master.bab_not_found");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.AnyAsync(i => i.Code == code, Ct)).Should().BeFalse();
        (await reader.Babs.AnyAsync(b => b.Code == unknownBab, Ct)).Should().BeFalse("no باب is invented to let the row through");
    }

    // ---- AC-200-F · a role without CatalogueManage cannot import ---------------------------------

    [Fact]
    public async Task Roles_without_catalogue_manage_are_refused_and_create_nothing()
    {
        string code = UniqueNames.Code("IMP-F");

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [Row(code, "خرسانة", null, "م٣", _babCode, "10", "20")]);

        foreach ((Guid actor, Role role) in RefusedActors())
        {
            HttpResponseMessage response = await ImportAsync(actor, role, file);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} does not hold CatalogueManage", role);
        }

        // The database is shared across this whole test class (PostgresDatabase is a collection
        // fixture), so a bare row count would also count every other test's items. What this test
        // owns is whether ITS row's code ever made it in.
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.AnyAsync(i => i.Code == code, Ct)).Should().BeFalse(
            "every attempt above was refused before the handler ever ran");
    }

    [Fact]
    public async Task Roles_without_catalogue_manage_cannot_download_the_template_either()
    {
        foreach ((Guid actor, Role role) in RefusedActors())
        {
            HttpResponseMessage response = await GetTemplateAsync(actor, role);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} does not hold CatalogueManage", role);
        }
    }

    // ---- AC-200-G · the import is audited, a refused import writes nothing ----------------------

    [Fact]
    public async Task A_successful_import_writes_one_event_naming_the_file_and_the_count()
    {
        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [Row(UniqueNames.Code("IMP-G"), "خرسانة", null, "م٣", _babCode, "10", "20")]);

        string fileName = UniqueNames.Code("prices") + ".xlsx";

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file, fileName);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        // The database is shared across this whole test class, so more than one CatalogueImported
        // event can exist by the time this runs — this test's own file name is what it owns.
        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EventType == AuditEventKind.CatalogueImported && r.Reason != null && r.Reason.Contains(fileName),
            Ct);

        record.ActorUserId.Should().Be(_technicalOffice);
        record.ActorRole.Should().Be(Role.TechnicalOffice);
        record.Reason.Should().Contain(fileName).And.Contain("1 item(s) created");
    }

    [Fact]
    public async Task A_refused_import_writes_no_audit_record_and_no_item()
    {
        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(Ct);
        long itemsBefore = await before.CatalogueItems.LongCountAsync(Ct);

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [Row(UniqueNames.Code("IMP-G-REF"), "خرسانة", null, "م٣", _babCode, "10", "20")]);

        (await ImportAsync(_finance, Role.Finance, file)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(countBefore);
        (await after.CatalogueItems.LongCountAsync(Ct)).Should().Be(itemsBefore);
    }

    // ---- AC-200-H · a bad row does not sink the good ones -----------------------------------------

    [Fact]
    public async Task A_bad_row_does_not_sink_the_good_ones_missing_price()
    {
        string[] codes = [.. Enumerable.Range(0, 200).Select(i => UniqueNames.Code($"IMP-H1-{i}"))];
        var rows = new List<IReadOnlyList<string?>>();

        for (int i = 0; i < 200; i++)
        {
            rows.Add(i == 42
                ? Row(codes[i], "خرسانة", null, "م٣", _babCode, null, "20")
                : Row(codes[i], "خرسانة", null, "م٣", _babCode, "10", "20"));
        }

        byte[] file = BuildFile(CatalogueTemplate.Columns, rows);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        Response body = await ReadResponseAsync(response);

        body.CreatedCount.Should().Be(199);
        body.Failures.Should().ContainSingle(f => f.RowNumber == 44 && f.MessageKey == "errors.master.cost_price_required");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.CountAsync(i => codes.Contains(i.Code), Ct)).Should().Be(199);
    }

    [Fact]
    public async Task A_code_repeated_in_the_file_refuses_the_later_occurrence_and_imports_the_first()
    {
        string code = UniqueNames.Code("IMP-H2");

        byte[] file = BuildFile(
            CatalogueTemplate.Columns,
            [
                Row(code, "خرسانة أولى", null, "م٣", _babCode, "10", "20"),
                Row(code, "خرسانة ثانية", null, "م٣", _babCode, "30", "40"),
            ]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        Response body = await ReadResponseAsync(response);

        body.CreatedCount.Should().Be(1);
        body.Failures.Should().ContainSingle(
            f => f.RowNumber == 3 && f.MessageKey == "errors.master.catalogue_item_code_repeated_in_file");

        await using KaffDbContext reader = _database.CreateBareContext();
        CatalogueItem stored = await reader.CatalogueItems.SingleAsync(i => i.Code == code, Ct);
        stored.DescriptionAr.Should().Be("خرسانة أولى", "the first occurrence imports, per D-136's row table");
    }

    [Fact]
    public async Task A_code_already_in_the_catalogue_is_refused_by_name()
    {
        string code = UniqueNames.Code("IMP-H3");

        byte[] first = BuildFile(
            CatalogueTemplate.Columns,
            [Row(code, "خرسانة", null, "م٣", _babCode, "10", "20")]);

        (await ImportAsync(_technicalOffice, Role.TechnicalOffice, first)).StatusCode.Should().Be(HttpStatusCode.OK);

        byte[] second = BuildFile(
            CatalogueTemplate.Columns,
            [Row(code, "خرسانة أخرى", null, "م٣", _babCode, "30", "40")]);

        Response body = await ReadResponseAsync(await ImportAsync(_technicalOffice, Role.TechnicalOffice, second));

        body.CreatedCount.Should().Be(0);
        body.Failures.Should().ContainSingle(
            f => f.MessageKey == "errors.master.catalogue_item_code_taken");
    }

    // ---- AC-200-I · the file's shape must be the template's shape ---------------------------------

    [Fact]
    public async Task An_extra_column_is_refused()
    {
        List<string> headers = [.. CatalogueTemplate.Columns, "extra"];

        byte[] file = BuildFile(
            headers,
            [[UniqueNames.Code("IMP-I1"), "خرسانة", null, "م٣", _babCode, "10", "20", "?"]]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // V-38-E / AC-200-I: the refusal names what the template requires and what the file carried,
        // not a bare code.
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("expectedColumns").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo(CatalogueTemplate.Columns);
        problem.RootElement.GetProperty("actualColumns").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo(headers);
    }

    [Fact]
    public async Task A_missing_column_is_refused()
    {
        List<string> headers = [.. CatalogueTemplate.Columns.Where(c => c != CatalogueTemplate.BaseSellRate)];

        byte[] file = BuildFile(
            headers,
            [[UniqueNames.Code("IMP-I2"), "خرسانة", null, "م٣", _babCode, "10"]]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("expectedColumns").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo(CatalogueTemplate.Columns);
        problem.RootElement.GetProperty("actualColumns").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo(headers,
                "the refusal must name what the file actually carried, missing column and all");
    }

    [Fact]
    public async Task A_single_description_column_standing_in_for_the_two_is_refused()
    {
        List<string> headers =
            [.. CatalogueTemplate.Columns.Where(c => c is not (CatalogueTemplate.DescriptionAr or CatalogueTemplate.DescriptionEn)), "description"];

        byte[] file = BuildFile(
            headers,
            [[UniqueNames.Code("IMP-I3"), "م٣", _babCode, "10", "20", "خرسانة"]]);

        (await ImportAsync(_technicalOffice, Role.TechnicalOffice, file)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Columns_reordered_but_complete_still_import_by_name_not_position()
    {
        List<string> headers =
            [.. CatalogueTemplate.Columns.Reverse()];

        string code = UniqueNames.Code("IMP-I4");

        byte[] file = BuildFile(
            headers,
            [["150.0000", "100.0000", _babCode, "م٣", null, "خرسانة عادية", code]]);

        HttpResponseMessage response = await ImportAsync(_technicalOffice, Role.TechnicalOffice, file);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the header check matches by name, not by column position");

        await using KaffDbContext reader = _database.CreateBareContext();
        CatalogueItem item = await reader.CatalogueItems.SingleAsync(i => i.Code == code, Ct);
        item.CostPrice.Amount.Should().Be(100.0000m);
        item.BaseSellRate.Amount.Should().Be(150.0000m);
    }

    [Fact]
    public async Task The_template_is_downloadable_and_reads_back_through_the_same_header_check()
    {
        HttpResponseMessage response = await GetTemplateAsync(_technicalOffice, Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(XlsxTemplateWriter.ContentType);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        XlsxReadResult read = XlsxSheetReader.Read(new MemoryStream(bytes));

        read.Success.Should().BeTrue();
        Dictionary<int, string?> headerText = read.Rows.Single().CellsByColumn
            .ToDictionary(pair => pair.Key, pair => pair.Value.Text);
        CatalogueTemplate.TryMatch(headerText, out _).Should().BeTrue(
            "AC-200-I: the file this endpoint hands out must be a file it also accepts");
    }

    // ---- blank rows are skipped, not reported ---------------------------------------------------

    [Fact]
    public async Task A_fully_blank_row_is_skipped_not_reported()
    {
        string code = UniqueNames.Code("IMP-BLANK");

        string sheetData = TestWorkbook.Row(1, HeaderCellsXml(CatalogueTemplate.Columns))
            + TestWorkbook.Row(2) // every cell absent
            + DataRowXml(3, CatalogueTemplate.Columns, Row(code, "خرسانة", null, "م٣", _babCode, "10", "20"));

        byte[] file = TestWorkbook.Build(sheetData);

        Response body = await ReadResponseAsync(await ImportAsync(_technicalOffice, Role.TechnicalOffice, file));

        body.CreatedCount.Should().Be(1);
        body.Failures.Should().BeEmpty("a formatted blank row is not one the user entered — D-136");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static IReadOnlyList<string?> Row(
        string code, string descriptionAr, string? descriptionEn, string unit, string bab, string? cost, string? sell)
        => [code, descriptionAr, descriptionEn, unit, bab, cost, sell];

    private static byte[] BuildFile(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(TestWorkbook.Row(1, HeaderCellsXml(headers)));

        int rowNumber = 2;

        foreach (IReadOnlyList<string?> row in rows)
        {
            sb.Append(DataRowXml(rowNumber, headers, row));
            rowNumber++;
        }

        return TestWorkbook.Build(sb.ToString());
    }

    private static string HeaderCellsXml(IReadOnlyList<string> headers)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < headers.Count; i++)
        {
            sb.Append(TestWorkbook.InlineText(i, 1, headers[i]));
        }

        return sb.ToString();
    }

    private static string DataRowXml(int rowNumber, IReadOnlyList<string> headers, IReadOnlyList<string?> values)
    {
        var cells = new StringBuilder();

        for (int i = 0; i < headers.Count && i < values.Count; i++)
        {
            if (values[i] is { } value)
            {
                cells.Append(TestWorkbook.InlineText(i, rowNumber, value));
            }
        }

        return TestWorkbook.Row(rowNumber, cells.ToString());
    }

    private IEnumerable<(Guid Actor, Role Role)> RefusedActors()
    {
        yield return (_finance, Role.Finance);
        yield return (_hr, Role.Hr);
        yield return (_siteEngineer, Role.SiteEngineer);
        yield return (_headOfDesign, Role.HeadOfDesign);
        yield return (_marketing, Role.MarketingSales);
    }

    private async Task<HttpResponseMessage> ImportAsync(
        Guid actorId, Role actorRole, byte[] file, string fileName = "catalogue.xlsx")
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(XlsxTemplateWriter.ContentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, ImportRoute) { Content = content };
        await StampAsync(request, actorId, actorRole);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> GetTemplateAsync(Guid actorId, Role actorRole)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, TemplateRoute);
        await StampAsync(request, actorId, actorRole);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(HttpRequestMessage request, Guid actorId, Role actorRole)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        Guid? department = actorRole switch
        {
            Role.TechnicalOffice => WellKnownDepartments.OperationsId,
            Role.Finance => WellKnownDepartments.FinanceId,
            Role.Hr => WellKnownDepartments.HrId,
            Role.SiteEngineer => WellKnownDepartments.OperationsId,
            Role.HeadOfDesign => WellKnownDepartments.OperationsId,
            Role.MarketingSales => null,
            _ => null,
        };

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }
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

    private static async Task<Response> ReadResponseAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync(Ct);

        return JsonSerializer.Deserialize<Response>(json, JsonOptions)
            ?? throw new InvalidOperationException("Empty import response body.");
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        _babCode = UniqueNames.Code("IMP-BAB");
        Bab bab = Bab.Create(_babCode, "باب", "Bab", Percentage.FromPercent(15m)).Value;

        User owner = MakeUser("imp-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "imp-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("imp-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User hr = MakeUser("imp-hr", Role.Hr, WellKnownDepartments.HrId);
        User siteEngineer = MakeUser(
            "imp-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "imp-design", Role.HeadOfDesign, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User marketing = MakeUser("imp-marketing", Role.MarketingSales, null);

        context.Babs.Add(bab);
        context.Users.AddRange(owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing);

        await context.SaveChangesAsync(Ct);

        _babId = bab.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, null)
            .Value;

    private static DateTimeOffset Now => new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
