using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>Builds valid accounts so tests can say what they are actually about.</summary>
internal static class TestAccounts
{
    private static readonly DateOnly Opened = new(2026, 1, 1);

    public static readonly Guid ProjectId = Guid.Parse("0195c000-0000-7000-8000-000000000001");
    public static readonly Guid ClientId = Guid.Parse("0195c000-0000-7000-8000-000000000002");
    public static readonly Guid OtherProjectId = Guid.Parse("0195c000-0000-7000-8000-000000000003");

    public static Account Safe() => Build(AccountType.Safe, "SAFE-MAIN");

    public static Account CompanyExpense() => Build(AccountType.CompanyExpense, "EXP-COMPANY");

    public static Account ProjectControl(Guid? projectId = null) =>
        Build(AccountType.ProjectControl, "PRJ-CTRL", projectId ?? ProjectId);

    public static Account ClientReceivable(Guid? projectId = null) =>
        Build(AccountType.ClientReceivable, "PRJ-AR", projectId ?? ProjectId, PartyType.Client, ClientId);

    public static Account Hold(Guid? projectId = null) =>
        Build(AccountType.Hold, "PRJ-HOLD", projectId ?? ProjectId, PartyType.Client, ClientId);

    public static Account ClientAdvance(Guid? projectId = null) =>
        Build(AccountType.ClientAdvance, "PRJ-ADV", projectId ?? ProjectId, PartyType.Client, ClientId);

    public static Account MaterialAdvance(Guid? projectId = null) =>
        Build(AccountType.MaterialAdvance, "PRJ-MAT", projectId ?? ProjectId, PartyType.Client, ClientId);

    public static Account ProjectCost(Guid? projectId = null) =>
        Build(AccountType.ProjectCost, "PRJ-COST", projectId ?? ProjectId);

    private static Account Build(
        AccountType type,
        string code,
        Guid? projectId = null,
        PartyType? partyType = null,
        Guid? partyId = null)
    {
        Result<Account> result = Account.Create(
            type,
            code,
            $"حساب {code}",
            $"Account {code}",
            Currency.Egp,
            Opened,
            projectId,
            partyType,
            partyId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test account '{code}' is invalid: {result.Error.Code}.");
        }

        return result.Value;
    }

    public static SourceDocument Document() =>
        new(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT-001");

    public static DateOnly Today => new(2026, 3, 1);

    public static DateTimeOffset Now => new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    public static Guid Actor => Guid.Parse("0195c000-0000-7000-8000-0000000000aa");
}
