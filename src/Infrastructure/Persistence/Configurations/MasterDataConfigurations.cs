using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Clients);
        builder.HasKey(client => client.Id);

        // The entered form and the normalised form are the mapped columns; Phone composes them.
        builder.Ignore(client => client.Phone);

        builder.Property(client => client.Code).IsRequired().HasMaxLength(Client.MaxCodeLength);
        builder.Property(client => client.Name).IsRequired().HasMaxLength(Client.MaxNameLength);
        builder.Property(client => client.PhoneEntered).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(client => client.PhoneNormalised).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(client => client.AlternatePhone).HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(client => client.Email).HasMaxLength(256);
        builder.Property(client => client.Address).HasMaxLength(DbLimits.LongTextLength);
        builder.Property(client => client.Notes).HasMaxLength(DbLimits.LongTextLength);
        builder.Property(client => client.Kind).IsRequired();
        builder.Property(client => client.TaxRegistrationNumber).HasMaxLength(64);
        builder.Property(client => client.IsActive).IsRequired();
        builder.Property(client => client.CreatedAt).IsRequired();

        // Codes are generated and never edited (Karim, 2026-08-21), so uniqueness here is an
        // assertion about the generator rather than a constraint on what a user may type.
        builder.HasIndex(client => client.Code).IsUnique().HasDatabaseName("ux_clients_code");

        // NOT unique. spec.md §2 says "deduplicated by phone" and §3 says "never create a duplicate
        // client", and this was a unique index until 2026-08-21 — which refused the save outright.
        // Karim ruled that the match is a warning, not a refusal: "a corporate client and its CEO
        // might be registered as two separate entities sharing the same contact number."
        //
        // The index stays because the warning needs the lookup, and it stays on the NORMALISED form
        // so +20 10 …, 0020 10 … and 010 … all match. A matcher that misses is now worse than
        // before: it used to mean a wrongly-accepted save, and it now means a warning nobody sees.
        // See decisions.md D-049.
        builder.HasIndex(client => client.PhoneNormalised).HasDatabaseName("ix_clients_phone");
    }
}

internal sealed class BabConfiguration : IEntityTypeConfiguration<Bab>
{
    public void Configure(EntityTypeBuilder<Bab> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Babs, table =>
            table.HasCheckConstraint("ck_babs_not_own_parent", "parent_bab_id IS NULL OR parent_bab_id <> id"));

        builder.HasKey(bab => bab.Id);

        builder.Property(bab => bab.Code).IsRequired().HasMaxLength(Bab.MaxCodeLength);
        builder.Property(bab => bab.NameAr).IsRequired().HasMaxLength(Bab.MaxNameLength);
        builder.Property(bab => bab.NameEn).IsRequired().HasMaxLength(Bab.MaxNameLength);

        // Precision from the Percentage convention: decimal(18,6), stored as a fraction.
        builder.Property(bab => bab.DefaultMarkup).IsRequired();
        builder.Property(bab => bab.SortOrder).IsRequired();
        builder.Property(bab => bab.IsActive).IsRequired();

        builder.HasIndex(bab => bab.Code).IsUnique().HasDatabaseName("ux_babs_code");

