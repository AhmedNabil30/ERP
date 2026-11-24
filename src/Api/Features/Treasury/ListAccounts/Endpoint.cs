using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.ListAccounts;

/// <summary>
/// <c>GET /api/treasury/accounts?type=&amp;projectId=</c>. A read-only list of the accounts the accounting forms pick from: accrued
/// expense, company expense, prepayment, accumulated depreciation, depreciation expense and project cost. Gated
/// <c>TreasuryReadCompany</c>. Without <c>projectId</c> it lists the company accounts only; with it, the handler also requires
/// <c>TreasuryReadProject</c> for that project (role and assignment) and lists that project's accounts.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/accounts";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListTreasuryAccounts")
            .WithTags("Treasury");
    }
}
