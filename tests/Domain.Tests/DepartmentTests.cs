using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-321's domain half — <c>Department.Create</c>, <c>Rename</c> and <c>Archive</c>. Mirrors
/// <c>BabEditingTests</c>'s shape, the closest existing archive-not-delete master record.
/// </summary>
public sealed class DepartmentTests
{
    [Fact]
    public void A_department_is_created_active()
    {
        Result<Department> created = Department.Create("المالية", "Finance");

        created.IsSuccess.Should().BeTrue();
        created.Value.NameAr.Should().Be("المالية");
        created.Value.NameEn.Should().Be("Finance");
        created.Value.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "Finance")]
    [InlineData("", "Finance")]
    [InlineData("   ", "Finance")]
    [InlineData("المالية", null)]
    [InlineData("المالية", "")]
    [InlineData("المالية", "   ")]
    public void A_blank_name_on_either_side_is_refused(string? nameAr, string? nameEn)
    {
        Result<Department> created = Department.Create(nameAr!, nameEn!);

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(MasterDataErrors.NameRequired);
    }

    [Fact]
    public void Names_can_be_corrected()
    {
        Department department = NewDepartment();

        department.Rename("قسم معدل", "Renamed department").IsSuccess.Should().BeTrue();

        department.NameAr.Should().Be("قسم معدل");
        department.NameEn.Should().Be("Renamed department");
    }

    [Fact]
    public void A_blank_rename_is_refused_and_changes_nothing()
    {
        Department department = NewDepartment();

        Result result = department.Rename("", "Whatever");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.NameRequired);
        department.NameAr.Should().Be("قسم", "a refused rename changes nothing");
    }

    [Fact]
    public void A_department_is_archived()
    {
        Department department = NewDepartment();

        department.Archive().IsSuccess.Should().BeTrue();

        department.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Archiving_twice_is_refused()
    {
        Department department = NewDepartment();
        department.Archive();

        Result result = department.Archive();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.AlreadyArchived);
    }

    private static Department NewDepartment() => Department.Create("قسم", "Department").Value;
}
