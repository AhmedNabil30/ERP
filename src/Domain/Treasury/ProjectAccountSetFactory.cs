using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Treasury;

/// <summary>
/// Builds the fixed ledger-account set a project needs the moment it exists (spec.md §6.3, §5.1-§5.3).
/// KAFF-302.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lump Sum gets the full five</b> (rule 2, <c>AC-302-A</c>): client advance, hold, firm advance,
/// تشوينات (<see cref="AccountType.MaterialAdvance"/>) and the client sub-ledger
/// (<see cref="AccountType.ClientReceivable"/>). Cost Plus and Design explicitly lose hold and تشوينات
/// (rules 3-4, <c>AC-302-B</c>/<c>C</c>) — spec.md §5.2 and §5.3, verbatim "No hold. No تشوينات." and
/// "no hold, no تشوينات".
/// </para>
/// <para>
/// <b><c>Q88</c> answered</b> (Karim via Nabil, 2026-09-14, decisions.md D-161): Cost Plus opens
/// "Project Operating Costs" and "Management/Supervision Revenues"; Design opens "Design Revenues"
/// and "Consulting Costs". Both pairs reuse the existing generic <see cref="AccountType.ProjectCost"/>
/// and <see cref="AccountType.ContractRevenue"/> types — only the name differs per contract type — so
/// no new <see cref="AccountType"/> member was added. Karim: accounts can be adjusted from the chart
/// of accounts later, so this seed only needs to unblock, not be final.
/// </para>
/// <para>
/// <b>عهدة (<see cref="AccountType.PettyCashAdvance"/>) is never part of this set</b>, at any contract
/// type (rule 1, <c>AC-302-D</c>): it is per-employee (<c>AccountScope.ProjectRequired</c> and
/// <c>PartyType.Employee</c>) and there is no employee to attach one to at project-creation time.
/// <c>KAFF-311</c> opens one when a specific request names its holder.
/// </para>
/// <para>
/// <b>Every account goes through <see cref="Account.Create"/> and nothing here bypasses it</b> (rule 6,
/// <c>AC-302-G</c>) — this factory only decides which types to open and what to name them; scope,
/// party and metadata validation all belong to the entity.
/// </para>
/// </remarks>
public static class ProjectAccountSetFactory
{
    /// <summary>The account types <paramref name="contractType"/>'s automatic set opens.</summary>
    public static IReadOnlyList<AccountType> RequiredTypes(ContractType contractType) => contractType switch
    {
        ContractType.LumpSum =>
        [
            AccountType.ClientAdvance,
            AccountType.Hold,
            AccountType.FirmAdvance,
            AccountType.MaterialAdvance,
            AccountType.ClientReceivable,
        ],

        // Q88 (D-161): both get a revenue and a cost account, no hold, no تشوينات.
        ContractType.CostPlus or ContractType.Design =>
        [
            AccountType.ContractRevenue,
            AccountType.ProjectCost,
        ],

        _ => throw new ArgumentOutOfRangeException(nameof(contractType), contractType, "Unhandled contract type."),
    };

    /// <summary>
    /// Builds — but does not persist — the accounts <paramref name="project"/> is still missing from
    /// <see cref="RequiredTypes"/>. <paramref name="existingTypes"/> names the types the project
    /// already has an account of, so a re-run is a no-op (rule 8, <c>AC-302-F</c>). Fails the whole
    /// batch, touching nothing, if any one account fails <see cref="Account.Create"/>'s own validation
    /// — there is no partial set (<c>AC-302-E</c>).
    /// </summary>
    public static Result<IReadOnlyList<Account>> Create(
        Project project,
        IReadOnlySet<AccountType> existingTypes,
        DateOnly openedOn)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(existingTypes);

        List<Account> accounts = [];

        foreach (AccountType type in RequiredTypes(project.ContractType))
        {
            if (existingTypes.Contains(type))
            {
                continue;
            }

            AccountTypeMetadata meta = AccountTypes.Of(type);

            Result<Account> created = Account.Create(
                type,
                Code(project.Code, type, project.ContractType),
                NameAr(type, project.ContractType),
                NameEn(type, project.ContractType),
                project.Currency,
                openedOn,
                projectId: project.Id,
                partyType: meta.RequiredParty,
                partyId: meta.RequiredParty is null ? null : project.ClientId);

            if (created.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Account>>(created.Error);
            }

            accounts.Add(created.Value);
        }

        return Result.Success<IReadOnlyList<Account>>(accounts);
    }

    /// <summary>
    /// <c>{project code}-{suffix}</c>, e.g. <c>PRJ-0007-HOLD</c>. Project.Code is capped at 32
    /// characters and the longest suffix here is 8 (<c>-CONSULT</c>), so the result never exceeds
    /// <see cref="Account.MaxCodeLength"/> (40).
    /// </summary>
    private static string Code(string projectCode, AccountType type, ContractType contractType) => (type, contractType) switch
    {
        (AccountType.ClientAdvance, _) => $"{projectCode}-CADV",
        (AccountType.Hold, _) => $"{projectCode}-HOLD",
        (AccountType.FirmAdvance, _) => $"{projectCode}-FADV",
        (AccountType.MaterialAdvance, _) => $"{projectCode}-MATADV",
        (AccountType.ClientReceivable, _) => $"{projectCode}-CREC",
        (AccountType.ProjectCost, ContractType.CostPlus) => $"{projectCode}-OPCOST",
        (AccountType.ContractRevenue, ContractType.CostPlus) => $"{projectCode}-MREV",
        (AccountType.ContractRevenue, ContractType.Design) => $"{projectCode}-DREV",
        (AccountType.ProjectCost, ContractType.Design) => $"{projectCode}-CONSULT",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No project-account code suffix registered for this type/contract combination."),
    };

    private static string NameAr(AccountType type, ContractType contractType) => (type, contractType) switch
    {
        (AccountType.ClientAdvance, _) => "دفعة مقدمة من العميل",
        (AccountType.Hold, _) => "محجوز",
        (AccountType.FirmAdvance, _) => "سلفة الشركة",
        (AccountType.MaterialAdvance, _) => "تشوينات",
        (AccountType.ClientReceivable, _) => "مستحق من العميل",
        (AccountType.ProjectCost, ContractType.CostPlus) => "تكاليف تشغيل المشروع",
        (AccountType.ContractRevenue, ContractType.CostPlus) => "إيرادات الإدارة/الإشراف",
        (AccountType.ContractRevenue, ContractType.Design) => "إيرادات التصميم",
        (AccountType.ProjectCost, ContractType.Design) => "تكاليف الاستشارات",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No Arabic name registered for this type/contract combination."),
    };

    private static string NameEn(AccountType type, ContractType contractType) => (type, contractType) switch
    {
        (AccountType.ClientAdvance, _) => "Client advance",
        (AccountType.Hold, _) => "Hold",
        (AccountType.FirmAdvance, _) => "Firm advance",
        (AccountType.MaterialAdvance, _) => "Material advance",
        (AccountType.ClientReceivable, _) => "Client receivable",
        (AccountType.ProjectCost, ContractType.CostPlus) => "Project operating costs",
        (AccountType.ContractRevenue, ContractType.CostPlus) => "Management/Supervision revenues",
        (AccountType.ContractRevenue, ContractType.Design) => "Design revenues",
        (AccountType.ProjectCost, ContractType.Design) => "Consulting costs",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No English name registered for this type/contract combination."),
    };
}
