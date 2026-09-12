using Kaff.Domain.Common;

namespace Kaff.Api.Features.DayLabour.SetEngagementDayRate;

/// <summary>The engagement's day rate, after the write. KAFF-210.</summary>
public sealed record Response(Guid EngagementId, Money DayRate);