        builder.HasOne<Bab>()
            .WithMany()
            .HasForeignKey(bab => bab.ParentBabId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CatalogueItemConfiguration : IEntityTypeConfiguration<CatalogueItem>
{
    public void Configure(EntityTypeBuilder<CatalogueItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.CatalogueItems, table =>
        {
            table.HasCheckConstraint("ck_catalogue_items_cost_not_negative", "cost_price >= 0");
            table.HasCheckConstraint("ck_catalogue_items_rate_not_negative", "base_sell_rate >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Code).IsRequired().HasMaxLength(CatalogueItem.MaxCodeLength);
        builder.Property(item => item.DescriptionAr).IsRequired().HasMaxLength(CatalogueItem.MaxDescriptionLength);
        builder.Property(item => item.DescriptionEn).HasMaxLength(CatalogueItem.MaxDescriptionLength);
        builder.Property(item => item.Unit).IsRequired().HasMaxLength(CatalogueItem.MaxUnitLength);
        builder.Property(item => item.CostPrice).IsRequired();
        builder.Property(item => item.BaseSellRate).IsRequired();
        builder.Property(item => item.Status).IsRequired();

        builder.HasIndex(item => item.Code).IsUnique().HasDatabaseName("ux_catalogue_items_code");
        builder.HasIndex(item => item.BabId).HasDatabaseName("ix_catalogue_items_bab");

        builder.HasOne<Bab>()
            .WithMany()
            .HasForeignKey(item => item.BabId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Employees, table =>
            // spec.md §10 registers workers with a trade / باب.
            table.HasCheckConstraint("ck_employees_day_labour_has_trade", "kind <> 'DayLabour' OR bab_id IS NOT NULL"));

        builder.HasKey(employee => employee.Id);
        builder.Ignore(employee => employee.Phone);

        builder.Property(employee => employee.Code).IsRequired().HasMaxLength(Employee.MaxCodeLength);
        builder.Property(employee => employee.FullName).IsRequired().HasMaxLength(Employee.MaxNameLength);
        builder.Property(employee => employee.PhoneEntered).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(employee => employee.PhoneNormalised).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(employee => employee.Kind).IsRequired();
        builder.Property(employee => employee.Specialty).HasMaxLength(200);
        builder.Property(employee => employee.NationalId).HasMaxLength(32);
        builder.Property(employee => employee.JobTitle).HasMaxLength(128);
        builder.Property(employee => employee.IsActive).IsRequired();
        builder.Property(employee => employee.CreatedAt).IsRequired();

        builder.HasIndex(employee => employee.Code).IsUnique().HasDatabaseName("ux_employees_code");

        // spec.md §2: "every costed person, exactly one record". spec.md §10 deduplicates by phone.
        builder.HasIndex(employee => employee.PhoneNormalised).IsUnique().HasDatabaseName("ux_employees_phone");

        builder.HasOne<Bab>()
            .WithMany()
            .HasForeignKey(employee => employee.BabId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubcontractorConfiguration : IEntityTypeConfiguration<Subcontractor>
{
    public void Configure(EntityTypeBuilder<Subcontractor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Subcontractors);
        builder.HasKey(subcontractor => subcontractor.Id);
        builder.Ignore(subcontractor => subcontractor.Phone);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(Subcontractor.MaxCodeLength);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(Subcontractor.MaxNameLength);
        builder.Property(s => s.PhoneEntered).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(s => s.PhoneNormalised).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(s => s.RetentionRate).IsRequired();
        builder.Property(s => s.WithholdingCategory).IsRequired();
        builder.Property(s => s.TaxRegistrationNumber).HasMaxLength(64);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ux_subcontractors_code");
        builder.HasIndex(s => s.PhoneNormalised).IsUnique().HasDatabaseName("ux_subcontractors_phone");

        builder.HasOne<Bab>()
            .WithMany()
            .HasForeignKey(s => s.TradeBabId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(DbTables.Suppliers);
        builder.HasKey(supplier => supplier.Id);
        builder.Ignore(supplier => supplier.Phone);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(Supplier.MaxCodeLength);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(Supplier.MaxNameLength);
        builder.Property(s => s.PhoneEntered).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(s => s.PhoneNormalised).IsRequired().HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(s => s.WithholdingCategory).IsRequired();
        builder.Property(s => s.TaxRegistrationNumber).HasMaxLength(64);
        builder.Property(s => s.Address).HasMaxLength(DbLimits.LongTextLength);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ux_suppliers_code");
        builder.HasIndex(s => s.PhoneNormalised).IsUnique().HasDatabaseName("ux_suppliers_phone");
    }
}
