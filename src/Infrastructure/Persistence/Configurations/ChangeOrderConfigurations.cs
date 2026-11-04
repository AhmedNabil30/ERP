using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class ChangeOrderConfiguration : IEntityTypeConfiguration<ChangeOrder>
{
    public void Configure(EntityTypeBuilder<ChangeOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ChangeOrders);
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.Number).IsRequired();
        builder.Property(order => order.Title).IsRequired().HasMaxLength(ChangeOrder.MaxTitleLength);
        builder.Property(order => order.CreatedAt).IsRequired();

        builder.HasIndex(order => new { order.ProjectId, order.Number })
            .IsUnique()
            .HasDatabaseName("ux_change_orders_project_number");

        builder.HasOne<Project>().WithMany().HasForeignKey(order => order.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.ChangeOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(order => order.GateHistory)
            .WithOne()
            .HasForeignKey(record => record.ChangeOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(order => order.GateHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The price is the sum of the lines, derived on read. No total is stored.
        builder.Ignore(order => order.Total);
    }
}

internal sealed class ChangeOrderLineConfiguration : IEntityTypeConfiguration<ChangeOrderLine>
{
    public void Configure(EntityTypeBuilder<ChangeOrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ChangeOrderLines);
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Description).IsRequired().HasMaxLength(ChangeOrderLine.MaxDescriptionLength);
        builder.Property(line => line.Unit).IsRequired().HasMaxLength(ChangeOrderLine.MaxUnitLength);

        // Money takes (18, 4) from the convention in KaffDbContext.ConfigureConventions; Quantity is a bare
        // decimal, which the same file gives (18, 4). These are the copied values of a priced line, not a balance.
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.UnitPrice).IsRequired();
        builder.Property(line => line.Total).IsRequired();
        builder.HasIndex(line => line.ChangeOrderId);
    }
}

internal sealed class ChangeOrderGateRecordConfiguration : IEntityTypeConfiguration<ChangeOrderGateRecord>
{
    public void Configure(EntityTypeBuilder<ChangeOrderGateRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ChangeOrderGateRecords);
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.OccurredAt).IsRequired();
        builder.Property(record => record.Reason).HasMaxLength(Project.MaxReasonLength);
        builder.HasIndex(record => record.ChangeOrderId);
        builder.HasOne<User>().WithMany().HasForeignKey(record => record.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExtractChangeOrderLineConfiguration : IEntityTypeConfiguration<ExtractChangeOrderLine>
{
    public void Configure(EntityTypeBuilder<ExtractChangeOrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.ExtractChangeOrderLines);
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Amount).IsRequired();
        builder.HasIndex(line => line.ExtractId);

        // ChangeOrderBillingBasis: a change order is billed whole, on one extract. The database refuses a second.
        builder.HasIndex(line => line.ChangeOrderId)
            .IsUnique()
            .HasDatabaseName("ux_extract_change_order_lines_change_order");
        builder.HasOne<ChangeOrder>().WithMany().HasForeignKey(line => line.ChangeOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
