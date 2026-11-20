using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class ExtractConfiguration : IEntityTypeConfiguration<Extract>
{
    public void Configure(EntityTypeBuilder<Extract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Extracts);
        builder.HasKey(extract => extract.Id);

        // Ids are generated in the domain (D-004); declared never-generated so children are inserted, not updated.
        builder.Property(extract => extract.Id).ValueGeneratedNever();
        builder.Property(extract => extract.Number).IsRequired();
        builder.Property(extract => extract.PeriodStart).IsRequired();
        builder.Property(extract => extract.PeriodEnd).IsRequired();
        builder.Property(extract => extract.CreatedAt).IsRequired();

        // Money and Percentage take their precision from the conventions in KaffDbContext.ConfigureConventions
        // (18, 4) and (18, 6). No balance is stored: these are the evidence and a snapshot of one calculation.
        builder.Property(extract => extract.CumulativeCertifiedWork).IsRequired();
        builder.Property(extract => extract.PreviouslyCertifiedWork).IsRequired();
        builder.Property(extract => extract.MaterialValueOnSite).IsRequired();
        builder.Property(extract => extract.MaterialInstalled).IsRequired();
        builder.Property(extract => extract.ApprovedPortion).IsRequired();

        builder.HasIndex(extract => new { extract.ProjectId, extract.Number })
            .IsUnique()
            .HasDatabaseName("ux_extracts_project_number");

        // D-241 item 2: a cancelled extract has at most one revision.
        builder.HasIndex(extract => extract.RevisesExtractId)
            .IsUnique()
            .HasFilter("revises_extract_id IS NOT NULL")
            .HasDatabaseName("ux_extracts_revises_extract_id");
        builder.HasOne<Extract>().WithMany().HasForeignKey(extract => extract.RevisesExtractId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>().WithMany().HasForeignKey(extract => extract.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(extract => extract.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(extract => extract.GateHistory)
            .WithOne()
            .HasForeignKey(record => record.ExtractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(extract => extract.GateHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(extract => extract.Figures)
            .WithOne()
            .HasForeignKey(figure => figure.ExtractId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(extract => extract.Figures).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(extract => extract.ChangeOrderLines)
            .WithOne()
            .HasForeignKey(line => line.ExtractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(extract => extract.ChangeOrderLines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(extract => extract.SourceDocument);
        builder.Ignore(extract => extract.ChangeOrderValue);
    }
}

internal sealed class ExtractGateRecordConfiguration : IEntityTypeConfiguration<ExtractGateRecord>
{
    public void Configure(EntityTypeBuilder<ExtractGateRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ExtractGateRecords);
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.OccurredAt).IsRequired();
        builder.Property(record => record.Reason).HasMaxLength(Project.MaxReasonLength);
        builder.HasIndex(record => record.ExtractId);
        builder.HasOne<User>().WithMany().HasForeignKey(record => record.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExtractFigureConfiguration : IEntityTypeConfiguration<ExtractFigure>
{
    public void Configure(EntityTypeBuilder<ExtractFigure> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ExtractFigures);
        builder.HasKey(figure => figure.Id);
        builder.Property(figure => figure.Id).ValueGeneratedNever();
        builder.Property(figure => figure.Amount).IsRequired();
        builder.Property(figure => figure.LabelKey).IsRequired().HasMaxLength(DbLimits.EnumLength * 2);
        builder.HasIndex(figure => figure.ExtractId);
    }
}

internal sealed class ExtractQuantityLineConfiguration : IEntityTypeConfiguration<ExtractQuantityLine>
{
    public void Configure(EntityTypeBuilder<ExtractQuantityLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ExtractQuantityLines);
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Ignore(line => line.CurrentQuantity);

        // D-248: one row per BOQ line per take; a revision of the evidence adds a take and changes no row.
        builder.HasIndex(line => new { line.ExtractId, line.Take, line.BoqLineId })
            .IsUnique()
            .HasDatabaseName("ux_extract_quantity_lines_extract_take_line");
        builder.HasOne<Extract>().WithMany().HasForeignKey(line => line.ExtractId).OnDelete(DeleteBehavior.Restrict);

        // No foreign key to boq_lines: one would make TRUNCATE boq_lines fail on the key before the freeze guard
        // (KAFF_BOQ_FROZEN) answers it, and the guard already keeps a frozen line, the only kind an extract reads, in place.
    }
}

internal sealed class NcrConfiguration : IEntityTypeConfiguration<Ncr>
{
    public void Configure(EntityTypeBuilder<Ncr> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Ncrs);
        builder.HasKey(ncr => ncr.Id);
        builder.Property(ncr => ncr.Id).ValueGeneratedNever();
        builder.Property(ncr => ncr.OpenedAt).IsRequired();
        builder.Property(ncr => ncr.CloseNote).HasMaxLength(Project.MaxReasonLength);
        builder.Property(ncr => ncr.Description).HasMaxLength(Project.MaxReasonLength);

        // AC-500-E: an extract holds against one open NCR at a time; a second split of the same extract reuses it.
        builder.HasIndex(ncr => ncr.ExtractId)
            .IsUnique()
            .HasFilter("status = 'Open'")
            .HasDatabaseName("ux_ncrs_open_extract");
        builder.HasIndex(ncr => ncr.ProjectId);

        builder.HasOne<Project>().WithMany().HasForeignKey(ncr => ncr.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Extract>().WithMany().HasForeignKey(ncr => ncr.ExtractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(ncr => ncr.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(ncr => ncr.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
