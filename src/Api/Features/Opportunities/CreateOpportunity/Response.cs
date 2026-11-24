using Kaff.Domain.Sales;

namespace Kaff.Api.Features.Opportunities.CreateOpportunity;

public sealed record Response(Guid Id, string Code, Guid ClientId, string Title, OpportunityStage Stage, OpportunityStatus Status);
