using Kaff.Domain.Closure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class SnagConfiguration : IEntityTypeConfiguration<Snag>
{
    public void Configure(EntityTypeBuilder<Snag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Snags);
        builder.HasKey(snag => snag.Id);
        builder.Property(snag => snag.Id).ValueGeneratedNever();
        builder.Property(snag => snag.Number).IsRequired();
        builder.Property(snag => snag.Description).IsRequired().HasMaxLength(Snag.MaxDescriptionLength);
        builder.Property(snag => snag.Location).IsRequired().HasMaxLength(Snag.MaxLocationLength);
        builder.Property(snag => snag.ResolutionReason).HasMaxLength(Project.MaxReasonLength);
        builder.Property(snag => snag.RaisedAt).IsRequired();

        builder.HasIndex(snag => new { snag.ProjectId, snag.Number })
            .IsUnique()
            .HasDatabaseName("ux_snags_project_number");

        builder.HasOne<Project>().WithMany().HasForeignKey(snag => snag.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subcontractor>().WithMany().HasForeignKey(snag => snag.SubcontractorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(snag => snag.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(snag => snag.FixedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(snag => snag.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(snag => snag.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(snag => snag.BlocksHandover);
    }
}

internal sealed class WarrantyCallbackConfiguration : IEntityTypeConfiguration<WarrantyCallback>
{
    public void Configure(EntityTypeBuilder<WarrantyCallback> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.WarrantyCallbacks);
        builder.HasKey(callback => callback.Id);
        builder.Property(callback => callback.Id).ValueGeneratedNever();
        builder.Property(callback => callback.Number).IsRequired();
        builder.Property(callback => callback.Description).IsRequired().HasMaxLength(WarrantyCallback.MaxDescriptionLength);
        builder.Property(callback => callback.RaisedOn).IsRequired();
        builder.Property(callback => callback.RaisedAt).IsRequired();

        builder.HasIndex(callback => new { callback.ProjectId, callback.Number })
            .IsUnique()
            .HasDatabaseName("ux_warranty_callbacks_project_number");

        builder.HasOne<Project>().WithMany().HasForeignKey(callback => callback.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(callback => callback.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(callback => callback.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(callback => callback.IsResolved);
    }
}
