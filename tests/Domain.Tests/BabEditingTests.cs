using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-204's domain half for <c>Bab.Rename</c> — new behaviour this story adds.</summary>
public sealed class BabEditingTests
{
    [Fact]
    public void Names_can_be_corrected()
    {
        Bab bab = NewBab();

        bab.Rename("باب معدل", "Renamed bab").IsSuccess.Should().BeTrue();

        bab.NameAr.Should().Be("باب معدل");
        bab.NameEn.Should().Be("Renamed bab");
    }

    [Theory]
    [InlineData(null, "Bab")]
    [InlineData("", "Bab")]
    [InlineData("   ", "Bab")]
    [InlineData("باب", null)]
    [InlineData("باب", "")]
    [InlineData("باب", "   ")]
    public void A_blank_name_on_either_side_is_refused_and_changes_nothing(string? nameAr, string? nameEn)
    {
        Bab bab = NewBab();

        Result result = bab.Rename(nameAr!, nameEn!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.NameRequired);
        bab.NameAr.Should().Be("باب", "a refused rename changes nothing");
        bab.NameEn.Should().Be("Bab");
    }

    [Fact]
    public void Renaming_does_not_touch_the_markup_or_the_parent()
    {
        Bab bab = NewBab();
        bab.SetDefaultMarkup(Percentage.FromPercent(11m));

        bab.Rename("باب آخر", "Another bab");

        bab.DefaultMarkup.Should().Be(Percentage.FromPercent(11m), "KAFF-204: a rename touches names only");
        bab.ParentBabId.Should().BeNull();
    }

    private static Bab NewBab() => Bab.Create("B-EDIT", "باب", "Bab", Percentage.FromPercent(15m)).Value;
}
