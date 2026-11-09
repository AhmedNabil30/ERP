using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-415 in the domain: a design project opens its execution project, and the execution signature raises the 30%
/// design credit (spec.md section 5.4), which posts only when a different user approves it.
/// </summary>
public sealed class DesignToExecutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly Guid ClientId = Guid.CreateVersion7();
    private static readonly RulingSet Rulings = RulingSet.Default;

    private static Project Design(decimal? area = 100m)
    {
        Project design = ProjectOpening.Open("تصميم فيلا", ClientId, ContractType.Design, null, null, Now).Value.Project;
        if (area is { } squareMetres)
        {
            design.SetDesignTerms(squareMetres, new Money(450m)).IsSuccess.Should().BeTrue();
        }

        return design;
    }

    private static OpenedProject Convert(Project design, ContractType type = ContractType.LumpSum) =>
        ProjectOpening.ConvertToExecution(design, alreadyConverted: false, "تنفيذ فيلا", type, Rulings, Now).Value;

    private static Account Open(Project project, AccountType type) => ProjectAccountSetFactory.Open(project, type, Today).Value;

    // ---- conversion ----

    [Theory]
    [InlineData(ContractType.LumpSum)]
    [InlineData(ContractType.CostPlus)]
    public void AC_415_A_C_a_design_project_opens_a_linked_execution_project_with_its_own_accounts(ContractType type)
    {
        Project design = Design();
        OpenedProject opened = Convert(design, type);

        Project execution = opened.Project;
        execution.Id.Should().NotBe(design.Id);
        execution.ClientId.Should().Be(design.ClientId);
        execution.ContractType.Should().Be(type);
        execution.LinkedProjectId.Should().Be(design.Id);
        execution.LinkType.Should().Be(ProjectLinkType.DesignToExecution);
        execution.Status.Should().Be(ProjectStatus.Setup);

        design.ContractType.Should().Be(ContractType.Design, "a project never mutates into another type");
        design.LinkedProjectId.Should().BeNull("the link lives on the execution project");

        opened.Accounts.Should().NotBeEmpty();
        opened.Accounts.Should().OnlyContain(account => account.ProjectId == execution.Id);
        opened.Accounts.Select(account => account.Type)
            .Should().BeEquivalentTo(ProjectAccountSetFactory.RequiredTypes(type), "exactly as any new project's");
    }

    [Fact]
    public void AC_415_B_a_non_design_project_is_refused()
    {
        Project lumpSum = ProjectOpening.Open("مشروع", ClientId, ContractType.LumpSum, null, null, Now).Value.Project;

        ProjectOpening.ConvertToExecution(lumpSum, false, "تنفيذ", ContractType.LumpSum, Rulings, Now)
            .Error.Should().Be(ProjectErrors.NotDesign);
    }

    [Fact]
    public void AC_415_B_a_design_project_that_already_converted_is_refused()
    {
        ProjectOpening.ConvertToExecution(Design(), alreadyConverted: true, "تنفيذ", ContractType.LumpSum, Rulings, Now)
            .Error.Should().Be(ProjectErrors.AlreadyConverted);
    }

    [Fact]
    public void Q_207_9_design_is_not_an_execution_type()
    {
        ProjectOpening.ConvertToExecution(Design(), false, "تنفيذ", ContractType.Design, Rulings, Now)
            .Error.Should().Be(ProjectErrors.ExecutionTypeNotAllowed);
    }

    // ---- the credit ----

    [Fact]
    public void AC_415_D_signature_raises_one_design_credit_of_30_percent_of_the_design_value_and_posts_nothing()
    {
        Project design = Design(); // 100 m2 x 450 = 45,000
        Project execution = Convert(design).Project;
        Account revenue = Open(execution, AccountType.ContractRevenue);

        Adjustment credit = Adjustment.RaiseDesignCredit(execution, 1, design, revenue, false, Rulings, Guid.CreateVersion7(), Now).Value;

        credit.Type.Should().Be(AdjustmentType.DesignCredit);
        credit.Status.Should().Be(AdjustmentStatus.Raised);
        credit.Amount.Should().Be(new Money(13_500m));
        credit.ProjectId.Should().Be(execution.Id);
        credit.TargetAccountId.Should().Be(revenue.Id);
        credit.SourceDocument.Should().Be(new SourceDocument(SourceDocumentType.DesignProject, design.Id, design.Code));
        credit.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_second_design_credit_is_refused()
    {
        Project design = Design();
        Project execution = Convert(design).Project;

        Adjustment.RaiseDesignCredit(
                execution, 2, design, Open(execution, AccountType.ContractRevenue), alreadyRaised: true, Rulings, Guid.CreateVersion7(), Now)
            .Error.Should().Be(AdjustmentErrors.DesignCreditAlreadyRaised);
    }

    [Fact]
    public void Q_207_6_a_design_project_with_no_contract_value_refuses_the_credit()
    {
        Project design = Design(area: null);
        Project execution = Convert(design).Project;

        Adjustment.RaiseDesignCredit(
                execution, 1, design, Open(execution, AccountType.ContractRevenue), false, Rulings, Guid.CreateVersion7(), Now)
            .Error.Should().Be(AdjustmentErrors.DesignValueMissing);
    }

    [Fact]
    public void AC_415_E_the_credit_cannot_target_the_hold_the_design_project_or_an_unlinked_project()
    {
        Project design = Design();
        Project execution = Convert(design).Project;
        Guid actor = Guid.CreateVersion7();

        Adjustment.RaiseDesignCredit(execution, 1, design, Open(execution, AccountType.Hold), false, Rulings, actor, Now)
            .Error.Should().Be(AdjustmentErrors.HoldUntouchable);
        Adjustment.RaiseDesignCredit(execution, 1, design, Open(design, AccountType.ContractRevenue), false, Rulings, actor, Now)
            .Error.Should().Be(AdjustmentErrors.TargetAccountNotAllowed);

        Project unlinked = ProjectOpening.Open("مشروع", ClientId, ContractType.LumpSum, null, null, Now).Value.Project;
        Adjustment.RaiseDesignCredit(unlinked, 1, design, Open(unlinked, AccountType.ContractRevenue), false, Rulings, actor, Now)
            .Error.Should().Be(AdjustmentErrors.DesignCreditNotLinked);
    }

    [Fact]
    public void AC_415_F_the_signer_cannot_approve_and_another_user_posts_one_credit_note_on_the_execution_accounts()
    {
        Project design = Design();
        Project execution = Convert(design).Project;
        Account revenue = Open(execution, AccountType.ContractRevenue);
        Account receivable = Open(execution, AccountType.ClientReceivable);
        Guid signer = Guid.CreateVersion7();
        Adjustment credit = Adjustment.RaiseDesignCredit(execution, 1, design, revenue, false, Rulings, signer, Now).Value;

        credit.Approve(signer, [], execution, receivable, revenue, Rulings, Now, Today)
            .Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        credit.Status.Should().Be(AdjustmentStatus.Raised);

        PostingSet posted = credit.Approve(Guid.CreateVersion7(), [], execution, receivable, revenue, Rulings, Now, Today).Value;

        credit.Status.Should().Be(AdjustmentStatus.Approved);
        Posting posting = posted.Postings.Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.CreditNote);
        posting.Amount.Should().Be(new Money(13_500m));
        posting.FromAccountId.Should().Be(receivable.Id);
        posting.ToAccountId.Should().Be(revenue.Id);
        posting.ProjectId.Should().Be(execution.Id);
        posting.SourceDocumentType.Should().Be(SourceDocumentType.Adjustment);
        posting.SourceDocumentId.Should().Be(credit.Id);
    }

    [Fact]
    public void Q_207_5_to_9_are_registered_as_assumed_rulings()
    {
        string[] names = ["DesignQuantitySeed", "DesignCreditBase", "DesignConversionCount", "DesignCreditApproval", "DesignConversionTargets"];

        foreach (string name in names)
        {
            RulingNote note = RulingSet.Default.Notes.Single(candidate => candidate.Name == name);
            note.Status.Should().Be(RulingStatus.Assumed);
            note.Question.Should().StartWith("Q-207-");
        }
    }
}
