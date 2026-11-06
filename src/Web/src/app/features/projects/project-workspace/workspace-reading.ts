import { AssignmentLevel, Role } from '../../../core/auth/auth.service';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { WaitingOnItem, WaitingStaff } from '../../../core/extracts/extracts.api';
import { BabProgress, ProgressKind, ProjectProgress } from '../../../core/projects/projects.api';

/**
 * Pure readings of the overview's three new reads (progress, waiting-on), kept out of the component so a
 * test can walk every case without a fixture. Nothing here does arithmetic on money (D-135): money passes
 * through as the string the API sent.
 */

/** One Bab as the overview draws it. `percent` is null when the API sent no completion: no bar, never 0. */
export interface BabRow {
  readonly code: string;
  readonly name: string;
  readonly percent: number | null;
  readonly percentText: string | null;
  readonly isActive: boolean;
  readonly executedValue: string | null;
}

/**
 * What the progress read is drawn as.
 * - `bar`: a real percentage exists (Lump Sum). Never produced without a `completion` on the wire.
 * - `money`: Cost Plus. spec.md section 5.2: cost plus supervision, **no percentage bar**.
 * - `unavailable`: the server cannot measure it (Design answers 409), or sent a percentage kind without one.
 * - `none`: nothing the caller may see (a Cost Plus reading for a caller who sees no money).
 */
export type ProgressView =
  | { readonly kind: 'loading' }
  | { readonly kind: 'failed'; readonly key: string }
  | { readonly kind: 'unavailable' }
  | { readonly kind: 'none' }
  | {
      readonly kind: 'bar';
      readonly percent: number;
      readonly percentText: string;
      readonly executedValue: string | null;
      readonly contractValue: string | null;
      readonly babs: readonly BabRow[];
    }
  | { readonly kind: 'money'; readonly costToDate: string | null; readonly supervisionToDate: string | null };

const UNAVAILABLE: ProgressView = { kind: 'unavailable' };

/** A wire fraction to a 0-100 number for a CSS width only (never money), or null when it is not a fraction. */
export function percentOf(fraction: string | undefined | null): { value: number; text: string } | null {
  if (fraction === undefined || fraction === null) {
    return null;
  }
  try {
    const text = fractionToPercent(fraction);
    const value = Number.parseFloat(text);
    return Number.isFinite(value) ? { value: Math.min(100, Math.max(0, value)), text } : null;
  } catch {
    return null;
  }
}

function babRow(bab: BabProgress, locale: 'ar' | 'en'): BabRow {
  const pct = percentOf(bab.completion);
  return {
    code: bab.code,
    name: (locale === 'ar' ? bab.nameAr : bab.nameEn) || bab.nameAr || bab.nameEn || bab.code,
    percent: pct?.value ?? null,
    percentText: pct?.text ?? null,
    isActive: bab.isActive,
    executedValue: bab.executedValue ?? null,
  };
}

/** A percentage reading: a bar only if the server sent the number. */
function percentageView(reading: ProjectProgress, locale: 'ar' | 'en'): ProgressView {
  const pct = percentOf(reading.completion);
  if (pct === null) {
    return UNAVAILABLE;
  }
  return {
    kind: 'bar',
    percent: pct.value,
    percentText: pct.text,
    executedValue: reading.executedValue ?? null,
    contractValue: reading.contractValue ?? null,
    babs: (reading.babs ?? []).map((bab) => babRow(bab, locale)),
  };
}

/**
 * How each `ProgressKind` is drawn. A `Record` over the union, so a kind added on the server and to the
 * type fails the build here until someone decides how it is drawn. `PROGRESS_KINDS` is read by the test.
 */
const VIEW_OF_KIND: Readonly<Record<ProgressKind, (reading: ProjectProgress, locale: 'ar' | 'en') => ProgressView>> = {
  Percentage: percentageView,
  StageWeighted: percentageView,
  // Cost Plus has NO percentage. Whatever else arrives, `completion` is not read for this kind.
  MonetaryOnly: (reading) =>
    reading.costToDate === undefined && reading.supervisionToDate === undefined
      ? { kind: 'none' }
      : {
          kind: 'money',
          costToDate: reading.costToDate ?? null,
          supervisionToDate: reading.supervisionToDate ?? null,
        },
};

