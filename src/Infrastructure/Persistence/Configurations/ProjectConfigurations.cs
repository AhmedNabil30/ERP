using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Projects, table =>
        {
            // spec.md §5 — terms belong to exactly one contract type. Cost Plus has no hold and no
            // تشوينات; Design has no BOQ at all. The database says so as well as the domain.
            table.HasCheckConstraint(
                "ck_projects_lump_sum_terms",
                "contract_type = 'LumpSum' OR (advance_rate IS NULL AND hold_rate IS NULL "
                + "AND advance_recovery_rate IS NULL AND material_advance_rate IS NULL)");

            table.HasCheckConstraint(
                "ck_projects_cost_plus_terms",
                "contract_type = 'CostPlus' OR supervision_rate IS NULL");

            table.HasCheckConstraint(
                "ck_projects_design_terms",
                "contract_type = 'Design' OR (area_square_metres IS NULL AND design_rate_per_square_metre IS NULL)");

            table.HasCheckConstraint(
                "ck_projects_area_positive",
                "area_square_metres IS NULL OR area_square_metres > 0");

            table.HasCheckConstraint(
                "ck_projects_not_linked_to_itself",
                "linked_project_id IS NULL OR linked_project_id <> id");

            table.HasCheckConstraint(
                "ck_projects_link_complete",
                "(linked_project_id IS NULL AND link_type IS NULL) "
                + "OR (linked_project_id IS NOT NULL AND link_type IS NOT NULL)");

            // spec.md §8 and §13 — a stoppage and a termination both carry a reason.
            table.HasCheckConstraint(
                "ck_projects_stoppage_reason",
                "stopped_on IS NULL OR stoppage_reason IS NOT NULL");

            table.HasCheckConstraint(
                "ck_projects_termination_reason",
                "terminated_on IS NULL OR termination_reason IS NOT NULL");
        });

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Code).IsRequired().HasMaxLength(Project.MaxCodeLength);
        builder.Property(project => project.Name).IsRequired().HasMaxLength(Project.MaxNameLength);
        builder.Property(project => project.ContractType).IsRequired();
        builder.Property(project => project.Status).IsRequired();
        builder.Property(project => project.Currency).IsRequired();
        builder.Property(project => project.DelayPenaltyEnabled).IsRequired();

        // spec.md §6.7, on the contract rather than the client — Karim, 2026-08-21, D-049.
        builder.Property(project => project.WithholdingCategory).IsRequired();
        builder.Property(project => project.StoppageReason).HasMaxLength(Project.MaxReasonLength);
        builder.Property(project => project.TerminationReason).HasMaxLength(Project.MaxReasonLength);
        builder.Property(project => project.CreatedAt).IsRequired();

        builder.Ignore(project => project.IsSigned);
        builder.Ignore(project => project.CanIssueExtracts);

        builder.HasIndex(project => project.Code).IsUnique().HasDatabaseName("ux_projects_code");
        builder.HasIndex(project => project.ClientId).HasDatabaseName("ix_projects_client");
        builder.HasIndex(project => project.Status).HasDatabaseName("ix_projects_status");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(project => project.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Opportunity>()
            .WithMany()
            .HasForeignKey(project => project.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(project => project.LinkedProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Opportunities, table =>
            // spec.md §3: "Closed Lost MUST record a reason."
            table.HasCheckConstraint(
                "ck_opportunities_closed_lost_reason",
                "status <> 'ClosedLost' OR closed_lost_reason IS NOT NULL"));

        builder.HasKey(opportunity => opportunity.Id);

        builder.Property(o => o.Code).IsRequired().HasMaxLength(Opportunity.MaxCodeLength);
        builder.Property(o => o.Title).IsRequired().HasMaxLength(Opportunity.MaxTitleLength);
        builder.Property(o => o.Stage).IsRequired();
        builder.Property(o => o.Status).IsRequired();
        builder.Property(o => o.ClosedLostReason).HasMaxLength(Opportunity.MaxReasonLength);
        builder.Property(o => o.LastActivityAt).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.HasIndex(o => o.Code).IsUnique().HasDatabaseName("ux_opportunities_code");
        builder.HasIndex(o => o.ClientId).HasDatabaseName("ix_opportunities_client");

        // Drives the inactivity alerts of spec.md §3 (day 2, day 4, day 7).
        builder.HasIndex(o => new { o.Status, o.LastActivityAt }).HasDatabaseName("ix_opportunities_activity");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(o => o.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
