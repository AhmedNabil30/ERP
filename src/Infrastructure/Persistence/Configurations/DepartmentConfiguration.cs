using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaff.Infrastructure.Persistence.Configurations;

/// <summary>
/// KAFF-321 — <see cref="Department"/> as master data, seeded with the five rows decisions.md D-162
/// (<c>Q85</c>) names, at the fixed ids <see cref="WellKnownDepartments"/> pins the HR/Operations
/// business rules to.
/// </summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(department => department.Id);

        builder.Property(department => department.NameAr).IsRequired().HasMaxLength(Department.MaxNameLength);
        builder.Property(department => department.NameEn).IsRequired().HasMaxLength(Department.MaxNameLength);
        builder.Property(department => department.IsActive).IsRequired();

        // Seeded via HasData rather than a runtime seeder: the test harness builds its schema with
        // EnsureCreated and never runs a migration, the same precedent ClientCodeSequence documents in
        // KaffDbContext.OnModelCreating. AC-321-A: "Given a fresh database, when seeding runs, then
        // Finance, Technical Office, Operations, Procurement and HR all exist."
        builder.HasData(
            SeedRow(WellKnownDepartments.FinanceId, "المالية", "Finance"),
            SeedRow(WellKnownDepartments.TechnicalOfficeId, "المكتب الفني", "Technical Office"),
            SeedRow(WellKnownDepartments.OperationsId, "العمليات", "Operations"),
            SeedRow(WellKnownDepartments.ProcurementId, "المشتريات", "Procurement"),
            SeedRow(WellKnownDepartments.HrId, "الموارد البشرية", "HR"));
    }

    /// <summary>
    /// A seed row as the anonymous shape <c>HasData</c> needs — <see cref="Department"/> has no public
    /// constructor for a seed value to call, the same reason every other seeded row in EF Core is
    /// expressed this way rather than through the entity's own factory.
    /// </summary>
    private static object SeedRow(Guid id, string nameAr, string nameEn) => new
    {
        Id = id,
        NameAr = nameAr,
        NameEn = nameEn,
        IsActive = true,
    };
}
