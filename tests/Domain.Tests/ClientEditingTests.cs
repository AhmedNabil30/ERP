using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-121's domain half — the mutation surface a client file did not have until 2026-09-04.
/// </summary>
/// <remarks>
/// <para>
/// KAFF-121 finding <b>F-09</b>: <c>Client</c> exposed <c>SetContactDetails</c>,
/// <c>SetTaxRegistration</c> and <c>Archive</c>, and nothing else. There was no setter for the name
/// and none for the primary phone, so <b>a mistyped client name was permanent</b> on a record
/// spec.md §2 requires to hold "full history". That is missing capability rather than a missing rule,
/// which is why it is built here rather than asked about.
/// </para>
/// <para>
/// These are entity tests on purpose. The endpoint half — the duplicate warning, the permission gate,
/// and what the trail contains afterwards — is in <c>Api.Tests/EditClientTests.cs</c>, because none
/// of it is observable from here.
/// </para>
/// </remarks>
public sealed class ClientEditingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 9, 0, 0, TimeSpan.Zero);

    // ---- KAFF-121 rule 2 · the name is editable ------------------------------------------------

    [Fact]
    public void A_name_can_be_corrected()
    {
        Client client = Corporate("شركة النور للمقاولت");

        client.Rename("شركة النور للمقاولات").IsSuccess.Should().BeTrue();

        client.Name.Should().Be("شركة النور للمقاولات");
    }

    [Fact]
    public void A_corrected_name_is_trimmed_exactly_as_a_registered_one_is()
    {
        Client client = Corporate("شركة النور");

        client.Rename("  شركة النيل  ").IsSuccess.Should().BeTrue();

        client.Name.Should().Be(
            "شركة النيل",
            "Create trims, so the edit path must too — a second, laxer route to one column is how an "
            + "invariant stops being one");
    }

    /// <summary>
    /// The conditions <see cref="Client.Create"/> applies, applied again on the way in.
    /// </summary>
    /// <remarks>
    /// A name that could never have been registered must not be reachable by editing into it. Both
    /// refusals carry <c>NameRequired</c>, which is the same error the create path returns for the
    /// same input — the caller sees one rule, not two that happen to agree today.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_cannot_be_edited_away(string? blank)
    {
        Client client = Corporate("شركة قائمة");

        Result renamed = client.Rename(blank);

        renamed.IsFailure.Should().BeTrue();
        renamed.Error.Should().Be(MasterDataErrors.NameRequired);
        client.Name.Should().Be("شركة قائمة", "a refused edit changes nothing");
    }

    [Fact]
    public void A_name_longer_than_the_column_is_refused_on_the_edit_path_too()
    {
        Client client = Corporate("شركة قائمة");

        Result renamed = client.Rename(new string('ن', Client.MaxNameLength + 1));

        renamed.IsFailure.Should().BeTrue();
        renamed.Error.Should().Be(MasterDataErrors.NameRequired);
        client.Name.Should().Be("شركة قائمة");
    }

    // ---- KAFF-121 rule 3 · the primary phone is editable ---------------------------------------

    [Fact]
    public void The_primary_phone_can_be_replaced_and_both_forms_move_together()
    {
        Client client = Corporate("شركة النيل", "01001234567");

        client.SetPrimaryPhone(PhoneNumber.Create("+20 111 222 3333").Value);

        client.PhoneEntered.Should().Be("+20 111 222 3333", "what the operator typed is what support calls");
        client.PhoneNormalised.Should().Be(
            "01112223333",
            "the deduplication key is derived, never typed — a stale key is a warning nobody sees");
    }

    /// <summary>
    /// spec.md §2, amended, and D-049 ruling 8: a repeated number warns and does not block.
    /// </summary>
    /// <remarks>
    /// The entity is where a refusal would be unanswerable, so the absence of one is asserted rather
    /// than assumed. Naming the client that already holds the number takes a query, and this type has
    /// no database — the warning belongs to the handler (KAFF-119 rule 4, KAFF-121 rule 4).
    /// </remarks>
    [Fact]
    public void The_entity_refuses_nothing_about_a_repeated_phone()
    {
        Client first = Corporate("العميل الأصلي", "01001234567");
        Client second = Corporate("المدير التنفيذي", "01009999999");

        second.SetPrimaryPhone(PhoneNumber.Create(first.PhoneEntered).Value);

        second.PhoneNormalised.Should().Be(
            first.PhoneNormalised,
            "Karim allowed two records on one number: a corporate client and its CEO");
    }

    // ---- KAFF-121 rule 6 · §6.7 constrains the PAIR --------------------------------------------

    [Fact]
    public void A_corporate_client_carrying_a_registration_number_cannot_become_an_individual()
    {
        Client client = Corporate("شركة النيل");
        client.SetTaxRegistration("123-456-789").IsSuccess.Should().BeTrue();

        Result reclassified = client.SetClassification(ClientKind.Individual, "123-456-789");

        reclassified.IsFailure.Should().BeTrue();
        reclassified.Error.Should().Be(MasterDataErrors.IndividualDoesNotWithhold);

        client.Kind.Should().Be(ClientKind.Corporate, "a refused change leaves both members alone");
        client.TaxRegistrationNumber.Should().Be("123-456-789");
    }

    [Fact]
    public void The_same_client_becomes_an_individual_the_moment_the_number_goes_with_it()
    {
        Client client = Corporate("شركة النيل");
        client.SetTaxRegistration("123-456-789").IsSuccess.Should().BeTrue();

        client.SetClassification(ClientKind.Individual, taxRegistrationNumber: null)
            .IsSuccess.Should().BeTrue(
                "clearing the number and changing the kind is one legal end state, and §6.7 "
                + "constrains the end state");

        client.Kind.Should().Be(ClientKind.Individual);
        client.TaxRegistrationNumber.Should().BeNull();
    }

    /// <summary>
    /// The case that a kind-then-number ordering would have refused for no reason.
    /// </summary>
    /// <remarks>
    /// Promoting an individual to a company that has a tax registration number is ordinary and legal.
    /// Two independent setters have to run in some order, and this direction is the one that breaks
    /// if the kind is written first — which is why <c>SetClassification</c> takes the pair.
    /// </remarks>
    [Fact]
    public void An_individual_can_become_a_company_and_take_a_registration_number_in_one_act()
    {
        Client client = Individual("أحمد محمود");

        client.SetClassification(ClientKind.Corporate, "987-654-321").IsSuccess.Should().BeTrue();

        client.Kind.Should().Be(ClientKind.Corporate);
        client.TaxRegistrationNumber.Should().Be("987-654-321");
    }

    /// <summary>
    /// <c>SetTaxRegistration</c> is <c>SetClassification</c> with the kind unchanged, and the
    /// registration path still goes through it.
    /// </summary>
    /// <remarks>
    /// It delegates rather than repeating the check. This test is what makes the delegation load
    /// bearing: reintroducing a second copy of §6.7 inside <c>SetTaxRegistration</c> would leave this
    /// green and would be exactly the drift KAFF-120 rule 5 warns about.
    /// </remarks>
    [Fact]
    public void Setting_a_registration_number_on_an_individual_is_still_refused_and_the_kind_never_moves()
    {
        Client client = Individual("أحمد محمود");

        Result set = client.SetTaxRegistration("123-456-789");

        set.IsFailure.Should().BeTrue();
        set.Error.Should().Be(MasterDataErrors.IndividualDoesNotWithhold);
        client.Kind.Should().Be(ClientKind.Individual, "recording a number never reclassifies anybody");
        client.TaxRegistrationNumber.Should().BeNull();
    }

    // ---- KAFF-121 rules 5 and 9 · what editing still cannot reach ------------------------------

    /// <summary>
    /// <c>AC-121-E</c> and rule 9, asserted where they are actually decided.
    /// </summary>
    /// <remarks>
    /// The code and the active flag are not editable because <c>Client</c> exposes no way to move
    /// them — <c>Archive</c> is a named act of its own (KAFF-123), not a field an edit can set. This
    /// enumerates the mutation surface, so a setter added later fails here rather than quietly
    /// widening what an edit can do.
    /// </remarks>
    [Fact]
    public void The_mutation_surface_is_exactly_these_methods_and_reaches_neither_the_code_nor_the_active_flag()
    {
        typeof(Client).GetMethods()
            .Where(method => method.DeclaringType == typeof(Client) && !method.IsSpecialName && method.IsPublic)
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                [
                    nameof(Client.Create), nameof(Client.Rename), nameof(Client.SetPrimaryPhone),
                    nameof(Client.SetContactDetails), nameof(Client.SetClassification),
                    nameof(Client.SetTaxRegistration), nameof(Client.Archive),
                ],
                "spec.md §2's amendment forbids editing a code, and KAFF-121 rule 9 keeps archiving a "
                + "separate act — both hold because there is no method that could do either");
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static Client Corporate(string name, string phone = "01001112222") =>
        Client.Create("C-90001", name, PhoneNumber.Create(phone).Value, ClientKind.Corporate, Now).Value;

    private static Client Individual(string name, string phone = "01003334444") =>
        Client.Create("C-90002", name, PhoneNumber.Create(phone).Value, ClientKind.Individual, Now).Value;
}
