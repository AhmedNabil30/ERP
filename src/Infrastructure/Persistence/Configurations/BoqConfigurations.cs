using Kaff.Domain.Boqs;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class BoqConfiguration : IEntityTypeConfiguration<Boq>
{
    public void Configure(EntityTypeBuilder<Boq> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Boqs);

        builder.HasKey(boq => boq.Id);

        // Ids are generated in the domain (D-004). Declared never-generated so a line discovered through
        // the Lines navigation is inserted, not treated as an existing row and updated.
        builder.Property(boq => boq.Id).ValueGeneratedNever();
        builder.Ignore(boq => boq.IsFrozen);

        builder.Property(boq => boq.CreatedAt).IsRequired();

        // One BOQ per project.
        builder.HasIndex(boq => boq.ProjectId).IsUnique().HasDatabaseName("ux_boqs_project");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(boq => boq.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(boq => boq.Lines)
            .WithOne()
            .HasForeignKey(line => line.BoqId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(boq => boq.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// BOQ lines. <b>There is deliberately no foreign key to the catalogue or to the باب tables</b>
/// (spec.md §4.4): the line carries copies, so nothing a catalogue edit does can reach a signed
/// contract. <c>SchemaInvariantTests</c> pins that the table has no such key.
/// </summary>
internal sealed class BoqLineConfiguration : IEntityTypeConfiguration<BoqLine>
{
    public void Configure(EntityTypeBuilder<BoqLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.BoqLines, table =>
        {
            table.HasCheckConstraint("ck_boq_lines_prices_not_negative", "cost_price >= 0 AND base_sell_rate >= 0 AND sell_rate >= 0");
            table.HasCheckConstraint(
                "ck_boq_lines_quantities_not_negative",
                "(quantity_estimated IS NULL OR quantity_estimated >= 0) "
                + "AND (quantity_surveyed IS NULL OR quantity_surveyed >= 0) AND quantity_executed >= 0");

            // spec.md §4.5: only a custom line waits in the review queue.
            table.HasCheckConstraint("ck_boq_lines_review_only_custom", "NOT pending_catalogue_review OR is_custom");
        });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.ItemCode).HasMaxLength(BoqLine.MaxCodeLength);
        builder.Property(line => line.DescriptionAr).IsRequired().HasMaxLength(BoqLine.MaxDescriptionLength);
        builder.Property(line => line.DescriptionEn).HasMaxLength(BoqLine.MaxDescriptionLength);
        builder.Property(line => line.Unit).IsRequired().HasMaxLength(BoqLine.MaxUnitLength);
        builder.Property(line => line.BabCode).IsRequired();
        builder.Property(line => line.BabNameAr).IsRequired();
        builder.Property(line => line.BabNameEn).IsRequired();
        builder.Property(line => line.CostPrice).IsRequired();
        builder.Property(line => line.BaseSellRate).IsRequired();
        builder.Property(line => line.ConditionsRate).IsRequired();
        builder.Property(line => line.LineMarkup).IsRequired();
        builder.Property(line => line.SellRate).IsRequired();
        builder.Property(line => line.QuantityExecuted).IsRequired();

        builder.HasIndex(line => new { line.BoqId, line.Position }).HasDatabaseName("ix_boq_lines_boq_position");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(line => line.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// A prepared base BOQ (KAFF-418). Not a contract, so a base line may name its catalogue item by key; the
/// restrict rule keeps a referenced catalogue row from being deleted under a base.
/// </summary>
internal sealed class BoqBaseConfiguration : IEntityTypeConfiguration<BoqBase>
{
    public void Configure(EntityTypeBuilder<BoqBase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.BoqBases);
        builder.HasKey(boqBase => boqBase.Id);
        builder.Property(boqBase => boqBase.Id).ValueGeneratedNever();
        builder.Ignore(boqBase => boqBase.IsArchived);

        builder.Property(boqBase => boqBase.Name).IsRequired().HasMaxLength(BoqBase.MaxNameLength);
        builder.Property(boqBase => boqBase.CreatedAt).IsRequired();

        builder.HasMany(boqBase => boqBase.Lines)
            .WithOne()
            .HasForeignKey(line => line.BoqBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(boqBase => boqBase.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BoqBaseLineConfiguration : IEntityTypeConfiguration<BoqBaseLine>
{
    public void Configure(EntityTypeBuilder<BoqBaseLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.BoqBaseLines, table =>
            table.HasCheckConstraint("ck_boq_base_lines_quantity_not_negative", "quantity >= 0"));

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Quantity).IsRequired();

        builder.HasIndex(line => new { line.BoqBaseId, line.Position }).HasDatabaseName("ix_boq_base_lines_base_position");

        builder.HasOne<Kaff.Domain.MasterData.CatalogueItem>()
            .WithMany()
            .HasForeignKey(line => line.CatalogueItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
