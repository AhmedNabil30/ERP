using Kaff.Domain.Adjustments;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class AdjustmentConfiguration : IEntityTypeConfiguration<Adjustment>
{
    public void Configure(EntityTypeBuilder<Adjustment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Adjustments);
        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.Id).ValueGeneratedNever();
        builder.Property(adjustment => adjustment.Number).IsRequired();
        builder.Property(adjustment => adjustment.Reason).IsRequired().HasMaxLength(Adjustment.MaxReasonLength);
        builder.Property(adjustment => adjustment.DecisionReason).HasMaxLength(Adjustment.MaxReasonLength);
        builder.Property(adjustment => adjustment.SourceDocumentReference).HasMaxLength(SourceDocument.MaxReferenceLength);

        // Money takes (18, 4) from the convention in KaffDbContext.ConfigureConventions. The amount is what was
        // asked for; no balance is stored. What it did to the books is in the postings it wrote.
        builder.Property(adjustment => adjustment.Amount).IsRequired();
        builder.Property(adjustment => adjustment.RaisedAt).IsRequired();

        builder.HasIndex(adjustment => new { adjustment.ProjectId, adjustment.Number })
            .IsUnique()
            .HasDatabaseName("ux_adjustments_project_number");
        builder.HasIndex(adjustment => new { adjustment.SourceDocumentType, adjustment.SourceDocumentId });

        builder.HasOne<Project>().WithMany().HasForeignKey(adjustment => adjustment.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(adjustment => adjustment.TargetAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(adjustment => adjustment.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(adjustment => adjustment.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(adjustment => adjustment.SourceDocument);
        builder.Ignore(adjustment => adjustment.PostingSource);
    }
}
