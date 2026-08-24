using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Users, table =>
        {
            // spec.md §9 — only Operations subdivides.
            table.HasCheckConstraint(
                "ck_users_operations_sub_department",
                "(department = 'Operations' AND operations_sub_department IS NOT NULL) "
                + "OR (department IS DISTINCT FROM 'Operations' AND operations_sub_department IS NULL)");

            // spec.md §12 — a portal user is scoped to exactly one client; nobody else carries one.
            table.HasCheckConstraint(
                "ck_users_client_scope",
                "(role = 'Client' AND client_id IS NOT NULL) OR (role <> 'Client' AND client_id IS NULL)");

            // spec.md §9 — "Subcontractor (record only, no login)."
            table.HasCheckConstraint(
                "ck_users_subcontractor_cannot_log_in",
                "role <> 'Subcontractor' OR password_hash IS NULL");
        });

        builder.HasKey(user => user.Id);

        // The entered form and the normalised form are the mapped columns; Phone composes them.
        builder.Ignore(user => user.Phone);

        builder.Property(user => user.UserName).IsRequired().HasMaxLength(128);
        builder.Property(user => user.FullName).IsRequired().HasMaxLength(200);
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.PhoneEntered).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(user => user.PhoneNormalised).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(user => user.Role).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512);
        builder.Property(user => user.SecurityStamp).IsRequired().HasMaxLength(64);

        // spec.md §9 amendment (decisions.md D-049 rulings 3 and 4). None of the three is money, so
        // no precision applies; the two non-nullable ones default in the database as well as in the
        // constructor, so an existing row backfills to the safe value — not forced to change a
        // password it was never issued, and not part-way through a failure run.
        builder.Property(user => user.MustChangePassword).IsRequired().HasDefaultValue(false);
        builder.Property(user => user.FailedSignInAttempts).IsRequired().HasDefaultValue(0);
        builder.Property(user => user.LockedOutUntil);

        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();

        builder.HasIndex(user => user.UserName)
            .IsUnique()
            .HasDatabaseName("ux_users_user_name");

        builder.HasIndex(user => user.PhoneNormalised)
            .HasDatabaseName("ix_users_phone_normalised");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(user => user.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(user => user.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ProjectAssignments, table =>
            table.HasCheckConstraint(
                "ck_project_assignments_revocation_complete",
                "(revoked_at IS NULL AND revoked_by_user_id IS NULL) "
                + "OR (revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL)"));

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Level).IsRequired();
        builder.Property(assignment => assignment.AssignedAt).IsRequired();
        builder.Property(assignment => assignment.AssignedByUserId).IsRequired();

        builder.Ignore(assignment => assignment.IsActive);

        // One live assignment per user per project. Revoked rows stay, so the audit trail can answer
        // who was able to act on a project on the day a movement was approved.
        builder.HasIndex(assignment => new { assignment.ProjectId, assignment.UserId })
            .IsUnique()
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ux_project_assignments_active");

        // The permission handler's lookup path, on every project-scoped request.
        builder.HasIndex(assignment => new { assignment.UserId, assignment.ProjectId })
            .HasDatabaseName("ix_project_assignments_user_project");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
