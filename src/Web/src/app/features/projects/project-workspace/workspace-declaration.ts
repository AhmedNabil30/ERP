import { ContractType, ProjectStatus } from '../../../core/projects/projects.api';
import { KaffIconName } from '../../../shared/kaff-icon/kaff-icon';
import { ClosureCaps, STEP_LABEL_KEY, closurePrimary } from '../../closure/closure-display';

/**
 * What the project workspace declares per contract type, in ONE table. **Type dispatches, it does not
 * fork** (CLAUDE.md): there is one page, and this table is the only place Lump Sum, Cost Plus and
 * Design differ. A group with no part, and a part the viewer may not use, is ABSENT from the page, never
 * rendered empty or disabled.
 *
 * Source: `.design/v2/team-a/README.md` "The project workspace", cut down to what the API serves today.
 * What the design shows and nothing serves yet (progress, change orders, logs list, ledger balances,
 * roster, linked projects, lifecycle, Cost Plus supervision rate, Design area and rate) is not declared
 * here; it is listed in the build report with the endpoint it needs.
 */
export type GroupId = 'understand' | 'quantify' | 'execute' | 'pay' | 'closure' | 'org';

export type PartId = 'facts' | 'boq' | 'dailyLog' | 'dayLabour' | 'extracts' | 'proof' | 'closure' | 'team';

export const GROUP_ORDER: readonly GroupId[] = ['understand', 'quantify', 'execute', 'pay', 'closure', 'org'];

export const GROUP_ICON: Readonly<Record<GroupId, KaffIconName>> = {
  understand: 'info',
  quantify: 'list',
  execute: 'layers',
  pay: 'calc',
  closure: 'check',
  org: 'folder',
};

export const GROUP_TITLE_KEY: Readonly<Record<GroupId, string>> = {
  understand: 'projects.workspace.vocab.group.understand',
  quantify: 'projects.workspace.vocab.group.quantify',
  execute: 'projects.workspace.vocab.group.execute',
  pay: 'projects.workspace.vocab.group.pay',
  closure: 'projects.workspace.vocab.group.closure',
  org: 'projects.workspace.vocab.group.org',
};

/** One intention question per type and group (design `GQ`). Literal keys, so a key scan finds each. */
export const GROUP_INTENT_KEY: Readonly<Record<ContractType, Readonly<Record<GroupId, string>>>> = {
  LumpSum: {
    understand: 'projects.workspace.vocab.intent.ls.understand',
    quantify: 'projects.workspace.vocab.intent.ls.quantify',
    execute: 'projects.workspace.vocab.intent.ls.execute',
    pay: 'projects.workspace.vocab.intent.ls.pay',
    closure: 'projects.workspace.vocab.intent.ls.closure',
    org: 'projects.workspace.vocab.intent.ls.org',
  },
  CostPlus: {
    understand: 'projects.workspace.vocab.intent.cp.understand',
    quantify: 'projects.workspace.vocab.intent.cp.quantify',
    execute: 'projects.workspace.vocab.intent.cp.execute',
    pay: 'projects.workspace.vocab.intent.cp.pay',
    closure: 'projects.workspace.vocab.intent.cp.closure',
    org: 'projects.workspace.vocab.intent.cp.org',
  },
  Design: {
    understand: 'projects.workspace.vocab.intent.ds.understand',
    quantify: 'projects.workspace.vocab.intent.ds.quantify',
    execute: 'projects.workspace.vocab.intent.ds.execute',
    pay: 'projects.workspace.vocab.intent.ds.pay',
    closure: 'projects.workspace.vocab.intent.ds.closure',
    org: 'projects.workspace.vocab.intent.ds.org',
  },
};

/**
 * The parts each type holds in each group, in order. Cost Plus has no BOQ, so its quantify group is
 * empty and therefore absent (what Cost Plus quantifies, the classification of contract lines, is
 * served by nothing yet). Design has no site: no daily log, no day labour.
 */
