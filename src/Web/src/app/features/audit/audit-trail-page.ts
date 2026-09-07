import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';

import { toProblem } from '../../core/api/problem-details';
import { AuditApi, AuditEntry, AuditSnapshot } from '../../core/audit/audit.api';
import {
  auditActionKey,
  auditEventKindKey,
  projectAccessPathKey,
  roleKey,
} from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';

/**
 * What the interceptor writes in place of a secret — `AuditRedactedAttribute.Placeholder`.
 *
 * **The one value in this file that crosses the boundary as a literal.** It is a stored value rather
 * than a wire contract, so there is nothing to import; `TC-1-303` is what notices if the two ever
 * disagree, by driving a real password change and reading the panel.
 */
const REDACTED = '[redacted]';

/** One cell of the changes table: either a catalogue key to resolve, or a stored value to show. */
export interface AuditValue {
  readonly key: string | null;
  readonly text: string;
}

/** One row of it — `Field · Before · After`, in `S-015`'s right-to-left column order. */
export interface AuditChange {
  readonly field: string;
  readonly before: AuditValue;
  readonly after: AuditValue;
}

/**
 * S-015 · the audit trail. The Owner's, and nobody else's.
 *
 * **The permission is the sharpest in slice 1, and its unusual half is "even for their own projects".**
 * Every other permission in Kaff is `role × assignment`; this one refuses a Technical Office lead the
 * trail of the project they run (D-049 ruling 1, Karim verbatim). **A filtered trail for a non-Owner is
 * a defect, not a partial success** — so this screen has no project scoping of any kind, and
 * `auditReadGuard` refuses rather than narrows. The server refuses independently; the guard only means
 * the refusal is visible instead of arriving as an empty list.
 *
 * **Nothing here can change a record, and there is nothing to call if it tried** (`AC-128-D`,
 * `AC-117-H`). `audit_records` is append-only by database trigger and the API exposes no write route.
 * The controls on this screen are a date range, a row that opens a panel, and a panel that closes —
 * `TC-1-302` enumerates them from the DOM rather than trusting this sentence.
 *
 * **`ActorRole` and `ActorDisplayName` are rendered as stored, never joined to the user's current
 * record** (`S-015`, `AC-118-J`). An act performed by a Finance user still reads Finance after that
 * user is promoted, and an actor since deactivated is still named. That is what makes it evidence.
 *
 * **Three of `S-015`'s controls are absent, recorded rather than invented** — the action chips and the
 * "Search actor or entity" box, which `GET /api/audit` has no parameters for, and `Load more`, which
 * needs the cursor paging the endpoint does not implement. See `audit.api.ts`, and the story summary:
 * these are findings against `KAFF-117`'s payload, not licence to filter a page in the browser.
 */
@Component({
  selector: 'kaff-audit-trail-page',
  imports: [FormField],
  templateUrl: './audit-trail-page.html',
  styleUrl: './audit-trail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditTrailPage {
  private readonly api = inject(AuditApi);

  protected readonly i18n = inject(I18nService);
  protected readonly auditActionKey = auditActionKey;
  protected readonly auditEventKindKey = auditEventKindKey;
  protected readonly projectAccessPathKey = projectAccessPathKey;
  protected readonly roleKey = roleKey;

  /**
   * The date range, empty on arrival.
   *
   * **Both ends open by default, deliberately.** `AC-117-A` is that a read with no filter answers with
   * every record in Kaff, because half of what the Owner is checking — a user created, a client edited
   * — belongs to no project and to no particular week. A screen that quietly defaulted to "this month"
   * would render a truncated trail that looks like a complete one, which is the same defect as a
   * silent `Take`.
   */
  private readonly rangeModel = signal<{ from: string; to: string }>({ from: '', to: '' });

  protected readonly rangeForm = form(this.rangeModel);

  protected readonly entries = signal<readonly AuditEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly selected = signal<AuditEntry | null>(null);

  /** The changes table for whichever record is open. Empty for a record that changed no entity. */
  protected readonly changes = computed<readonly AuditChange[]>(() => {
    const entry = this.selected();

    if (entry === null) {
      return [];
    }

    return fieldsOf(entry).map((field) => ({
      field,
      before: valueOf(entry.before, field),
      after: valueOf(entry.after, field),
    }));
  });

  constructor() {
    void this.reload();
  }

  protected async onSubmitRange(): Promise<void> {
    await this.reload();
  }

  protected open(entry: AuditEntry): void {
    this.selected.set(entry);
  }

  protected close(): void {
    this.selected.set(null);
  }

  protected trackEntry(_index: number, entry: AuditEntry): string {
    return entry.id;
  }

  /**
   * `20/08/2026 14:32:07`, in Latin digits under Gregorian, both pinned by {@link I18nService}.
   *
   * The single most reorderable string on any slice-1 screen — four Latin runs and three separators
   * inside an Arabic row — which is why every one of them is inside a `<bdi>` in the template.
   */
  protected timestamp(value: string): string {
    return this.i18n.formatDate(new Date(value), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  }

  /** How many fields a row says moved, from the same source the panel builds its table from. */
  protected changedCount(entry: AuditEntry): number {
    return fieldsOf(entry).length;
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    this.selected.set(null);

    const range = this.rangeModel();

    try {
      this.entries.set(await this.api.list(range.from, range.to));
    } catch (error) {
      // A 403 here is the mechanism working, not a bug — the server is the decider and a role without
      // AuditRead reaching this route by URL is exactly what it decides. An inverted range arrives the
      // same way, as the server's own key. This file chooses neither.
      this.failure.set(toProblem(error).messageKey);
      this.entries.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

/**
 * Which fields the panel lists.
 *
 * `ChangedProperties` is what actually moved and is empty on creation and on deletion, where *every*
 * field is the change. Falling back to the surviving snapshot's own keys is what makes a `Created`
 * record show what it was created with rather than an empty table — which would read as "nothing
 * changed", the exact misreading `audit.value.redacted` exists to prevent one row lower.
 */
function fieldsOf(entry: AuditEntry): readonly string[] {
  if (entry.changedProperties.length > 0) {
    return entry.changedProperties;
  }

  return Object.keys(entry.after ?? entry.before ?? {});
}

/**
 * One cell.
 *
 * **A redacted value reads as redacted and an absent one reads as absent; neither is ever blank.**
 * `S-015`: a blank cell reads as "nothing changed", which is the opposite of what happened.
 */
function valueOf(snapshot: AuditSnapshot | null, field: string): AuditValue {
  const value = snapshot === null ? undefined : snapshot[field];

  if (value === REDACTED) {
    return { key: 'audit.value.redacted', text: '' };
  }

  if (value === undefined || value === null) {
    return { key: 'audit.value.none', text: '' };
  }

  return {
    key: null,
    text: typeof value === 'string' ? value : JSON.stringify(value),
  };
}
