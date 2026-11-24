import { PillTone } from '../kaff-status-pill/kaff-status-pill';
import { KaffIconName } from '../kaff-icon/kaff-icon';
import { InactivityLevel, OpportunityStage, OpportunityStatus } from '../../core/sales/opportunities.api';

const DAY_MS = 86_400_000;

/** `sales.stage.<Stage>`: the six pipeline words. */
export function stageKey(stage: OpportunityStage): string {
  return `sales.stage.${stage}`;
}

/** The status word. A stalled opportunity reads as active: the quiet line and the follow-up badge carry it. */
export function statusKey(status: OpportunityStatus): string {
  return `sales.status.${status === 'Stalled' ? 'Active' : status}`;
}

export const STATUS_PILL: Readonly<Record<OpportunityStatus, { tone: PillTone; icon: KaffIconName }>> = {
  Active: { tone: 'run', icon: 'dot' },
  Stalled: { tone: 'run', icon: 'dot' },
  OnHold: { tone: 'post', icon: 'pause' },
  ClosedLost: { tone: 'out', icon: 'x' },
  ClosedWon: { tone: 'done', icon: 'check' },
};

/** Whole days since the last activity, never negative. */
export function daysQuiet(lastActivityAt: string, now: Date): number {
  return Math.max(0, Math.floor((now.getTime() - new Date(lastActivityAt).getTime()) / DAY_MS));
}

/** The i18n key and parameter for «آخر نشاط» text: today, one day, two days, 3 to 10 days, more. */
export function agoKey(days: number): { key: string; params?: { n: number } } {
  if (days === 0) return { key: 'sales.ago.today' };
  if (days === 1) return { key: 'sales.ago.one' };
  if (days === 2) return { key: 'sales.ago.two' };
  return { key: days <= 10 ? 'sales.ago.few' : 'sales.ago.many', params: { n: days } };
}

export function needsFollowUp(inactivity: InactivityLevel): boolean {
  return inactivity !== 'None';
}

/** The next stage a person may move to by hand: none past Negotiation (D-268), where conversion takes over. */
export function nextStage(stage: OpportunityStage): OpportunityStage | null {
  switch (stage) {
    case 'Lead': return 'Meeting';
    case 'Meeting': return 'SiteVisit';
    case 'SiteVisit': return 'Quotation';
    case 'Quotation': return 'Negotiation';
    case 'Negotiation':
    case 'Contract': return null;
  }
}
