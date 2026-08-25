namespace Kaff.Api.Features.Users.DeactivateUser;

/// <summary>
/// Why the account is being switched off. The user is named by the route, not by the body.
/// </summary>
/// <param name="Reason">
/// Recorded verbatim on every audit record the act writes, when it is supplied (<c>AC-110-G</c>).
/// <para>
/// <b>Optional, and deliberately so.</b> Whether the Owner must type one is <b>Q35, open</b>. The
/// mandatory-reason rule was not waived, it was <i>withdrawn</i>: no cited source states it,
/// <c>User.Deactivate</c> takes only a timestamp, and <c>IAuditContext.SetReason</c> is a voluntary
/// call. CLAUDE.md's "why, where the flow requires it" is the judgement, not the rule — and
/// refusing a request for a rule nobody has stated is this backlog's own named failure mode. If
/// Karim answers yes, the gate belongs in <c>Validator.cs</c> in this folder and the same shape
/// applies to every rejection gate in slice 5.
/// </para>
/// </param>
public sealed record Request(string? Reason);
