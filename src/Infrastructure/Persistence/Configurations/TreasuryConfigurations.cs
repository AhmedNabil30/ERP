using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Accounts, table =>
        {
            // A structural roll-up node cannot also be a ledger; the two ideas are mutually exclusive.
            table.HasCheckConstraint(
                "ck_accounts_ledger_is_postable",
                "ledger_kind IS NULL OR is_postable = TRUE");

            // A party sub-ledger names both a type and an identifier, or neither.
            table.HasCheckConstraint(
                "ck_accounts_party_complete",
                "(party_type IS NULL AND party_id IS NULL) OR (party_type IS NOT NULL AND party_id IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_accounts_closed_after_opened",
                "closed_on IS NULL OR closed_on >= opened_on");
        });

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Code)
            .IsRequired()
            .HasMaxLength(Account.MaxCodeLength);

        builder.Property(account => account.NameAr).IsRequired().HasMaxLength(Account.MaxNameLength);
        builder.Property(account => account.NameEn).IsRequired().HasMaxLength(Account.MaxNameLength);

        builder.Property(account => account.Type).IsRequired();
        builder.Property(account => account.Class).IsRequired();
        builder.Property(account => account.NormalBalance).IsRequired();
        builder.Property(account => account.Currency).IsRequired();
        builder.Property(account => account.IsPostable).IsRequired();
        builder.Property(account => account.EnforceNonNegative).IsRequired();
        builder.Property(account => account.IsActive).IsRequired();
        builder.Property(account => account.OpenedOn).IsRequired();

        builder.HasIndex(account => account.Code)
            .IsUnique()
            .HasDatabaseName("ux_accounts_code");

        // The two dimensions of spec.md §6.3: project × party.
        builder.HasIndex(account => new { account.ProjectId, account.Type })
            .HasDatabaseName("ix_accounts_project_type");

        builder.HasIndex(account => new { account.PartyType, account.PartyId })
            .HasDatabaseName("ix_accounts_party");

        builder.HasIndex(account => account.LedgerKind)
            .HasDatabaseName("ix_accounts_ledger_kind");

        // Self-referencing tree. Restrict, never cascade: deleting an account that carries history is
        // not a thing this system does.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(account => account.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Postings, table =>
        {
            // spec.md §6.1 — direction is the account pair, never the sign.
            table.HasCheckConstraint("ck_postings_amount_positive", "amount > 0");

            table.HasCheckConstraint("ck_postings_distinct_accounts", "from_account_id <> to_account_id");

            table.HasCheckConstraint("ck_postings_not_self_reversing", "reverses_id IS NULL OR reverses_id <> id");
        });

        builder.HasKey(posting => posting.Id);

        // Computed conveniences over the mapped columns. Ignored explicitly rather than left to
        // convention, so a value object the provider has no converter for can never reach the model.
        builder.Ignore(posting => posting.SourceDocument);
        builder.Ignore(posting => posting.IsReversal);
        builder.Ignore(posting => posting.Nature);

        builder.Property(posting => posting.PostingDate).IsRequired();

        // Precision comes from the Money convention in KaffDbContext.ConfigureConventions: decimal(18,4).
        builder.Property(posting => posting.Amount).IsRequired();

        builder.Property(posting => posting.Type).IsRequired();
        builder.Property(posting => posting.SourceDocumentType).IsRequired();
        builder.Property(posting => posting.SourceDocumentId).IsRequired();
        builder.Property(posting => posting.SourceDocumentReference).HasMaxLength(SourceDocument.MaxReferenceLength);
        builder.Property(posting => posting.CreatedByUserId).IsRequired();
        builder.Property(posting => posting.CreatedAt).IsRequired();

        // These two indexes are what make the balances view an index-only aggregate rather than a
        // table scan, and what keeps the non-negative guard cheap enough to run inside a trigger.
        builder.HasIndex(posting => new { posting.FromAccountId, posting.PostingDate })
            .HasDatabaseName("ix_postings_from_account_date");

        builder.HasIndex(posting => new { posting.ToAccountId, posting.PostingDate })
            .HasDatabaseName("ix_postings_to_account_date");

        builder.HasIndex(posting => new { posting.ProjectId, posting.PostingDate })
            .HasDatabaseName("ix_postings_project_date");

        // Grouping the several postings one extract produces into a single readable movement.
        builder.HasIndex(posting => new { posting.SourceDocumentType, posting.SourceDocumentId })
            .HasDatabaseName("ix_postings_source_document");

        // spec.md §6.1 — a posting is corrected once. A second reversal of the same posting would
        // double-count the correction.
        builder.HasIndex(posting => posting.ReversesId)
            .IsUnique()
            .HasFilter("reverses_id IS NOT NULL")
            .HasDatabaseName("ux_postings_reverses");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(posting => posting.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(posting => posting.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Posting>()
            .WithMany()
            .HasForeignKey(posting => posting.ReversesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.AccountingPeriods, table =>
        {
            table.HasCheckConstraint("ck_accounting_periods_month", "month BETWEEN 1 AND 12");
            table.HasCheckConstraint("ck_accounting_periods_range", "ends_on >= starts_on");
        });

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Year).IsRequired();
        builder.Property(period => period.Month).IsRequired();
        builder.Property(period => period.StartsOn).IsRequired();
        builder.Property(period => period.EndsOn).IsRequired();
        builder.Property(period => period.Status).IsRequired();

        builder.HasIndex(period => new { period.Year, period.Month })
            .IsUnique()
            .HasDatabaseName("ux_accounting_periods_year_month");

        // The closed-period trigger scans this by date range on every posting insert.
        builder.HasIndex(period => new { period.Status, period.StartsOn, period.EndsOn })
            .HasDatabaseName("ix_accounting_periods_status_range");
    }
}

/// <summary>
/// Maps the read-only balances view.
/// </summary>
/// <remarks>
/// Keyless and view-backed, so EF cannot be persuaded to write to it. The view is defined in
/// <c>Persistence/Sql/002_views.sql</c>.
/// </remarks>
internal sealed class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasNoKey();
        builder.ToView(DbTables.AccountBalancesView);

        builder.Property(balance => balance.AccountCode).HasMaxLength(Account.MaxCodeLength);
        builder.Property(balance => balance.NameAr).HasMaxLength(Account.MaxNameLength);
        builder.Property(balance => balance.NameEn).HasMaxLength(Account.MaxNameLength);
    }
}
