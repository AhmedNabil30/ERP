using Kaff.Domain.Auditing;
using Kaff.Infrastructure.Persistence.Constants;
using Kaff.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.AuditRecords, table =>
        {
            // A record with neither a before nor an after state describes nothing — unless it names
            // an event, whose whole content is that it happened. See decisions.md D-061.
            table.HasCheckConstraint(
                "ck_audit_records_has_state",
                "event_type IS NOT NULL OR before_json IS NOT NULL OR after_json IS NOT NULL");

            // A row is an entity change or an event, never a hybrid. Enums are stored as text here,
            // so the literal reads as the rule does; the pair is held together by
            // Only_an_Occurred_record_carries_an_event_type.
            table.HasCheckConstraint(
                "ck_audit_records_event_shape",
                "(action = 'Occurred') = (event_type IS NOT NULL)");

            // KAFF-116, and the database says it rather than a comment. Two rules, one constraint:
            //
            //   a grant path without a project names an authority over nothing — a company-wide act
            //   went through no access policy, so its path is null rather than 'OwnerGlobal';
            //
            //   'None' is the value a refusal carries, and a refusal writes no record at all. A row
            //   claiming it would be a grant that named its own absence.
            //
            // Held together by A_grant_path_is_refused_without_a_project_and_may_never_be_None.
            table.HasCheckConstraint(
                "ck_audit_records_grant_path",
                "grant_path IS NULL OR (project_id IS NOT NULL AND grant_path <> 'None')");

            // An actor is named completely or not at all. The one legitimate unnamed actor is work
            // outside a request — migrations, seeding, scheduled jobs (SystemCurrentUser) — and it
            // carries no role either. Everything else names a user, and a user without the role they
            // acted under is a permanently unattributed row in a table that is append-only by
            // trigger and has no correction path.
            //
            // A constraint rather than IsRequired() on ActorRole, because the column must stay
            // nullable for that one case: NOT NULL would refuse the system actor, which is the only
            // row shape that is genuinely roleless.
            //
            // A constraint rather than the AuditContext guard alone, because the guard sits on the
            // two channels that *declare* an actor and the interceptor's fallback constructs one
            // without passing through either. See decisions.md D-075. Held together by
            // An_actor_is_named_completely_or_not_at_all.
            table.HasCheckConstraint(
                "ck_audit_records_actor_is_named_completely",
                "(actor_user_id IS NULL) = (actor_role IS NULL)");

            // decisions.md D-063 §3. Dropping NOT NULL from entity_id silently permits an entity
            // change with no subject, which was impossible before — this is the point of the change,
            // not a side effect of it. An event (action = 'Occurred') may still name none.
            table.HasCheckConstraint(
                "ck_audit_records_entity_change_has_subject",
                "action = 'Occurred' OR entity_id IS NOT NULL");
        });

        builder.HasKey(record => record.Id);

        builder.Property(record => record.OccurredAt).IsRequired();
        builder.Property(record => record.Action).IsRequired();
        builder.Property(record => record.EntityType).IsRequired().HasMaxLength(128);

        // entity_id is nullable by CLR type (Guid?) and no IsRequired() call here — decisions.md
        // D-063 §3. It is still required for every entity change; that is enforced in the database by
        // ck_audit_records_entity_change_has_subject, not here, because the one legal exception (an
        // event) shares this same column.
        builder.Property(record => record.ActorDisplayName).IsRequired().HasMaxLength(200);
        builder.Property(record => record.CorrelationId).IsRequired();
        builder.Property(record => record.RequestPath).HasMaxLength(512);

        // System.Net.IPAddress maps natively to PostgreSQL's inet — no converter, no varchar. See
        // decisions.md D-063 §2. Null for work outside a request, same as RequestPath.
        builder.Property(record => record.IpAddress);

        // spec.md §7 requires a written reason on every rejection.
        builder.Property(record => record.Reason).HasMaxLength(DbLimits.LongTextLength);

        // jsonb rather than text: the trail must stay queryable — "show me every change that touched
        // this amount" is a real support question — without a second, parsed copy of the data.
        builder.Property(record => record.BeforeJson).HasColumnType("jsonb");
        builder.Property(record => record.AfterJson).HasColumnType("jsonb");

        builder.Property(record => record.ChangedProperties)
            .HasConversion(new StringListConverter(), new StringListComparer())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(record => new { record.EntityType, record.EntityId })
            .HasDatabaseName("ix_audit_records_entity");

        builder.HasIndex(record => record.OccurredAt)
            .HasDatabaseName("ix_audit_records_occurred_at");

        builder.HasIndex(record => record.ActorUserId)
            .HasDatabaseName("ix_audit_records_actor");

        builder.HasIndex(record => record.CorrelationId)
            .HasDatabaseName("ix_audit_records_correlation");

        builder.HasIndex(record => record.ProjectId)
            .HasDatabaseName("ix_audit_records_project");

        // No foreign key to users. The trail must survive a user record being changed or removed;
        // evidence that can be broken by a later edit elsewhere is not evidence. The actor's display
        // name is copied at the time of the change for the same reason.
    }
}
