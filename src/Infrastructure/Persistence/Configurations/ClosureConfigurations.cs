using Kaff.Domain.Closure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
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

internal sealed class HoldReleaseRequestConfiguration : IEntityTypeConfiguration<HoldReleaseRequest>
{
    public void Configure(EntityTypeBuilder<HoldReleaseRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.HoldReleaseRequests);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.RejectionReason).HasMaxLength(Project.MaxReasonLength);

        // At most one request waits for the Owner on a project; the domain asks first, this index is the record.
        builder.HasIndex(request => request.ProjectId)
            .IsUnique()
            .HasFilter("status = 'Requested'")
            .HasDatabaseName("ux_hold_release_requests_waiting");

        builder.HasOne<Project>().WithMany().HasForeignKey(request => request.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubcontractorRetentionReleaseRequestConfiguration : IEntityTypeConfiguration<SubcontractorRetentionReleaseRequest>
{
    public void Configure(EntityTypeBuilder<SubcontractorRetentionReleaseRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.SubcontractorRetentionReleaseRequests);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.RejectionReason).HasMaxLength(Project.MaxReasonLength);

        // At most one request waits for the Owner per subcontractor on a project; the domain asks first, this index is the record.
        builder.HasIndex(request => new { request.ProjectId, request.SubcontractorId })
            .IsUnique()
            .HasFilter("status = 'Requested'")
            .HasDatabaseName("ux_subcontractor_retention_release_requests_waiting");

        builder.HasOne<Project>().WithMany().HasForeignKey(request => request.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubcontractorRetentionReversalRequestConfiguration : IEntityTypeConfiguration<SubcontractorRetentionReversalRequest>
{
    public void Configure(EntityTypeBuilder<SubcontractorRetentionReversalRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.SubcontractorRetentionReversalRequests);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Reason).HasMaxLength(Project.MaxReasonLength).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.RejectionReason).HasMaxLength(Project.MaxReasonLength);

        // At most one reversal request waits for the Owner per subcontractor on a project; the domain asks first, this index is the record.
        builder.HasIndex(request => new { request.ProjectId, request.SubcontractorId })
            .IsUnique()
            .HasFilter("status = 'Requested'")
            .HasDatabaseName("ux_subcontractor_retention_reversal_requests_waiting");

        builder.HasOne<Project>().WithMany().HasForeignKey(request => request.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Posting>().WithMany().HasForeignKey(request => request.OriginalPostingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
