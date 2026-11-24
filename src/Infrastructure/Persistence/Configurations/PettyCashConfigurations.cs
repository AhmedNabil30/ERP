using Kaff.Domain.Identity;
using Kaff.Domain.PettyCash;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class PettyCashRequestConfiguration : IEntityTypeConfiguration<PettyCashRequest>
{
    public void Configure(EntityTypeBuilder<PettyCashRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.PettyCashRequests);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();

        // Money takes (18, 4) from the convention in KaffDbContext.ConfigureConventions. The amount is what was asked for;
        // no balance is stored.
        builder.Property(request => request.Amount).IsRequired();
        builder.Property(request => request.Purpose).IsRequired().HasMaxLength(PettyCashRequest.MaxPurposeLength);
        builder.Property(request => request.CreatedAt).IsRequired();
        builder.Property(request => request.ReturnReason).HasMaxLength(Project.MaxReasonLength);

        builder.HasIndex(request => new { request.ProjectId, request.Status });

        builder.HasOne<Project>().WithMany().HasForeignKey(request => request.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
