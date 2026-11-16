import { ProjectStatus } from '../../core/projects/projects.api';
import { KaffIconName } from '../../shared/kaff-icon/kaff-icon';

export type PillTone = 'new' | 'overwrite' | 'error' | 'locked' | 'same';

/** The state pill is word, shape and colour (never colour alone); this is shape and colour. */
export const STATUS_PILL: Readonly<Record<ProjectStatus, { tone: PillTone; icon: KaffIconName }>> = {
  Setup: { tone: 'same', icon: 'file' },
  Active: { tone: 'new', icon: 'check' },
  HandoverPending: { tone: 'overwrite', icon: 'info' },
  Handover: { tone: 'overwrite', icon: 'info' },
  UnderWarranty: { tone: 'overwrite', icon: 'info' },
  Closed: { tone: 'same', icon: 'lock' },
  Stopped: { tone: 'locked', icon: 'pause' },
  Terminated: { tone: 'error', icon: 'x' },
};

/**
 * D-228 item 4: an Active project with no daily log entry yet reads «لم تبدأ» (the Setup word); «جاري العمل» comes with
 * the first entry. A display derivation only: the state machine is unchanged, and every other status shows as it is.
 */
export function shownStatus(project: {
  readonly status: ProjectStatus;
  readonly workStarted?: boolean;
  readonly contractType?: string;
}): ProjectStatus {
  // A design project has no daily log, so «not started» would never end: it shows the server's status (project.html ?type=ds&view=normal).
  return project.status === 'Active' && project.workStarted !== true && project.contractType !== 'Design' ? 'Setup' : project.status;
}
