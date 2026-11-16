using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Projects;

/// <summary>
/// KAFF-415 through the endpoints: a design project converts into an execution project, the execution signature raises
/// the 30% design credit, and the credit posts on the execution project's accounts only when a different user approves.
/// </summary>
/// <remarks>Every project here is created on the current clock, so none hits the derived archive rule (AC-417-J).</remarks>
[Collection(DatabaseCollection.Name)]
public sealed class DesignToExecutionTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private readonly TestClock _clock = new();
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _secondOwner;
    private Guid _technicalOffice;
    private Guid _technicalOfficeUnassigned;
    private Guid _itemId;

    public DesignToExecutionTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database.ConnectionString, clock: _clock);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- conversion ----

    [Fact]
    public async Task AC_415_A_C_H_I_conversion_opens_a_linked_execution_project_with_its_own_accounts()
    {
        Guid design = await NewDesignAsync();

        HttpResponseMessage response = await ConvertAsync(design, _technicalOffice, "LumpSum");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await JsonAsync(response);
        Guid execution = body.GetProperty("id").GetGuid();
        body.GetProperty("contractType").GetString().Should().Be("LumpSum");
        body.GetProperty("designProjectId").GetGuid().Should().Be(design);
        body.GetProperty("clientId").GetGuid().Should().Be(_clientId);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Project designRow = await reader.Projects.SingleAsync(p => p.Id == design, Ct);
            designRow.ContractType.Should().Be(ContractType.Design);
            designRow.LinkedProjectId.Should().BeNull();

            List<Account> executionAccounts = await reader.Accounts.Where(a => a.ProjectId == execution).ToListAsync(Ct);
            List<Guid> designAccounts = await reader.Accounts.Where(a => a.ProjectId == design).Select(a => a.Id).ToListAsync(Ct);
            executionAccounts.Select(a => a.Type).Should().BeEquivalentTo(ProjectAccountSetFactory.RequiredTypes(ContractType.LumpSum));
            executionAccounts.Select(a => a.Id).Should().NotIntersectWith(designAccounts);

            (await reader.AuditRecords.AnyAsync(
                r => r.EntityType == nameof(Project) && r.EntityId == execution && r.Action == AuditAction.Created
                    && r.ActorUserId == _technicalOffice, Ct))
                .Should().BeTrue("the conversion is audited by the interceptor");
        }

        JsonElement executionView = await GetProjectAsync(execution);
        executionView.GetProperty("designProjectId").GetGuid().Should().Be(design);
        executionView.GetProperty("executionProjectId").ValueKind.Should().Be(JsonValueKind.Null);
        decimal.Parse(executionView.GetProperty("designAreaSquareMetres").GetString()!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(100m, "decimals cross the wire as strings");

        JsonElement designView = await GetProjectAsync(design);
        designView.GetProperty("executionProjectId").GetGuid().Should().Be(execution);
        designView.GetProperty("designProjectId").ValueKind.Should().Be(JsonValueKind.Null);

        await using KaffDbContext boqReader = _database.CreateBareContext();
        (await boqReader.Boqs.AnyAsync(b => b.ProjectId == execution, Ct)).Should().BeFalse("Q-207-5: no BOQ line is seeded");
    }

    [Fact]
    public async Task D_227_the_technical_office_user_who_converts_can_open_the_execution_project()
    {
        Guid design = await NewDesignAsync();
        Guid execution = (await JsonAsync(await ConvertAsync(design, _technicalOffice, "LumpSum"))).GetProperty("id").GetGuid();

        (await SendAsync(HttpMethod.Get, $"/api/projects/{execution}", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{execution}", _technicalOfficeUnassigned, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC_415_B_a_non_design_project_is_refused()
    {
        Guid lumpSum = await NewProjectAsync(ContractType.LumpSum);

        HttpResponseMessage response = await ConvertAsync(lumpSum, _owner, "LumpSum");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.project.not_design");
    }

    [Fact]
    public async Task AC_415_B_a_second_conversion_is_refused()
    {
        Guid design = await NewDesignAsync();
        (await ConvertAsync(design, _owner, "CostPlus")).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await ConvertAsync(design, _owner, "LumpSum");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.project.already_converted");
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.CountAsync(p => p.LinkedProjectId == design, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Q_207_9_design_as_the_target_type_is_refused()
    {
        Guid design = await NewDesignAsync();

        HttpResponseMessage response = await ConvertAsync(design, _owner, "Design");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.execution_type_not_allowed");
    }

    [Fact]
    public async Task AC_415_H_a_technical_office_user_not_assigned_to_the_design_project_is_refused()
    {
        Guid design = await NewDesignAsync();

        (await ConvertAsync(design, _technicalOfficeUnassigned, "LumpSum")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.AnyAsync(p => p.LinkedProjectId == design, Ct)).Should().BeFalse();
    }

    // ---- the credit ----

    [Fact]
    public async Task AC_415_D_E_F_signature_raises_one_credit_the_signer_cannot_approve_and_another_owner_posts_it()
    {
        Guid design = await NewDesignAsync();
        Guid execution = (await JsonAsync(await ConvertAsync(design, _owner, "LumpSum"))).GetProperty("id").GetGuid();
        await AddBoqLineAsync(execution);

        HttpResponseMessage signed = await SendAsync(HttpMethod.Post, $"/api/projects/{execution}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" });
        signed.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid creditId = (await JsonAsync(signed)).GetProperty("designCreditAdjustmentId").GetGuid();

        Adjustment credit = await LoadCreditAsync(execution);
        credit.Id.Should().Be(creditId);
        credit.Type.Should().Be(AdjustmentType.DesignCredit);
        credit.Status.Should().Be(AdjustmentStatus.Raised);
        credit.Amount.Should().Be(new Money(13_500m), "30% of 100 m2 x 450");
        credit.SourceDocumentType.Should().Be(SourceDocumentType.DesignProject);
        credit.SourceDocumentId.Should().Be(design);
        credit.RaisedByUserId.Should().Be(_owner);
        (await AdjustmentPostingsAsync(creditId)).Should().BeEmpty("raised, not posted");

        // A second signature is refused and raises nothing.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{execution}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            (await reader.Adjustments.CountAsync(a => a.ProjectId == execution, Ct)).Should().Be(1);
        }

        string approval = $"/api/projects/{execution}/adjustments/{creditId}/approval";
        HttpResponseMessage bySigner = await SendAsync(HttpMethod.Post, approval, _owner, null);
        bySigner.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(bySigner)).Should().Be("errors.auth.same_actor_created_and_approved");
        (await AdjustmentPostingsAsync(creditId)).Should().BeEmpty();

        (await SendAsync(HttpMethod.Post, approval, _secondOwner, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        Posting posting = (await AdjustmentPostingsAsync(creditId)).Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.CreditNote);
        posting.Amount.Should().Be(new Money(13_500m));
        posting.ProjectId.Should().Be(execution);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Account from = await reader.Accounts.SingleAsync(a => a.Id == posting.FromAccountId, Ct);
            Account to = await reader.Accounts.SingleAsync(a => a.Id == posting.ToAccountId, Ct);
            from.Type.Should().Be(AccountType.ClientReceivable);
            to.Type.Should().Be(AccountType.ContractRevenue);
            from.ProjectId.Should().Be(execution);
            to.ProjectId.Should().Be(execution);

            (await reader.Postings.AnyAsync(p => p.ProjectId == design, Ct)).Should().BeFalse("the design project is never posted on");
            AccountType[] untouchable = [AccountType.Hold, AccountType.ClientAdvance, AccountType.FirmAdvance, AccountType.MaterialAdvance];
            (await reader.Postings
                    .Where(p => p.ProjectId == execution)
                    .Join(reader.Accounts, p => p.FromAccountId, a => a.Id, (p, a) => a.Type)
                    .Concat(reader.Postings.Where(p => p.ProjectId == execution)
                        .Join(reader.Accounts, p => p.ToAccountId, a => a.Id, (p, a) => a.Type))
                    .ToListAsync(Ct))
                .Should().NotIntersectWith(untouchable);
        }

        // Reversal mirrors the posting through ReversesId; nothing is updated or deleted.
        HttpResponseMessage reversed = await SendAsync(
            HttpMethod.Post, $"/api/projects/{execution}/adjustments/{creditId}/reversal", _secondOwner, new { reason = "wrong design value" });
        reversed.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();
        Posting mirror = await after.Postings.SingleAsync(p => p.ReversesId == posting.Id, Ct);
        mirror.FromAccountId.Should().Be(posting.ToAccountId);
        mirror.ToAccountId.Should().Be(posting.FromAccountId);
        mirror.Amount.Should().Be(posting.Amount);
        (await after.Postings.SingleAsync(p => p.Id == posting.Id, Ct)).Amount.Should().Be(posting.Amount, "the original stands");
        (await after.Adjustments.SingleAsync(a => a.Id == creditId, Ct)).Status.Should().Be(AdjustmentStatus.Reversed);
    }

    [Fact]
    public async Task A_cost_plus_execution_project_opens_its_client_sub_ledger_and_the_credit_posts()
    {
        Guid design = await NewDesignAsync();
        Guid execution = (await JsonAsync(await ConvertAsync(design, _technicalOffice, "CostPlus"))).GetProperty("id").GetGuid();
        // D-227: the converting user is assigned by the conversion itself, no manual assignment.

        (await SendAsync(HttpMethod.Post, $"/api/projects/{execution}/sign", _technicalOffice, new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        Adjustment credit = await LoadCreditAsync(execution);
        credit.RaisedByUserId.Should().Be(_technicalOffice);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{execution}/adjustments/{credit.Id}/approval", _owner, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await AdjustmentPostingsAsync(credit.Id)).Should().ContainSingle()
            .Which.Amount.Should().Be(new Money(13_500m));
    }

    [Fact]
    public async Task AC_417_K_design_signs_with_area_priced_at_450_per_m2_and_conversion_credits_30_percent()
    {
        Guid design = await NewDesignAsync(area: null);

        HttpResponseMessage signed = await SendAsync(HttpMethod.Post, $"/api/projects/{design}/sign", _owner, new { areaSquareMetres = "100" });
        signed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(signed)).GetProperty("contractValue").GetString().Should().StartWith("45000");

        Guid execution = (await JsonAsync(await ConvertAsync(design, _owner, "LumpSum"))).GetProperty("id").GetGuid();
        await AddBoqLineAsync(execution);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{execution}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await LoadCreditAsync(execution)).Amount.Should().Be(new Money(13_500m), "30% of 100 m2 x 450");
    }

    [Fact]
    public async Task AC_417_K_design_without_area_is_refused()
    {
        Guid design = await NewDesignAsync(area: null);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{design}/sign", _owner, new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.area_required");
    }

    [Fact]
    public async Task AC_417_K_non_positive_area_is_refused()
    {
        Guid design = await NewDesignAsync(area: null);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{design}/sign", _owner, new { areaSquareMetres = "0" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.area_required");
    }

    [Fact]
    public async Task AC_417_K_lump_sum_with_area_is_refused()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        await AddBoqLineAsync(project);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { areaSquareMetres = "100" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.terms_do_not_match_contract_type");
    }

    [Fact]
    public async Task An_unlinked_project_signs_without_any_design_credit()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        await AddBoqLineAsync(project);

        HttpResponseMessage signed = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" });

        signed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(signed)).GetProperty("designCreditAdjustmentId").ValueKind.Should().Be(JsonValueKind.Null);
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Adjustments.AnyAsync(a => a.ProjectId == project, Ct)).Should().BeFalse();
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private Task<HttpResponseMessage> ConvertAsync(Guid design, Guid actor, string contractType) =>
        SendAsync(HttpMethod.Post, $"/api/projects/{design}/convert-to-execution", actor, new { name = "تنفيذ فيلا", contractType });

    private async Task AddBoqLineAsync(Guid project) =>
        (await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines", _owner,
            new { catalogueItemId = _itemId, quantityEstimated = 5m, quantitySurveyed = 4m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

    private async Task<Adjustment> LoadCreditAsync(Guid execution)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Adjustments.SingleAsync(a => a.ProjectId == execution && a.Type == AdjustmentType.DesignCredit, Ct);
    }

    private async Task<List<Posting>> AdjustmentPostingsAsync(Guid adjustment)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings
            .Where(p => p.SourceDocumentType == SourceDocumentType.Adjustment && p.SourceDocumentId == adjustment && p.ReversesId == null)
            .ToListAsync(Ct);
    }

    private async Task<JsonElement> GetProjectAsync(Guid project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project}", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonAsync(response);
    }

    private async Task<Guid> NewDesignAsync(decimal? area = 100m)
    {
        await using KaffDbContext context = _database.CreateContext();

        OpenedProject opened = ProjectOpening.Open("تصميم فيلا", _clientId, ContractType.Design, null, null, _clock.GetUtcNow()).Value;
        if (area is { } seeded)
        {
            opened.Project.SetDesignTerms(seeded, new Money(450m)).IsSuccess.Should().BeTrue();
        }

        context.Projects.Add(opened.Project);
        context.Accounts.AddRange(opened.Accounts);
        await context.SaveChangesAsync(Ct);

        await AssignAsync(opened.Project.Id, _technicalOffice);
        return opened.Project.Id;
    }

    private async Task<Guid> NewProjectAsync(ContractType type)
    {
        await using KaffDbContext context = _database.CreateContext();

        OpenedProject opened = ProjectOpening.Open("مشروع", _clientId, type, null, null, _clock.GetUtcNow()).Value;
        context.Projects.Add(opened.Project);
        context.Accounts.AddRange(opened.Accounts);
        await context.SaveChangesAsync(Ct);
        return opened.Project.Id;
    }

    private async Task AssignAsync(Guid project, Guid userId)
    {
        await using KaffDbContext context = _database.CreateContext();
        User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(project, user, AssignmentLevel.Standard, _owner, _clock.GetUtcNow()).Value);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Client client = Client.Create(UniqueNames.Code("DX-C"), "عميل التصميم", UniqueNames.Phone(), ClientKind.Corporate, now).Value;
        User owner = User.Create(UniqueNames.Code("dx-owner"), "dx-owner", UniqueNames.Phone(), Role.Owner, now).Value;
        User secondOwner = User.Create(UniqueNames.Code("dx-owner2"), "dx-owner2", UniqueNames.Phone(), Role.Owner, now).Value;
        User technicalOffice = User.Create(
            UniqueNames.Code("dx-tech"), "dx-tech", UniqueNames.Phone(), Role.TechnicalOffice, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        User technicalOfficeUnassigned = User.Create(
            UniqueNames.Code("dx-tech-un"), "dx-tech-un", UniqueNames.Phone(), Role.TechnicalOffice, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        Bab bab = Bab.Create(UniqueNames.Code("DXB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("DXI"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, secondOwner, technicalOffice, technicalOfficeUnassigned);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _secondOwner = secondOwner.Id;
        _technicalOffice = technicalOffice.Id;
        _technicalOfficeUnassigned = technicalOfficeUnassigned.Id;
        _itemId = item.Id;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
