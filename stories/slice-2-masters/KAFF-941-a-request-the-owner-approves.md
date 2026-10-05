# KAFF-941 · A change request the Owner approves

<!-- kaff id=KAFF-941 slice=2 points=0 state=NOT-BUILT -->

**Slice:** 2 (Masters) · **Epic:** Catalogue · **Points:** not estimated — the shape is not settled.
**Owner:** unassigned.
**Decision:** `D-177`, Nabil, 2026-10-06.

## Why this story exists

Nabil's full ruling on the catalogue was two things, not one:

1. The Owner alone edits an item's cost and its sell-price override. Built as `KAFF-940`.
2. **Anyone else must submit a request for the Owner's approval to change them.** That is this story,
   and it is not built.

The two were separated deliberately, with Nabil's explicit agreement. The first is a permission split
the server can enforce today. The second is a workflow that does not exist anywhere in this system:
it needs a request entity with its own state machine, a queue the Owner works through, an approval and
rejection path with a stored reason, audit records for each transition, and some way for the requester
to learn the outcome.

Improvising that inside a catalogue screen is how a codebase grows a second approval engine beside the
one it already has. `CLAUDE.md` names exactly that failure: *"Type dispatches, it does not fork."*

## What has to be answered before this can be cut properly

These are open and none of them should be guessed:

- Does this mechanism serve only the catalogue, or is it the general "ask the Owner" path the rest of
  the system will need? The answer decides whether it lives in `Domain/` or in a feature slice.
- Is a reason mandatory on the request? On the rejection? `CLAUDE.md` requires that a rejection
  returns to origin with a stored reason, which suggests yes for rejection at least.
- Does an approved request apply the change automatically, or does it unlock the field for the Owner
  to apply by hand? These differ in audit shape: one writes the Owner as the actor, the other records
  an approval and a separate edit.
- What happens to a pending request when the underlying item changes, is archived, or the requester
  loses the permission that let them ask?
- Who else can see the queue — only the Owner, or Finance too?
- Is there a notification at all, and through what? Nothing in this system notifies anybody today.

## Not in this story

The permission split itself. That is `KAFF-940`, already cut and ready, and this story assumes it has
shipped.

## Questions for Karim

The six above are Nabil's to answer as the business owner, not Karim's, since this is his own approval
process. They are listed here rather than in `stories/questions-for-karim.md` for that reason.
