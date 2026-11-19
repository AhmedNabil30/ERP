using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>D-240 item 3: a typed posting date is the site day or earlier, never later, and never before 2000.</summary>
public sealed class TypedPostingDateTests
{
    private static readonly DateOnly SiteToday = new(2026, 11, 19);

    [Theory]
    [InlineData(-365, true)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void A_typed_posting_date_may_be_the_site_day_or_earlier_and_never_later(int daysFromToday, bool accepted)
    {
        TypedPostingDate.Check(SiteToday.AddDays(daysFromToday), SiteToday).IsSuccess.Should().Be(accepted);
    }

    [Fact]
    public void A_date_before_2000_or_a_missing_date_is_refused()
    {
        TypedPostingDate.Check(new DateOnly(1999, 12, 31), SiteToday).Error.Should().Be(TreasuryErrors.PostingDateOutOfRange);
        TypedPostingDate.Check(null, SiteToday).Error.Should().Be(TreasuryErrors.PostingDateRequired);
    }
}
