import { ContractType, ProjectStatus } from '../../../core/projects/projects.api';
import { ClosureCaps, STEP_LABEL_KEY, closurePrimary } from '../../closure/closure-display';

/**
 * What the overview offers as its one primary action. The tabs and the groups of the old workspace are
 * gone: the project frame draws the tabs from the server's `tabs` (D-218 item 2), and the overview is one
 * short pane (`.design/v2/team-a/README.md` "Tab: نظرة عامة").
 *
 * **Type dispatches, it does not fork** (CLAUDE.md): one function, one table of what differs.
 */

/** What a viewer may use. Convenience only: every read and every screen is decided again by the server. */
export interface Capabilities {
  /** The server's `tabs.extracts`: the viewer reads extract money. False for the junior site engineer. */
  readonly seesMoney: boolean;
  /** The server's `tabs.boq`. */
  readonly canReadBoq: boolean;
  /** The server's `tabs.dailyLog`. */
  readonly canWriteDailyLog: boolean;
  /** What the viewer holds on THIS project for the closure chain (practical completion to closing). */
  readonly closure: ClosureCaps;
}

export interface PrimaryAction {
  readonly labelKey: string;
  readonly params?: Readonly<Record<string, number>>;
  /** Where it goes. Absent for an act the page performs itself (`act`). */
  readonly link?: readonly string[];
  readonly act?: 'reopen' | 'signDesign' | 'convert';
  /** Query string of the link (the after-signing primary opens the team tab with its add sheet). */
  readonly query?: Readonly<Record<string, string>>;
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
 * The one primary action the shell places in the header (mock `project.html`): reopen on an archived
 * project, open the BOQ while it is unsigned, open the extract that waits on the viewer, a new daily log
 * for the junior site engineer. Only an action that leads to a screen that exists is declared.
 */
export function primaryAction(
  type: ContractType,
  status: ProjectStatus,
  caps: Capabilities,
  projectId: string,
  pending: PendingExtract | null = null,
  signed = false,
): PrimaryAction | null {
  if (type === 'LumpSum') {
    if (caps.canReadBoq && status === 'Setup' && !signed) {
      return { labelKey: 'projects.frame.action.open_boq', link: ['/projects', projectId, 'boq'] };
    }
    // `hasAction` is the server's own answer from waiting-on (who may act now).
    if (pending?.hasAction === true) {
      return {
        labelKey: 'projects.frame.action.open_extract',
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