export const PROGRESS_KINDS = Object.keys(VIEW_OF_KIND) as readonly ProgressKind[];

/** An unknown kind from a newer server is "unavailable", never a bar and never 0%. */
export function progressViewOf(reading: ProjectProgress, locale: 'ar' | 'en'): ProgressView {
  const draw = (VIEW_OF_KIND as Partial<Record<string, (r: ProjectProgress, l: 'ar' | 'en') => ProgressView>>)[
    reading.kind
  ];
  return draw === undefined ? UNAVAILABLE : draw(reading, locale);
}

/** The step an extract waits for, by the status it is in now. Literal keys, so the catalogue scan sees each. */
export const WAITING_STEP_KEY: Readonly<Record<string, string>> = {
  Draft: 'projects.workspace.waiting.step.Draft',
  QcSubmitted: 'projects.workspace.waiting.step.QcSubmitted',
  PartiallyApproved: 'projects.workspace.waiting.step.TechnicalApproved',
  TechnicalApproved: 'projects.workspace.waiting.step.TechnicalApproved',
  DeductionsApplied: 'projects.workspace.waiting.step.DeductionsApplied',
  OwnerApproved: 'projects.workspace.waiting.step.OwnerApproved',
  Issued: 'projects.workspace.waiting.step.Issued',
  Disputed: 'projects.workspace.waiting.step.Disputed',
};
export const WAITING_STEP_FALLBACK_KEY = 'projects.workspace.waiting.step.other';

/** Statuses whose next step is taken on the extract screen. Issued (collection) and Disputed have no web screen yet. */
const ACTED_ON_THE_EXTRACT_SCREEN: readonly string[] = [
  'Draft',
  'QcSubmitted',
  'PartiallyApproved',
  'TechnicalApproved',
  'DeductionsApplied',
  'OwnerApproved',
];

export interface WaitingRoleView {
  readonly role: Role;
  /** Site engineer at Supervisor level reads "site supervisor", the way the floor says it. */
  readonly supervisor: boolean;
}

export interface WaitingView {
  readonly extractId: string;
  readonly number: number;
  readonly stepKey: string;
  readonly roles: readonly WaitingRoleView[];
  readonly holderNames: readonly string[];
  readonly barredNames: readonly string[];
  /** The viewer may take the step now (the server named them in `holders`). */
  readonly viewerIsHolder: boolean;
  /** The viewer holds the role but already acted on this extract (separation of duties). */
  readonly viewerIsBarred: boolean;
  /** The step is taken on a screen that exists, so the header slot may point at it. */
  readonly hasScreen: boolean;
}

function names(people: readonly WaitingStaff[]): readonly string[] {
  return people.map((person) => person.fullName);
}

function level(value: AssignmentLevel): boolean {
  return value === 'Supervisor';
}

/**
 * The newest open extract and what it waits for, from this viewer's side. Terminal extracts are not
 * waiting on anyone. Null: nothing is open.
 */
export function waitingViewOf(items: readonly WaitingOnItem[], viewerUserId: string | null): WaitingView | null {
  const open = items.filter((item) => !item.waitingOn.isTerminal);
  const newest = open.reduce<WaitingOnItem | null>((a, b) => (a === null || b.number > a.number ? b : a), null);
  if (newest === null) {
    return null;
  }
  const w = newest.waitingOn;
  const holderIds = w.holders.map((person) => person.userId);
  const barredIds = w.barredHolders.map((person) => person.userId);
  return {
    extractId: newest.extractId,
    number: newest.number,
    stepKey: WAITING_STEP_KEY[w.status] ?? WAITING_STEP_FALLBACK_KEY,
    roles: w.roles.map((r) => ({ role: r.role, supervisor: r.role === 'SiteEngineer' && level(r.minimumLevel) })),
    holderNames: names(w.holders),
    barredNames: names(w.barredHolders),
    viewerIsHolder: viewerUserId !== null && holderIds.includes(viewerUserId),
    viewerIsBarred: viewerUserId !== null && !holderIds.includes(viewerUserId) && barredIds.includes(viewerUserId),
    hasScreen: ACTED_ON_THE_EXTRACT_SCREEN.includes(w.status),
  };
}
