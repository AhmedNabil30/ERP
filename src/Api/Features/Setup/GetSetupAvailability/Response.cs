namespace Kaff.Api.Features.Setup.GetSetupAvailability;

/// <summary>
/// <c>ux/slice-1-flows.md</c> S-002: <c>{ "available": true }</c> while the users table is empty,
/// <c>{ "available": false }</c> for ever afterwards. The SPA route decision (show <c>/setup</c> or
/// not) turns on this single boolean, never on a cookie or on looking at the database itself, because
/// after D-050 the client can do neither.
/// </summary>
public sealed record Response(bool Available);
