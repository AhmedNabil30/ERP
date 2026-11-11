using Kaff.Domain.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Clients.QuickAddClient;

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CreateClient.Handler.HandleAsync(
            new CreateClient.Request(
                request.Name,
                request.Phone,
                request.Kind,
                AlternatePhone: null,
                Email: null,
                Address: null,
                TaxRegistrationNumber: null,
                request.AcknowledgedDuplicatePhone),
            database,
            audit,
            clock,
            cancellationToken);
    }
}