export const DECLARATION: Readonly<Record<ContractType, Readonly<Record<GroupId, readonly PartId[]>>>> = {
  LumpSum: {
    understand: ['facts'],
    quantify: ['boq'],
    execute: ['dailyLog', 'dayLabour'],
    pay: ['extracts', 'proof'],
    closure: ['closure'],
    org: ['team'],
  },
  CostPlus: {
    understand: [],
    quantify: [],
    execute: ['dailyLog', 'dayLabour'],
    pay: ['proof'],
    closure: ['closure'],
    org: ['team'],
  },
  Design: {
    understand: ['facts'],
    quantify: [],
    execute: [],
    pay: ['proof'],
    closure: [],
    org: ['team'],
  },
};

/** What a viewer may use. Convenience only: every read and every screen is decided again by the server. */
export interface Capabilities {
  /** False for the site engineer: a money-free read model, no money in any group. */
  readonly seesMoney: boolean;
  readonly canReadProject: boolean;
  readonly canReadBoq: boolean;
  readonly canWriteDailyLog: boolean;
  readonly canManageDayLabour: boolean;
  readonly canReadProof: boolean;
  /** What the viewer holds on THIS project for the closure chain (practical completion to closing). */
  readonly closure: ClosureCaps;
}

/** Whether a declared part is shown to this viewer. */
export function partAllowed(part: PartId, caps: Capabilities): boolean {
  switch (part) {
    case 'facts':
      return caps.seesMoney;
    case 'boq':
      return caps.canReadBoq;
    case 'dailyLog':
      return caps.canWriteDailyLog;
    case 'dayLabour':
      return caps.canManageDayLabour;
    case 'extracts':
      return caps.seesMoney && caps.canReadProject;
    case 'proof':
      return caps.canReadProof;
    case 'closure':
      return caps.canReadProject;
    case 'team':
      return true;
  }
}

/** The groups this viewer sees, each with its visible parts; a group with none is absent. */
export function visibleGroups(
  type: ContractType,
  caps: Capabilities,
): readonly { readonly id: GroupId; readonly parts: readonly PartId[] }[] {
  return GROUP_ORDER.map((id) => ({
    id,
    parts: DECLARATION[type][id].filter((part) => partAllowed(part, caps)),
  })).filter((group) => group.parts.length > 0);
}

export interface PrimaryAction {
  readonly labelKey: string;
  readonly params?: Readonly<Record<string, number>>;
  readonly link: readonly string[];
}

/**
 * The newest extract still in the chain, as the server describes it for THIS viewer: `hasAction` is the
 * server's own `actions` answer (permission, assignment, state and separation of duties already folded in),
 * so the slot never re-derives who may do what.
 */
export interface PendingExtract {
  readonly id: string;
  readonly number: number;
  readonly hasAction: boolean;
}

/**
 * The one primary action the shell places in the header (README Q2: working text). Only an action that
 * leads to a screen that exists is declared. "New extract", "new invoice", collect and the change-order
 * gates have no web screen yet, so they are not offered.
 */
export function primaryAction(
  type: ContractType,
  status: ProjectStatus,
  caps: Capabilities,
  projectId: string,
  pending: PendingExtract | null = null,
): PrimaryAction | null {
  if (type === 'LumpSum') {
    if (caps.seesMoney && status === 'Setup' && caps.canReadBoq) {
      return { labelKey: 'projects.workspace.vocab.action.open_boq', link: ['/projects', projectId, 'boq'] };
    }
    if (caps.seesMoney && pending?.hasAction === true) {
      return {
        labelKey: 'projects.workspace.vocab.action.open_extract',
        params: { number: pending.number },
        link: ['/projects', projectId, 'extracts', pending.id],
      };
    }
    if (!caps.seesMoney && caps.canWriteDailyLog) {
      return {
        labelKey: 'projects.workspace.vocab.action.new_daily_log',
        link: ['/projects', projectId, 'daily-log'],
      };
    }
  }
  // The closure acts (declare practical completion, hand over, begin the warranty, close) take the slot
  // when nothing earlier does. The screen they lead to asks for confirmation; the slot only points there.
  const step = closurePrimary(type, status, caps.closure);
  return step === null ? null : { labelKey: STEP_LABEL_KEY[step], link: ['/projects', projectId, 'closure'] };
}
