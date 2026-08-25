using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaff.Api.Authorization;

/// <summary>
/// The single server-side gate. Every protected endpoint passes through here.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9: "Permission = role × assignment. A user MUST be assigned to a project to open it or
/// act on it. Role alone is insufficient. Enforcement is server-side; hiding UI elements is
/// presentation, not security."
/// </para>
/// <para>
/// The handler decides nothing itself: it reads the caller's identity from the token and their
/// <b>authority from the database</b>, finds the project the request is about, asks
/// <see cref="PermissionEvaluator"/>, and — on a grant — tells <see cref="IAuditContext"/> which
/// access path admitted it, so the trail can say by what authority (KAFF-116). The rule itself lives
/// in Domain as a pure function, so it can be tested exhaustively without an HTTP context and cannot
/// be quietly amended by an endpoint.
/// </para>
/// <para>
/// <b>The token supplies identity; the database supplies authority.</b> Only the user id is taken
/// from the principal. Role, department, sub-department, client scope and whether the account is
/// still active are re-read on every authorized request through
/// <see cref="IPermissionSubjectReader"/> — including for company-wide permissions, which name no
/// project and so never reach <see cref="IProjectAccessPolicy"/>. That gap is what let a deactivated
/// user keep <c>UserManage</c> and <c>TreasuryPostCompany</c> until their token expired. See
/// decisions.md D-048.
/// </para>
/// <para>
/// A refusal is logged with the decision reason. "Forbidden" with no explanation is the failure mode
/// that turns into a two-hour support call.
/// </para>
/// </remarks>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionSubjectReader _subjectReader;
    private readonly IProjectAccessPolicy _projectAccessPolicy;
    private readonly IAuditContext _auditContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        ICurrentUser currentUser,
        IPermissionSubjectReader subjectReader,
        IProjectAccessPolicy projectAccessPolicy,
        IAuditContext auditContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _currentUser = currentUser;
        _subjectReader = subjectReader;
        _auditContext = auditContext;
        _projectAccessPolicy = projectAccessPolicy;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        CancellationToken cancellationToken =
            _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        PermissionSubject? subject = await BuildSubjectAsync(cancellationToken);

        Guid? projectId = ResolveProjectId(requirement.Scope);

        ProjectAccess? access = null;
        if (subject is not null && projectId is not null)
        {
            access = await _projectAccessPolicy.EvaluateAsync(subject, projectId.Value, cancellationToken);
        }

        PermissionDecision decision = PermissionEvaluator.Evaluate(subject, requirement.Permission, projectId, access);

        if (decision == PermissionDecision.Granted)
        {
            // D-075. Who the trail names, taken from the row this handler has just read out of the
            // users table rather than from the token's claims — which is what stops the trail and the
            // permission system disagreeing about the same user on the same request. A grant always
            // has a subject: PermissionEvaluator refuses a null one before anything else.
            _auditContext.ActorVerifiedAs(
                new AuditActor(subject!.UserId, subject.FullName, subject.Role));

            // KAFF-116. The gate is the only place that knows by what authority this request reached
            // the project, and the Owner's authority leaves no row anywhere to reconstruct it from.
            // Handing the policy's own answer to the audit context — rather than letting the
            // interceptor work it out again — keeps one source of truth for it.
            if (access is not null)
            {
                _auditContext.GrantedThrough(access.Path);
            }

            context.Succeed(requirement);
            return;
        }

        _logger.LogInformation(
            "Refused {Permission} for user {UserId} on project {ProjectId}: {Decision}.",
            requirement.Permission,
            subject?.UserId,
            projectId,
            decision);

        // Not calling Fail(): another handler may satisfy the same requirement through a different
        // policy. Simply not succeeding is the correct way to decline.
    }

    /// <summary>
    /// The caller's authority, read from the database on every request.
    /// </summary>
    /// <remarks>
    /// The token contributes exactly one value: the user id. Everything the permission rule consults
    /// — role, department, sub-department, client scope — is loaded fresh, so deactivating a user,
    /// changing their role, or moving them between departments takes effect on their next request
    /// rather than when their token happens to expire.
    ///
    /// Note this makes the claims-versus-database comparison that used to live in
    /// <see cref="IProjectAccessPolicy"/> unnecessary: no decision made here consults a claimed role.
    ///
    /// <b>The claimed role does not stop existing, though</b> — a token issued before a role change
    /// still carries the old one, and until decisions.md D-075 the audit trail read it. That is why
    /// the row this method returns is handed to <see cref="IAuditContext.ActorVerifiedAs"/> on a
    /// grant: the trail and the gate must not be able to describe the same request differently.
    /// </remarks>
    private async Task<PermissionSubject?> BuildSubjectAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return null;
        }

        return await _subjectReader.ReadAsync(
            _currentUser.UserId.Value,
            _currentUser.SecurityStamp,
            cancellationToken);
    }

    private Guid? ResolveProjectId(ProjectScope scope)
    {
        HttpContext? http = _httpContextAccessor.HttpContext;

        if (http is null || scope.Source == ProjectScopeSource.None)
        {
            return null;
        }

        string? raw = scope.Source switch
        {
            ProjectScopeSource.Route => http.Request.RouteValues.TryGetValue(scope.Key, out object? value)
                ? value?.ToString()
                : null,
            ProjectScopeSource.Query => http.Request.Query.TryGetValue(scope.Key, out Microsoft.Extensions.Primitives.StringValues value)
                ? value.ToString()
                : null,
            _ => null,
        };

        return Guid.TryParse(raw, out Guid projectId) ? projectId : null;
    }
}
