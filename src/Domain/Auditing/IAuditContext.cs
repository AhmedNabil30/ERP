using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Auditing;

/// <summary>
/// What happened, when the thing that happened is not a change to an entity.
/// </summary>
/// <remarks>
/// Sign-out changes no row; a sign-in against an account that is already clean changes none either.
/// Both are facts the trail must carry, so they are named here rather than inferred from the change
/// tracker. Adding a member is a one-line, backfill-free change — the column that stores it is
/// <see cref="AuditRecord.EventType"/> and it lands with the mechanism, which is the part that
/// cannot be added after the first consumer. See decisions.md D-061.
/// </remarks>
public enum AuditEventKind
{
    SignedIn = 1,
    SignedOut = 2,
}

/// <summary>
/// An event a handler declares. The interceptor turns it into an <see cref="AuditRecord"/>; no
/// handler ever constructs one itself.
/// </summary>
public sealed record AuditEvent(AuditEventKind Kind, string SubjectType, Guid SubjectId);

/// <summary>
/// Who a record names.
/// </summary>
/// <remarks>
/// <para>
/// Normally <see cref="IAuditContext.VerifiedActor"/> — the row the authorization gate read from the
/// users table on this request. <b>Never the token's claims</b>: see
/// <see cref="IAuditContext.ActorVerifiedAs"/> and decisions.md D-075.
/// </para>
/// <para>
/// <see cref="IAuditContext.AttributeTo"/> is the other source and has one caller: bootstrap
/// (KAFF-100), where the Owner is created by the very transaction being audited and the endpoint is
/// anonymous by definition. Left alone it would put a null actor on the first row of the trail, which
/// is the outcome D-051 (Q31) rejected the seed to avoid.
/// </para>
/// <para>
/// <see cref="UserId"/> and <see cref="Role"/> are null together, for the one case where there is
/// genuinely no actor: work outside a request — migrations, seeding, scheduled jobs
/// (<c>SystemCurrentUser</c>). <b>A named actor without a role — or a role over nobody — is refused
/// by the database</b>, which is the whole of <c>ck_audit_records_actor_is_named_completely</c>:
/// <c>(actor_user_id IS NULL) = (actor_role IS NULL)</c>. A check constraint rather than
/// <c>IsRequired()</c> on the role, because that one genuinely roleless case must stay legal.
/// </para>
/// </remarks>
public sealed record AuditActor(Guid? UserId, string DisplayName, Role? Role);

/// <summary>
/// Per-request state the audit interceptor needs but cannot infer from the change tracker.
/// </summary>
/// <remarks>
/// A handler that performs a rejection, a reversal or any other movement that spec.md requires a
/// reason for calls <see cref="SetReason"/> before saving. spec.md §7: "Any rejection at any gate
/// returns the extract to Draft with a written reason and full audit trail. Never a silent
/// step-back."
///
/// The reason is set on the unit of work rather than passed through every entity method, so adding a
/// new state transition cannot accidentally bypass it. <see cref="Record{TSubject}"/> and
/// <see cref="AttributeTo"/> are the same arrangement for the same reason: the handler states a
/// fact, the one mechanism decides what is written.
/// </remarks>
public interface IAuditContext
{
    /// <summary>Groups every audit record written during one request.</summary>
    Guid CorrelationId { get; }

    /// <summary>The reason supplied for the change now being saved, if any.</summary>
    string? Reason { get; }

    /// <summary>The request path, recorded for traceability.</summary>
    string? RequestPath { get; }

    /// <summary>
    /// How the access policy admitted this request to the project it named, or null when it named
    /// none. Written onto <see cref="AuditRecord.GrantPath"/> by the interceptor.
    /// </summary>
    ProjectAccessPath? GrantPath { get; }

    /// <summary>
    /// Records the path the access policy granted.
    /// </summary>
    /// <remarks>
    /// Called by the authorization gate and by nothing else — it states what the policy already
    /// decided, so that the interceptor does not have to derive it a second time. KAFF-116 rule 6: a
    /// second derivation is a second source of truth and would disagree eventually.
    /// <para>
    /// Set once per request, alongside <see cref="CorrelationId"/> and <see cref="RequestPath"/>, and
    /// deliberately <b>not</b> discarded by <see cref="Clear"/>: how the caller reached the project is
    /// a fact about the request, not about one save within it.
    /// </para>
    /// </remarks>
    void GrantedThrough(ProjectAccessPath path);

    /// <summary>
    /// Who the authorization gate verified this caller to be, read from the users table, or null when
    /// no gate ran on this request.
    /// </summary>
    /// <remarks>
    /// See <see cref="ActorVerifiedAs"/> for why the trail takes the actor from here and not from the
    /// token.
    /// </remarks>
    AuditActor? VerifiedActor { get; }

    /// <summary>
    /// Records the actor exactly as the gate read them from the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called by the authorization gate on a grant, and by nothing else — the same arrangement as
    /// <see cref="GrantedThrough"/> and for the same reason (KAFF-116 rule 6): the gate has already
    /// read the caller's row, so a second read here would be a second source of truth.
    /// </para>
    /// <para>
    /// <b>This is what keeps the trail and the permission system from disagreeing about the same user
    /// on the same request.</b> D-048 stopped the gate trusting the token because claims go stale;
    /// until decisions.md D-075 the audit trail still believed them, so a role change would have
    /// attributed an act to a role the gate had already stopped honouring — permanently, in a table
    /// that is append-only by trigger.
    /// </para>
    /// <para>
    /// Set once per request alongside <see cref="CorrelationId"/>, and deliberately <b>not</b>
    /// discarded by <see cref="Clear"/>: who the caller is is a fact about the request, not about one
    /// save within it.
    /// </para>
    /// </remarks>
    void ActorVerifiedAs(AuditActor actor);

    /// <summary>Events declared for the next save, in the order they were declared.</summary>
    IReadOnlyList<AuditEvent> Events { get; }

    /// <summary>The declared actor, or null when the request's own identity is the actor.</summary>
    AuditActor? Actor { get; }

    /// <summary>
    /// Records why this change is being made. Applies to every audit record written by the next save.
    /// </summary>
    void SetReason(string reason);

    /// <summary>
    /// Declares that something happened to <typeparamref name="TSubject"/> that no entity change
    /// describes. The record is written by the interceptor on the next save, in the same transaction
    /// and under the same correlation id as anything else that save writes.
    /// </summary>
    void Record<TSubject>(AuditEventKind kind, Guid subjectId)
        where TSubject : Entity;

    /// <summary>
    /// Names the actor for the next save. Legal only on a request that carries no identity — the
    /// interceptor refuses it otherwise, because an authenticated caller naming a different actor is
    /// impersonation written into an append-only table.
    /// </summary>
    void AttributeTo(AuditActor actor);

    /// <summary>
    /// Discards the reason, the declared events and the declared actor. Called by the interceptor
    /// once a save has consumed them: they describe the change just saved, not whatever the request
    /// does next.
    /// </summary>
    void Clear();
}
