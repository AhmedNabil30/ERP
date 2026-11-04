using Kaff.Domain.Collections;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class CollectionRecordConfiguration : IEntityTypeConfiguration<CollectionRecord>
{
    public void Configure(EntityTypeBuilder<CollectionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.CollectionRecords);
        builder.HasKey(collection => collection.Id);
        builder.Property(collection => collection.Id).ValueGeneratedNever();
        builder.Property(collection => collection.Method).IsRequired();
        builder.Property(collection => collection.Date).IsRequired();
        builder.Property(collection => collection.Reference).IsRequired().HasMaxLength(CollectionRecord.MaxReferenceLength);

        // Money takes (18, 4) from the convention in KaffDbContext.ConfigureConventions. What was received, never a balance.
        builder.Property(collection => collection.CashReceived).IsRequired();
        builder.Property(collection => collection.WithheldAtSource).IsRequired();
        builder.Property(collection => collection.RecordedAt).IsRequired();

        builder.HasIndex(collection => collection.ExtractId);
        builder.HasOne<Extract>().WithMany().HasForeignKey(collection => collection.ExtractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(collection => collection.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(collection => collection.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Cheques);
        builder.HasKey(cheque => cheque.Id);
        builder.Property(cheque => cheque.Id).ValueGeneratedNever();
        builder.Property(cheque => cheque.State).IsRequired();
        builder.Property(cheque => cheque.StateChangedAt).IsRequired();

        builder.HasIndex(cheque => cheque.CollectionId).IsUnique();
        builder.HasOne<CollectionRecord>().WithMany().HasForeignKey(cheque => cheque.CollectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(cheque => cheque.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(cheque => cheque.StateChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
