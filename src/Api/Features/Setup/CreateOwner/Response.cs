using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Setup.CreateOwner;

public sealed record Response(Guid UserId, string UserName, string FullName, Role Role, bool IsActive);
