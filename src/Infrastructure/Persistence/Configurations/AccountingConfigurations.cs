using Kaff.Domain.Accounting;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class AssetRegisterEntryConfiguration : IEntityTypeConfiguration<AssetRegisterEntry>
{
    public void Configure(EntityTypeBuilder<AssetRegisterEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.AssetRegisterEntries);

        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).ValueGeneratedNever();
        builder.Property(asset => asset.Name).IsRequired().HasMaxLength(AssetRegisterEntry.MaxNameLength);
        builder.Property(asset => asset.Category).IsRequired();
        builder.Property(asset => asset.Cost).IsRequired();
        builder.Property(asset => asset.Rate).IsRequired();
        builder.Property(asset => asset.PurchaseDate).IsRequired();
        builder.Property(asset => asset.CreatedAt).IsRequired();
        builder.Ignore(asset => asset.MonthlyCharge);

        builder.HasOne<Account>().WithMany().HasForeignKey(asset => asset.AccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(asset => asset.DepreciationExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(asset => asset.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccrualConfiguration : IEntityTypeConfiguration<Accrual>
{
    public void Configure(EntityTypeBuilder<Accrual> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Accruals);
        builder.HasKey(accrual => accrual.Id);
        builder.Property(accrual => accrual.Id).ValueGeneratedNever();
        builder.Property(accrual => accrual.Amount).IsRequired();
        builder.Property(accrual => accrual.AccruedOn).IsRequired();
        builder.Property(accrual => accrual.Description).HasMaxLength(Accrual.MaxDescriptionLength);
        builder.Property(accrual => accrual.CreatedAt).IsRequired();
        builder.Ignore(accrual => accrual.IsOpen);

        builder.HasIndex(accrual => accrual.ReleasedOn).HasDatabaseName("ix_accruals_released_on");

        builder.HasOne<Project>().WithMany().HasForeignKey(accrual => accrual.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(accrual => accrual.AccruedExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(accrual => accrual.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(accrual => accrual.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(accrual => accrual.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PrepaymentScheduleConfiguration : IEntityTypeConfiguration<PrepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<PrepaymentSchedule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.PrepaymentSchedules);

        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.Id).ValueGeneratedNever();
        builder.Property(schedule => schedule.TotalAmount).IsRequired();
        builder.Property(schedule => schedule.Months).IsRequired();
        builder.Property(schedule => schedule.FirstMonth).IsRequired();
        builder.Property(schedule => schedule.Description).HasMaxLength(Accrual.MaxDescriptionLength);
        builder.Property(schedule => schedule.CreatedAt).IsRequired();

        // One schedule per funding posting: the same payment cannot be released to expense twice.
        builder.HasIndex(schedule => schedule.FundingPostingId).IsUnique().HasDatabaseName("ux_prepayment_schedules_funding");

        builder.HasOne<Project>().WithMany().HasForeignKey(schedule => schedule.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(schedule => schedule.PrepaymentAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(schedule => schedule.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Posting>().WithMany().HasForeignKey(schedule => schedule.FundingPostingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(schedule => schedule.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BudgetBaselineConfiguration : IEntityTypeConfiguration<BudgetBaseline>
{
    public void Configure(EntityTypeBuilder<BudgetBaseline> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.BudgetBaselines);
        builder.HasKey(baseline => baseline.Id);
        builder.Property(baseline => baseline.Id).ValueGeneratedNever();
        builder.Property(baseline => baseline.CostTotal).IsRequired();
        builder.Property(baseline => baseline.Tolerance).IsRequired();
        builder.Property(baseline => baseline.SetAt).IsRequired();
        builder.Ignore(baseline => baseline.Limit);

        // SourceBoqId is deliberately a plain column, not a foreign key: the baseline is a copy, and nothing that happens to
        // the BOQ later may reach it (CLAUDE.md, frozen by copy).
        builder.HasIndex(baseline => baseline.ProjectId).IsUnique().HasDatabaseName("ux_budget_baselines_project");

        builder.HasOne<Project>().WithMany().HasForeignKey(baseline => baseline.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(baseline => baseline.SetByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountingRequestConfiguration : IEntityTypeConfiguration<AccountingRequest>
{
    public void Configure(EntityTypeBuilder<AccountingRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.AccountingRequests);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Kind).IsRequired();
        builder.Property(request => request.Status).IsRequired();
        builder.Property(request => request.Payload).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.RejectionReason).HasMaxLength(Project.MaxReasonLength);

        builder.HasIndex(request => new { request.Status, request.RequestedAt }).HasDatabaseName("ix_accounting_requests_status");

        builder.Property(request => request.SubjectKey).HasMaxLength(100);
        builder.HasIndex(request => new { request.Kind, request.SubjectKey })
            .IsUnique()
            .HasFilter("status = 'Requested' AND subject_key IS NOT NULL")
            .HasDatabaseName("ux_accounting_requests_waiting_subject");

        builder.HasOne<Project>().WithMany().HasForeignKey(request => request.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
