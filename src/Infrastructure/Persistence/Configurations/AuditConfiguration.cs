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
        });

        builder.HasKey(record => record.Id);

        builder.Property(record => record.OccurredAt).IsRequired();
        builder.Property(record => record.Action).IsRequired();
        builder.Property(record => record.EntityType).IsRequired().HasMaxLength(128);
        builder.Property(record => record.EntityId).IsRequired();
        builder.Property(record => record.ActorDisplayName).IsRequired().HasMaxLength(200);
        builder.Property(record => record.CorrelationId).IsRequired();
        builder.Property(record => record.RequestPath).HasMaxLength(512);

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
