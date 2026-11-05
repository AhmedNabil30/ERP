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
