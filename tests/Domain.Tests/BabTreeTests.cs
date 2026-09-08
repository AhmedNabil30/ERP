using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md §2 — the أبواب are a <b>tree</b>. KAFF-205 <c>AC-205-C</c>.
/// </summary>
/// <remarks>
/// <para>
/// The guard this file pins was <c>parentBabId == Id</c> and nothing else, so <c>A.SetParent(B)</c>
/// followed by <c>B.SetParent(A)</c> was accepted in full — and after it, neither باب is reachable
/// from any root, every walk over the tree runs forever, and both disappear from <c>S-021</c>.
/// Found by the BA at slice-2 refinement on 2026-09-08 and routed as a defect rather than a
/// question: nothing needs asking to know that a tree has no cycles.
/// </para>
/// <para>
/// <b>A cycle is a property of the tree, not of the node</b>, which is why <c>SetParent</c> takes the
/// parent pointers of every باب. An entity cannot see its siblings, and a check that only looks at
/// the node can only ever catch depth one — which is exactly the guard that was there.
/// </para>
/// </remarks>
public sealed class BabTreeTests
{
    [Fact]
    public void A_bab_cannot_become_its_own_parent()
    {
        Bab bab = NewBab("B-01");

        bab.SetParent(bab.Id, Tree(bab)).Error
            .Should().Be(MasterDataErrors.BabCannotBeItsOwnParent);

        bab.ParentBabId.Should().BeNull("a refused move moves nothing");
    }

    [Fact]
    public void A_two_step_cycle_is_refused()
    {
        // The reported defect, in the story's own words: "A.SetParent(B) → allowed, B is not A;
        // B.SetParent(A) → allowed, A is not B ← and now neither is reachable from a root."
        Bab a = NewBab("B-A");
        Bab b = NewBab("B-B");

        a.SetParent(b.Id, Tree(a, b)).IsSuccess.Should().BeTrue("A under B is a legal move");

        b.SetParent(a.Id, Tree(a, b)).Error
            .Should().Be(MasterDataErrors.BabCannotBeItsOwnParent, "B is already A's parent");

        b.ParentBabId.Should().BeNull("the refused move left B a root");
    }

    [Fact]
    public void A_bab_cannot_be_re_parented_under_its_own_descendant_at_any_depth()
    {
        // AC-205-C's first half: the chain A → B → C, and A re-parented to C. Depth three, so the
        // walk has to leave the candidate parent and keep going.
        Bab a = NewBab("B-A");
        Bab b = NewBab("B-B");
        Bab c = NewBab("B-C");

        b.SetParent(a.Id, Tree(a, b, c)).IsSuccess.Should().BeTrue();
        c.SetParent(b.Id, Tree(a, b, c)).IsSuccess.Should().BeTrue();

        a.SetParent(c.Id, Tree(a, b, c)).Error
            .Should().Be(MasterDataErrors.BabCannotBeItsOwnParent, "C is A's grandchild");

        a.ParentBabId.Should().BeNull();

        // And every باب is still reachable from a root, which is what the refusal protects.
        Reaches(a, Tree(a, b, c)).Should().BeTrue();
        Reaches(b, Tree(a, b, c)).Should().BeTrue();
        Reaches(c, Tree(a, b, c)).Should().BeTrue();
    }

    [Fact]
    public void A_legal_move_is_accepted_and_clearing_the_parent_makes_a_root()
    {
        // AC-205-A and AC-205-B. Without this the guard could refuse everything and still be green.
        Bab a = NewBab("B-A");
        Bab d = NewBab("B-D");

        a.SetParent(d.Id, Tree(a, d)).IsSuccess.Should().BeTrue();
        a.ParentBabId.Should().Be(d.Id);

        a.SetParent(null, Tree(a, d)).IsSuccess.Should().BeTrue();
        a.ParentBabId.Should().BeNull();
    }

    [Fact]
    public void A_cycle_already_in_the_data_refuses_rather_than_walking_forever()
    {
        // The guard is new; rows written before it are not. A chain longer than the tree is a cycle
        // among أبواب this node is not part of, and the walk must end rather than hang the request.
        Guid x = Guid.NewGuid();
        Guid y = Guid.NewGuid();
        Bab bab = NewBab("B-01");

        Dictionary<Guid, Guid?> corrupt = new() { [bab.Id] = null, [x] = y, [y] = x };

        bab.SetParent(x, corrupt).Error.Should().Be(MasterDataErrors.BabCannotBeItsOwnParent);
        bab.ParentBabId.Should().BeNull();
    }

    private static Bab NewBab(string code)
    {
        Result<Bab> created = Bab.Create(code, "باب", "Bab", Percentage.FromPercent(15m));
        created.IsSuccess.Should().BeTrue();
        return created.Value;
    }

    /// <summary>Every باب's parent pointer, as a handler reads it out of the database.</summary>
    private static Dictionary<Guid, Guid?> Tree(params Bab[] babs) =>
        babs.ToDictionary(bab => bab.Id, bab => bab.ParentBabId);

    /// <summary>Walks to a root, bounded so a cycle ends the walk instead of the test run.</summary>
    private static bool Reaches(Bab bab, Dictionary<Guid, Guid?> tree)
    {
        Guid? at = bab.ParentBabId;

        for (int step = 0; step <= tree.Count; step++)
        {
            if (at is null)
            {
                return true;
            }

            at = tree.GetValueOrDefault(at.Value);
        }

        return false;
    }
}
