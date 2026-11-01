using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class DailyLogConfiguration : IEntityTypeConfiguration<DailyLog>
{
    public void Configure(EntityTypeBuilder<DailyLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.DailyLogs);
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.LogDate).IsRequired();
        builder.Property(log => log.CreatedAt).IsRequired();

        // spec.md section 8, rule 1: one log per engineer per project per day. A second open is the same log.
        builder.HasIndex(log => new { log.ProjectId, log.EngineerId, log.LogDate })
            .IsUnique()
            .HasDatabaseName("ux_daily_logs_project_engineer_date");

        builder.HasOne<Project>().WithMany().HasForeignKey(log => log.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(log => log.EngineerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Shared mapping of every entry kind. OccurredAt and CreatedAt are separate columns on all of them.</summary>
internal static class LogEntryMapping
{
    public static void Map<TEntry>(EntityTypeBuilder<TEntry> builder, string table)
        where TEntry : LogEntry
    {
        builder.ToTable(table);
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.Property(entry => entry.CreatedAt).IsRequired();

        builder.HasIndex(entry => entry.DailyLogId);
        builder.HasOne<DailyLog>().WithMany().HasForeignKey(entry => entry.DailyLogId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(entry => entry.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExecutedQuantityEntryConfiguration : IEntityTypeConfiguration<ExecutedQuantityEntry>
{
    public void Configure(EntityTypeBuilder<ExecutedQuantityEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.ExecutedQuantityEntries);
        builder.Property(entry => entry.Quantity).HasPrecision(18, 4).IsRequired();
        builder.HasIndex(entry => new { entry.DailyLogId, entry.BoqLineId });

        // Deliberately no foreign key to boq_lines. A key would make TRUNCATE boq_lines fail on the key before
        // kaff_boq_lines_reject_truncate (003_boq_freeze.sql) could refuse it, hiding the freeze guard's own
        // message. The line cannot be deleted from a frozen BOQ anyway, and the handler only records against
        // a line it has just locked.
    }
}

internal sealed class DayLabourEntryConfiguration : IEntityTypeConfiguration<DayLabourEntry>
{
    public void Configure(EntityTypeBuilder<DayLabourEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.DayLabourEntries);
        builder.Property(entry => entry.Hours).HasPrecision(18, 4).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(entry => entry.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MaterialEntryConfiguration : IEntityTypeConfiguration<MaterialEntry>
{
    public void Configure(EntityTypeBuilder<MaterialEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.MaterialEntries);
        builder.Property(entry => entry.MaterialType).IsRequired().HasMaxLength(MaterialEntry.MaxLength);
        builder.Property(entry => entry.Unit).IsRequired().HasMaxLength(MaterialEntry.MaxLength);
        builder.Property(entry => entry.Quantity).HasPrecision(18, 4).IsRequired();
    }
}

internal sealed class PhotoEntryConfiguration : IEntityTypeConfiguration<PhotoEntry>
{
    public void Configure(EntityTypeBuilder<PhotoEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.PhotoEntries);
        builder.Property(entry => entry.FileReference).IsRequired().HasMaxLength(DailyLog.MaxTextLength);
        builder.Property(entry => entry.Caption).HasMaxLength(DailyLog.MaxTextLength);
    }
}

internal sealed class FieldEntryConfiguration : IEntityTypeConfiguration<FieldEntry>
{
    public void Configure(EntityTypeBuilder<FieldEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.FieldEntries);
        builder.Property(entry => entry.Text).HasMaxLength(DailyLog.MaxTextLength);
    }
}

internal sealed class SiteExpenseDraftConfiguration : IEntityTypeConfiguration<SiteExpenseDraft>
{
    public void Configure(EntityTypeBuilder<SiteExpenseDraft> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        LogEntryMapping.Map(builder, DbTables.SiteExpenseDrafts);
        builder.Property(entry => entry.Description).IsRequired().HasMaxLength(DailyLog.MaxTextLength);

        // Money: precision comes from the Money convention (18, 4) in KaffDbContext.ConfigureConventions.
        builder.Property(entry => entry.Amount).IsRequired();
    }
}
