using System.Reflection;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-207 (the register) and KAFF-208 (nobody appears in both populations) — the domain half.
/// </summary>
public sealed class EmployeeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    // ---- AC-207-A · a salaried employee is created -------------------------------------------

    [Fact]
    public void A_salaried_employee_is_created_active_with_no_bab_required()
    {
        Result<Employee> result = Employee.Create(
            "E-10001", "Ahmed Ali", Phone("01012345671"), EmployeeKind.Salaried, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(EmployeeKind.Salaried);
        result.Value.IsActive.Should().BeTrue();
    }

    // ---- AC-207-B · day labour without a باب is refused (entity half; the DB half is Api.Tests) ----

    [Fact]
    public void Day_labour_with_no_bab_is_refused()
    {
        Result<Employee> result = Employee.Create(
            "E-10002", "Worker", Phone("01012345672"), EmployeeKind.DayLabour, Now, babId: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.DayLabourRequiresTrade);
    }

    [Fact]
    public void Day_labour_with_a_bab_is_created()
    {
        Result<Employee> result = Employee.Create(
            "E-10003", "Worker", Phone("01012345673"), EmployeeKind.DayLabour, Now, babId: Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(EmployeeKind.DayLabour);
    }

    // ---- AC-207-F · archived, not deleted -------------------------------------------------------

    [Fact]
    public void Archiving_deactivates_and_a_second_archive_is_refused()
    {
        Employee employee = NewSalariedEmployee();

        employee.Archive().IsSuccess.Should().BeTrue();
        employee.IsActive.Should().BeFalse();

        Result second = employee.Archive();
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(MasterDataErrors.AlreadyArchived);
    }

    // ---- AC-207-E · the register stores no pay figure --------------------------------------------

    [Fact]
    public void The_stored_properties_carry_no_money_typed_member()
    {
        // Allow-list, written out by name — the same discipline AC-207-E and AC-208-D both ask for:
        // adding a member is a deliberate edit to this test, not a silent widening.
        string[] allowedProperties =
        [
            nameof(Employee.Id),
            nameof(Employee.Code),
            nameof(Employee.FullName),
            nameof(Employee.PhoneEntered),
            nameof(Employee.PhoneNormalised),
            nameof(Employee.Phone),
            nameof(Employee.Kind),
            nameof(Employee.BabId),
            nameof(Employee.Specialty),
            nameof(Employee.NationalId),
            nameof(Employee.Department),
            nameof(Employee.JobTitle),
            nameof(Employee.HiredOn),
            nameof(Employee.IsActive),
            nameof(Employee.CreatedAt),
        ];

        PropertyInfo[] properties = typeof(Employee).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        properties.Select(property => property.Name).Should().BeEquivalentTo(
            allowedProperties,
            "AC-207-E: no salary, day rate, wage or other money-typed member may be added without this "
            + "test being edited to name it");

        properties.Should().NotContain(
            property => property.PropertyType == typeof(Money) || property.PropertyType == typeof(Money?),
            "spec.md §10, D-055 §2, CLAUDE.md: the register stores no pay figure of any kind");
    }

    // ---- AC-208-A · every costed person is in exactly one population -----------------------------

    [Theory]
    [InlineData(EmployeeKind.Salaried)]
    [InlineData(EmployeeKind.DayLabour)]
    public void Kind_is_always_a_defined_enum_member_never_the_zero_value(EmployeeKind kind)
    {
        Employee employee = Employee.Create(
            "E-KIND", "Person", Phone("01012345674"), kind, Now, babId: kind == EmployeeKind.DayLabour ? Guid.NewGuid() : null).Value;

        Enum.IsDefined(employee.Kind).Should().BeTrue();
        employee.Kind.Should().NotBe(default(EmployeeKind), "the zero value is not a defined member of EmployeeKind");
    }

    // ---- AC-208-D · immutability is not merely an absent setter -----------------------------------

    [Fact]
    public void No_public_member_sets_Kind_after_construction()
    {
        // The allow-list of every public member Employee exposes. Kind's own property has a private
        // setter [Verified @ Employee.cs -> Kind], so the only way this could be broken is a NEW
        // public method added beside Edit/Archive/SetStaffDetails that reaches Kind — this test names
        // every member that exists today so that addition is a deliberate edit to this list.
        string[] allowedPublicMembers =
        [
            nameof(Employee.Create),
            nameof(Employee.SetStaffDetails),
            nameof(Employee.Edit),
            nameof(Employee.Archive),
        ];

        MethodInfo[] publicMethods = typeof(Employee)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(method => !method.IsSpecialName) // excludes property get_/set_ accessors
            .Where(method => method.DeclaringType == typeof(Employee))
            .ToArray();

        publicMethods.Select(method => method.Name).Should().BeEquivalentTo(
            allowedPublicMembers,
            "AC-208-D: a SetKind (or equivalent) added beside these must be a deliberate edit to this "
            + "allow-list, not a silent widening");

        MethodInfo? kindSetter = typeof(Employee).GetProperty(nameof(Employee.Kind))!.SetMethod;

        (kindSetter is null || !kindSetter.IsPublic).Should().BeTrue(
            "Kind has no public setter — the whole of today's immutability is structural");
    }

    // ---- Employee.Edit (KAFF-207) — never touches Kind or Code -----------------------------------

    [Fact]
    public void Edit_corrects_name_bab_and_specialty_but_never_kind_or_code()
    {
        Employee employee = NewSalariedEmployee();
        string originalCode = employee.Code;

        Result edited = employee.Edit("New Name", Phone("01099999999"), babId: null, specialty: "Finishing");

        edited.IsSuccess.Should().BeTrue();
        employee.FullName.Should().Be("New Name");
        employee.Specialty.Should().Be("Finishing");
        employee.Code.Should().Be(originalCode, "Edit carries no path to Code — it is generated once, D-130 §6");
        employee.Kind.Should().Be(EmployeeKind.Salaried, "Edit carries no Kind parameter at all — KAFF-208 rule 3");
    }

    [Fact]
    public void Edit_refuses_a_blank_name_and_changes_nothing()
    {
        Employee employee = NewSalariedEmployee();
        string originalName = employee.FullName;

        Result edited = employee.Edit(" ", Phone("01099999998"), babId: null, specialty: null);

        edited.IsFailure.Should().BeTrue();
        edited.Error.Should().Be(MasterDataErrors.NameRequired);
        employee.FullName.Should().Be(originalName);
    }

    [Fact]
    public void Edit_still_requires_a_bab_for_day_labour()
    {
        Employee employee = Employee.Create(
            "E-DL-EDIT", "Worker", Phone("01099999997"), EmployeeKind.DayLabour, Now, babId: Guid.NewGuid()).Value;

        Result edited = employee.Edit("Worker", Phone("01099999996"), babId: null, specialty: null);

        edited.IsFailure.Should().BeTrue();
        edited.Error.Should().Be(MasterDataErrors.DayLabourRequiresTrade);
    }

    private static Employee NewSalariedEmployee() =>
        Employee.Create("E-10099", "Original Name", Phone("01055555555"), EmployeeKind.Salaried, Now).Value;

    private static PhoneNumber Phone(string number) => PhoneNumber.Create(number).Value;
}
