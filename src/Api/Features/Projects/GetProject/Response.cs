using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Projects.GetProject;

/// <summary>
/// One project's header, for S-052. KAFF-400.
/// </summary>
/// <remarks>
/// <b>No stored balance</b> — CLAUDE.md, spec.md §6.1. <c>ContractValue</c> is the signed term, never a
/// derived figure. <b>Every money-shaped member crosses the wire as a JSON string</b> — decisions.md
/// D-135, through the global converters in <c>KaffJson</c>. <b>Contract terms follow <c>Permission.ProjectMoneyRead</c></b> (KAFF-420 AC-5), the rule
/// <c>GetProgress</c> already uses: <c>ContractValue</c>, <c>OriginalContractValue</c>, <c>ApprovedChangeOrderTotal</c>,
/// <c>HoldRate</c>, <c>AdvanceRate</c> and <c>AdvanceRecoveryRate</c> are null, and so absent from the JSON, for any
/// other <c>ProjectRead</c> holder, as is <c>Money</c>.
/// </remarks>
/// <param name="Id">The project.</param>
/// <param name="Code">Generated.</param>
/// <param name="Name">As stored.</param>
/// <param name="ClientId">The owning client.</param>
/// <param name="ClientName">Denormalised for the header — S-052 needs it without a second call.</param>
/// <param name="ContractType">Lump Sum, Cost Plus or Design (spec.md §5).</param>
/// <param name="Status">spec.md §13's state machine. Never متعثرة or تم تأجيلها — those are health tags,
/// not states (D-044 §7), and carry no field here yet; KAFF-400 does not build the tag.</param>
/// <param name="ContractValue">
/// The REVISED value: the signed value plus the approved change orders, derived on every read (ruling
/// <c>ChangeOrderDisplayedValue</c>). Null for Cost Plus.
/// </param>
/// <param name="OriginalContractValue">The signed value, before any change order.</param>
/// <param name="ApprovedChangeOrderTotal">The sum of the approved change orders. Zero when there are none.</param>
/// <param name="HoldRate">Lump Sum only.</param>
/// <param name="AdvanceRate">Lump Sum only.</param>
/// <param name="WorkStarted">D-228 item 4: true once any daily log entry exists on the project. Absent when false. Display only; the state machine does not read it.</param>
/// <param name="ConversionOffered">D-228 item 2, interim: a signed design project that has not converted. The web offers «تحويل إلى تنفيذ» only when true, so sign and convert are never offered together.</param>
/// <param name="DesignRatePerSquareMetre">
/// A Design project only, and only for a caller who may read project money: the fee per square metre. The rate stored at
/// signing once signed, <c>ContractDefaults.DesignRatePerSquareMetre</c> before, which is what <c>SignProject</c> will store.
/// The web previews the fee from this and holds no price of its own.
/// </param>
/// <param name="AdvanceRecoveryRate">Lump Sum only. The share of each extract that repays the advance.</param>
/// <param name="WithholdingCategory">The contract's withholding category (spec.md section 6.7). Only for a caller who may read project money.</param>
/// <param name="Actions">What the caller may do from the frame now.</param>
/// <param name="TeamSize">
/// Count of active <c>ProjectAssignment</c> rows. S-052's "team" — the per-member roster (KAFF-115) is
/// its own story and is not built here; see the build notes.
/// </param>
/// <param name="SignedOn">Null until signed.</param>
/// <param name="StartedOn">Null until the project first becomes Active.</param>
/// <param name="HandoverOn">Null until handover.</param>
/// <param name="WarrantyEndsOn">Null until the warranty period is set at handover.</param>
/// <param name="ClosedOn">Null until closed.</param>
/// <param name="LastDailyLogOn">
/// The date of the most recent daily log on the project, derived on every read. Null when none has been written.
/// There is no contract duration and no target completion on <c>Project</c>, so none is returned and none is
/// invented (ruling <c>ProjectTargetCompletion</c>).
/// </param>
/// <param name="Money">
/// Derived from the extracts and the collection postings; never a stored balance. Null for a caller who may not
/// see extract money.
/// </param>
/// <param name="IsArchived">True for an unsigned Setup project whose follow-up period has ended (AC-417-J). Derived on every read.</param>
/// <param name="FollowUpEndsOn">The day the follow-up period ends. Null once the project is signed or has left Setup.</param>
/// <param name="DesignProjectId">KAFF-415: on an execution project, the design project it was converted from. Null otherwise.</param>
/// <param name="ExecutionProjectId">KAFF-415: on a design project, the execution project it converted into. Null otherwise.</param>
/// <param name="DesignAreaSquareMetres">
/// KAFF-415, Q-207-5: on an execution project converted from a design, the design area, carried as a reference. No BOQ
/// line is seeded from it. Null otherwise.
/// </param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    Guid ClientId,
    string ClientName,
    ContractType ContractType,
    ProjectStatus Status,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? ContractValue,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Percentage? HoldRate,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Percentage? AdvanceRate,
    int TeamSize,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? OriginalContractValue,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? ApprovedChangeOrderTotal,
    DateOnly? SignedOn,
    DateOnly? StartedOn,
    DateOnly? HandoverOn,
    DateOnly? WarrantyEndsOn,
    DateOnly? ClosedOn,
    DateOnly? LastDailyLogOn,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ProjectMoney? Money,
    bool IsArchived,
    DateOnly? FollowUpEndsOn,
    Guid? DesignProjectId,
    Guid? ExecutionProjectId,
    decimal? DesignAreaSquareMetres,
    ProjectStage Stage,
    bool IsSigned,
    bool LoggedToday,
    ProjectTabs Tabs,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Percentage? AdvanceRecoveryRate = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WorkStarted = false,
    bool ConversionOffered = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? DesignRatePerSquareMetre = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WithholdingCategory? WithholdingCategory = null,
    ProjectActions? Actions = null,
    DateOnly? PlannedStart = null,
    DateOnly? PlannedEnd = null);

/// <summary>
/// What this caller may do from the project frame now (D-246). Each flag is the check the route behind it makes, asked of
/// the same caller: role and assignment, then the route's own refusals.
/// </summary>
/// <param name="EditWithholding">Set the contract's withholding category: a <c>ProjectFinancialsEdit</c> holder, and the client is not an individual.</param>
/// <param name="PrepareExtract">Prepare the next extract: an <c>ExtractPrepare</c> holder, and the prepare route would not refuse it now.</param>
public sealed record ProjectActions(bool EditWithholding, bool PrepareExtract);

/// <summary>
/// What the client has been billed, paid and still owes. Only extracts issued to the client count; an extract still
/// in the approval chain is not yet a debt.
/// </summary>
/// <param name="CertifiedToDate">Certified work value of the issued extracts.</param>
/// <param name="BilledToDate">Net payable of the issued extracts.</param>
/// <param name="CollectedToDate">Cash received plus tax withheld at source on them, summed from the postings, net of reversals.</param>
/// <param name="Outstanding">Billed less collected.</param>
public sealed record ProjectMoney(Money CertifiedToDate, Money BilledToDate, Money CollectedToDate, Money Outstanding);
