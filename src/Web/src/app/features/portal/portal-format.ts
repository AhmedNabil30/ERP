import { siteDayOf } from '../../core/format/site-day';
import { I18nService } from '../../core/i18n/i18n.service';
import { PillTone } from '../../shared/kaff-status-pill/kaff-status-pill';
import { KaffIconName } from '../../shared/kaff-icon/kaff-icon';
import { ProjectStatus } from '../../core/projects/projects.api';

/**
 * D-273 #15: day/month/year, never ISO. A bare date is kept as it is and an instant is read in the site day (Africa/Cairo). Built from
 * its three numbers rather than from `Intl`'s date string, which carries direction marks that scramble the order inside a
 * left-to-right figure.
 */
export function portalDate(i18n: I18nService, value: string): string {
  const [year = '0', month = '01', day = '01'] = siteDayOf(value).split('-');
  const two = (n: string) => i18n.formatNumber(Number(n), { minimumIntegerDigits: 2, useGrouping: false });
  return `${two(day)}/${two(month)}/${i18n.formatNumber(Number(year), { useGrouping: false })}`;
}

/**
 * The share of `total` that `part` is, as a whole percent for a meter bar. Both arrive as two-decimal strings; they are
 * read as whole piastres (BigInt), never as a float, and the result drives a bar width only.
 */
export function percentOf(part: string, total: string): number {
  const toPiastres = (value: string): bigint => BigInt(value.replace('.', ''));
  try {
    const whole = toPiastres(total);
    if (whole <= 0n) {
      return 0;
    }
    const share = (toPiastres(part) * 100n) / whole;
    return Number(share < 0n ? 0n : share > 100n ? 100n : share);
  } catch {
    return 0;
  }
}

export interface PillView {
  readonly tone: PillTone;
  readonly icon: KaffIconName;
}

/** The project state pill. The words come from `projectWorkspaceStatusKey`; this only picks the tone. */
export function projectPill(status: ProjectStatus): PillView {
  switch (status) {
    case 'Active':
    case 'HandoverPending':
      return { tone: 'run', icon: 'clock' };
    case 'Setup':
      return { tone: 'none', icon: 'clock' };
    case 'Stopped':
    case 'Terminated':
      return { tone: 'post', icon: 'pause' };
    default:
      return { tone: 'done', icon: 'check' };
  }
}

export interface OrderPillView extends PillView {
  readonly key: string;
}

/** The change order's state word for the client. Only the client decides; Kaff's own approval follows. */
export function changeOrderPill(status: string): OrderPillView {
  switch (status) {
    case 'Approved':
      return { tone: 'done', icon: 'check', key: 'portal.co.status.approved' };
    case 'OwnerApproval':
      return { tone: 'run', icon: 'check', key: 'portal.co.status.client_approved' };
    case 'Rejected':
      return { tone: 'none', icon: 'x', key: 'portal.co.status.refused' };
    case 'Withdrawn':
      return { tone: 'post', icon: 'pause', key: 'portal.co.status.withdrawn' };
    default:
      return { tone: 'run', icon: 'clock', key: 'portal.co.status.waiting' };
  }
}
