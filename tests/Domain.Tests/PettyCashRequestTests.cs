using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.PettyCash;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-609b: the supervisor submits or returns a junior's عهدة draft (spec.md section 9).</summary>
public sealed class PettyCashRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Project = Guid.CreateVersion7();
    private static readonly Guid Junior = Guid.CreateVersion7();
    private static readonly Guid Supervisor = Guid.CreateVersion7();

    private static PettyCashRequest NewDraft() => PettyCashRequest.Draft(Project, Junior, new Money(5_000m), "مواد سباكة", Now).Value;

    [Fact]
    public void A_new_request_is_a_draft_with_nothing_decided()
    {
        PettyCashRequest request = NewDraft();

        request.Status.Should().Be(PettyCashRequestStatus.Draft);
        request.SubmittedAt.Should().BeNull();
        request.ReturnReason.Should().BeNull();
    }

    [Fact]
    public void The_supervisor_submits_a_draft()
    {
        PettyCashRequest request = NewDraft();

        request.Submit(Supervisor, Now).IsSuccess.Should().BeTrue();

        request.Status.Should().Be(PettyCashRequestStatus.Submitted);
        request.SubmittedByUserId.Should().Be(Supervisor);
        request.SubmittedAt.Should().Be(Now);
        request.Amount.Should().Be(new Money(5_000m), "a submitted request keeps its amount");
    }

    [Fact]
    public void The_user_who_drafted_a_request_cannot_submit_or_return_it()
    {
        PettyCashRequest request = NewDraft();

        request.Submit(Junior, Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        request.Return(Junior, "reason", Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        request.Status.Should().Be(PettyCashRequestStatus.Draft);
    }

    [Fact]
    public void A_return_stores_its_reason_and_a_blank_reason_is_refused()
    {
        PettyCashRequest request = NewDraft();

        foreach (string? blank in new string?[] { null, "", "   " })
        {
            request.Return(Supervisor, blank, Now).Error.Should().Be(PettyCashErrors.ReasonRequired);
        }

        request.Status.Should().Be(PettyCashRequestStatus.Draft);

        request.Return(Supervisor, "  المبلغ غير واضح  ", Now).IsSuccess.Should().BeTrue();

        request.Status.Should().Be(PettyCashRequestStatus.Returned);
        request.ReturnReason.Should().Be("المبلغ غير واضح");
        request.ReturnedByUserId.Should().Be(Supervisor);
    }

    [Fact]
    public void Only_a_draft_can_be_submitted_or_returned()
    {
        PettyCashRequest submitted = NewDraft();
        submitted.Submit(Supervisor, Now).IsSuccess.Should().BeTrue();
        PettyCashRequest returned = NewDraft();
        returned.Return(Supervisor, "reason", Now).IsSuccess.Should().BeTrue();

        foreach (PettyCashRequest request in new[] { submitted, returned })
        {
            request.Submit(Supervisor, Now).Error.Should().Be(PettyCashErrors.NotDraft);
            request.Return(Supervisor, "reason", Now).Error.Should().Be(PettyCashErrors.NotDraft);
        }
    }

    [Fact]
    public void A_draft_needs_a_positive_amount_and_a_purpose()
    {
        PettyCashRequest.Draft(Project, Junior, Money.Zero, "x", Now).Error.Should().Be(PettyCashErrors.AmountInvalid);
        PettyCashRequest.Draft(Project, Junior, new Money(1m), " ", Now).Error.Should().Be(PettyCashErrors.PurposeRequired);
    }
}
