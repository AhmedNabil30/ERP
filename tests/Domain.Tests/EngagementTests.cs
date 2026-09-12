using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-210 — the domain half. decisions.md D-139 §3, D-140.
/// </summary>
public sealed class EngagementTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    // ---- AC-210-A · an engagement opens against a worker and a project -----------------------

    [Fact]
    public void An_engagement_opens_against_a_worker_and_a_project()
    {
        Guid workerId = Guid.CreateVersion7();
        Guid projectId = Guid.CreateVersion7();

        Result<Engagement> result = Engagement.Open(workerId, projectId, Today, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.WorkerId.Should().Be(workerId);
        result.Value.ProjectId.Should().Be(projectId);
        result.Value.IsOpen.Should().BeTrue();
        result.Value.ClosedOn.Should().BeNull();
        result.Value.ClosedAt.Should().BeNull();
    }

    // ---- rule 9 / Q76 · no route in this slice supplies a day rate ---------------------------

    [Fact]
    public void An_engagement_opens_with_no_day_rate_by_default()
    {
        Result<Engagement> result = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now);

        result.Value.DayRate.Should().BeNull("no route this slice maps supplies one — decisions.md D-140, Q76");
    }

    // ---- AC-210-C · the day rate keeps four decimals and never passes through a float --------

    [Fact]
    public void An_agreed_day_rate_keeps_four_decimals_exactly()
    {
        var dayRate = new Money(487.6543m);

        Result<Engagement> result = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now, dayRate);

        result.IsSuccess.Should().BeTrue();
        result.Value.DayRate.Should().Be(dayRate);
        result.Value.DayRate!.Value.Amount.Should().Be(487.6543m);
    }

    // ---- rule 4 · a day rate is what was agreed, never invented ------------------------------

    [Fact]
    public void A_non_positive_day_rate_is_refused()
    {
        Result<Engagement> result = Engagement.Open(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now, new Money(0m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.EngagementDayRateMustBePositive);
    }

    // ---- rule 6 · an engagement closes only by an explicit manual close ----------------------

    [Fact]
    public void An_open_engagement_closes()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;
        DateTimeOffset closedAt = Now.AddDays(10);
        DateOnly closedOn = DateOnly.FromDateTime(closedAt.UtcDateTime);

        Result result = engagement.Close(closedOn, closedAt);

        result.IsSuccess.Should().BeTrue();
        engagement.IsOpen.Should().BeFalse();
        engagement.ClosedOn.Should().Be(closedOn);
        engagement.ClosedAt.Should().Be(closedAt);
    }

    [Fact]
    public void A_closed_engagement_cannot_be_closed_again()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;
        engagement.Close(Today, Now);

        Result result = engagement.Close(Today, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.EngagementAlreadyClosed);
    }

    // ---- AC-210-F · a rating is out of 5, refused outside 1-5 --------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_rating_within_1_to_5_is_accepted(int rating)
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;

        Result result = engagement.Rate(rating);

        result.IsSuccess.Should().BeTrue();
        engagement.Rating.Should().Be(rating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void A_rating_outside_1_to_5_is_refused(int rating)
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;

        Result result = engagement.Rate(rating);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.EngagementRatingOutOfRange);
        engagement.Rating.Should().BeNull();
    }

    // ---- rule 8 · an unrated worker is a different fact from a rating of 0 -------------------

    [Fact]
    public void A_freshly_opened_engagement_is_unrated_not_zero()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;

        engagement.Rating.Should().BeNull();
    }

    // ---- decisions.md D-152 §2 (Q76), D-153 §1 point 6 · SetDayRate --------------------------

    [Fact]
    public void SetDayRate_records_the_agreed_rate_on_an_open_engagement()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;
        var rate = new Money(487.6543m);

        Result result = engagement.SetDayRate(rate);

        result.IsSuccess.Should().BeTrue();
        engagement.DayRate.Should().Be(rate);
    }

    [Fact]
    public void SetDayRate_refuses_a_non_positive_rate()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;

        Result result = engagement.SetDayRate(new Money(0m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.EngagementDayRateMustBePositive);
        engagement.DayRate.Should().BeNull();
    }

    [Fact]
    public void SetDayRate_refuses_a_closed_engagement()
    {
        Engagement engagement = Engagement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now).Value;
        engagement.Close(Today, Now);

        Result result = engagement.SetDayRate(new Money(300m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.EngagementAlreadyClosed);
        engagement.DayRate.Should().BeNull();
    }

    [Fact]
    public void Open_records_the_opener()
    {
        Guid openedByUserId = Guid.CreateVersion7();

        Engagement engagement = Engagement.Open(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Today, Now, openedByUserId: openedByUserId).Value;

        engagement.OpenedByUserId.Should().Be(openedByUserId);
    }

    // ---- AC-210-B · the pool's figures are derived, never a stored column --------------------

    /// <summary>
    /// TC-2-117: <c>Engagement</c>'s and <c>Employee</c>'s own properties, enumerated as an
    /// allow-list, so that no average, no count, no total and no cached rating column can be added
    /// under a name this test does not know to look for. CLAUDE.md — "Never store a balance … a
    /// stored average is a stored balance under another name."
    /// </summary>
    [Fact]
    public void No_pool_figure_is_a_stored_column_on_the_engagement_or_the_worker()
    {
        string[] engagementProperties = [.. typeof(Engagement).GetProperties().Select(p => p.Name)];
        string[] employeeProperties = [.. typeof(Employee).GetProperties().Select(p => p.Name)];

        engagementProperties.Should().NotBeEmpty();
        employeeProperties.Should().NotBeEmpty();

        engagementProperties.Should().BeEquivalentTo(
            [
                nameof(Entity.Id), nameof(Engagement.WorkerId), nameof(Engagement.ProjectId),
                nameof(Engagement.DayRate), nameof(Engagement.OpenedOn), nameof(Engagement.OpenedAt),
                nameof(Engagement.OpenedByUserId), nameof(Engagement.ClosedOn), nameof(Engagement.ClosedAt),
                nameof(Engagement.Rating), nameof(Engagement.IsOpen),
            ],
            "one engagement, one rating — no aggregate belongs on this entity");

        string[] forbidden = ["average", "frequency", "count", "total"];

        foreach (string property in engagementProperties.Concat(employeeProperties))
        {
            string lowered = property.ToLowerInvariant();
            forbidden.Should().NotContain(
                term => lowered.Contains(term, StringComparison.Ordinal),
                $"{property} would be a cached pool figure — derive it from Engagements instead");
        }
    }
}
