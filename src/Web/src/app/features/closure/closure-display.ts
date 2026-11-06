import { ContractType, ProjectStatus } from '../../core/projects/projects.api';
import { STATUS_PILL } from '../projects/project-status-pill';

export const CLOSURE_PILL = STATUS_PILL;

/** The closure acts a button can ask for. The hold release has its own panel, because it shows a figure. */
export type ClosureStep = 'practical-completion' | 'handover' | 'warranty' | 'close' | 'hold-release';

/** What a viewer holds on THIS project (`Session.projects[].permissions`). Convenience; the server decides. */
export interface ClosureCaps {
  readonly declare: boolean;
  readonly handover: boolean;
  readonly snag: boolean;
  readonly holdRelease: boolean;
  readonly callback: boolean;
  readonly close: boolean;
  readonly manage: boolean;
}

/**
 * The project acts this viewer may take now, from the state machine of `ProjectClosure`: Active ->
 * HandoverPending (declare), HandoverPending -> Handover (hand over), Handover -> UnderWarranty (begin the
 * warranty), UnderWarranty -> Closed (close). Design has none of them. The hold release is not in this
 * list: it is the one act with its own panel, because it shows a figure.
 */
export function closureNextSteps(type: ContractType, status: ProjectStatus, caps: ClosureCaps): readonly ClosureStep[] {
  if (type === 'Design') {
    return [];
  }
  switch (status) {
    case 'Active':
      return caps.declare ? ['practical-completion'] : [];
    case 'HandoverPending':
      return caps.handover ? ['handover'] : [];
    case 'Handover':
      return caps.handover ? ['warranty'] : [];
    case 'UnderWarranty':
      return caps.close ? ['close'] : [];
    default:
      return [];
  }
}

/** The label of each act's button, a literal per act so the catalogue gate can see every key. */
export const STEP_LABEL_KEY: Readonly<Record<ClosureStep, string>> = {
  'practical-completion': 'closure.action.practical_completion',
  handover: 'closure.action.handover',
  warranty: 'closure.action.warranty',
  close: 'closure.action.close',
  'hold-release': 'closure.action.hold_release',
};

/**
 * The one closure act the workspace header offers this viewer, or null. The hold release is never offered
 * there: the header does not read the hold ledger, and it must not invite a release it cannot show the figure for.
 */
export function closurePrimary(type: ContractType, status: ProjectStatus, caps: ClosureCaps): ClosureStep | null {
  // Declaring practical completion is never the header's invitation: on a running project it would be the only
  // filled control on the screen and would invite the user to call the job finished while money still moves
  // (consultant review A4). It lives on the closure screen only.
  return closureNextSteps(type, status, caps).find((step) => step !== 'practical-completion') ?? null;
}

/** The closure permissions in a project entry's list (`Session.projects[].permissions`). */
export function closureCapsOf(permissions: readonly string[] | undefined): ClosureCaps {
  const has = (permission: string): boolean => permissions?.includes(permission) ?? false;
  return {
    declare: has('ProjectPracticalCompletionDeclare'),
    handover: has('ProjectHandover'),
    snag: has('SnagManage'),
    holdRelease: has('HoldRelease'),
    callback: has('WarrantyCallbackManage'),
    close: has('ProjectClose'),
    manage: has('ProjectManage'),
  };
}
