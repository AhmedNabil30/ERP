using Kaff.Domain.Common;

namespace Kaff.Api.Features.Projects.SignProject;

/// <param name="ProjectId">The signed project.</param>
/// <param name="SignedOn">The signature date.</param>
/// <param name="ContractValue">Derived from the BOQ total (Lump Sum) or area × rate (Design); null for Cost Plus.</param>
/// <param name="BoqFrozenOn">The date the BOQ was frozen, or null if the project has no BOQ.</param>
/// <param name="DesignCreditAdjustmentId">
/// KAFF-415: the 30% design credit this signature raised, status Raised, when the project was converted from a design
/// project. Null otherwise.
/// </param>
public sealed record Response(
    Guid ProjectId, DateOnly SignedOn, Money? ContractValue, DateOnly? BoqFrozenOn, Guid? DesignCreditAdjustmentId);
