import { AuditAction, AuditEventKind } from '../audit/audit.api';
import {
  AssignmentLevel,
  Department,
  OperationsSubDepartment,
  ProjectAccessPath,
  Role,
} from '../auth/auth.service';
import { ClientKind } from '../clients/clients.api';

/**
 * `enum.<Type>.<Member>` — `ux/rtl-and-i18n.md` §6's naming convention for a server enum rendered as
 * text, and hard rule 4: "Never key on a value … build the key in the component with an exhaustive
 * switch so a new enum member is a compile error under `strictTemplates`." One function per enum,
 * every branch named, `assertNever` in the default so a tenth role or a new access path fails the
 * build rather than rendering silently as nothing.
 */
function assertNever(value: never): never {
  throw new Error(`Unhandled enum member: ${String(value)}`);
}

export function roleKey(role: Role): string {
  switch (role) {
    case 'Owner':
      return 'enum.Role.Owner';
    case 'Finance':
      return 'enum.Role.Finance';
    case 'TechnicalOffice':
      return 'enum.Role.TechnicalOffice';
    case 'SiteEngineer':
      return 'enum.Role.SiteEngineer';
    case 'HeadOfDesign':
      return 'enum.Role.HeadOfDesign';
    case 'MarketingSales':
      return 'enum.Role.MarketingSales';
    case 'Client':
      return 'enum.Role.Client';
    case 'Subcontractor':
      return 'enum.Role.Subcontractor';
    case 'Hr':
      return 'enum.Role.Hr';
    default:
      return assertNever(role);
  }
}

export function departmentKey(department: Department): string {
  switch (department) {
    case 'Finance':
      return 'enum.Department.Finance';
    case 'Hr':
      return 'enum.Department.Hr';
    case 'Marketing':
      return 'enum.Department.Marketing';
    case 'Operations':
      return 'enum.Department.Operations';
    default:
      return assertNever(department);
  }
}

export function operationsSubDepartmentKey(subDepartment: OperationsSubDepartment): string {
  switch (subDepartment) {
    case 'Technical':
      return 'enum.OperationsSubDepartment.Technical';
    case 'Financial':
      return 'enum.OperationsSubDepartment.Financial';
    case 'Administrative':
      return 'enum.OperationsSubDepartment.Administrative';
    default:
      return assertNever(subDepartment);
  }
}

export function assignmentLevelKey(level: AssignmentLevel): string {
  switch (level) {
    case 'Standard':
      return 'enum.AssignmentLevel.Standard';
    case 'Junior':
      return 'enum.AssignmentLevel.Junior';
    case 'Supervisor':
      return 'enum.AssignmentLevel.Supervisor';
    default:
      return assertNever(level);
  }
}

/**
 * `HrGlobal`, `PortalClient` and `None` never reach {@link import('../auth/auth.service').ProjectEntry}
 * today (KAFF-105b, D-103) — handled anyway so the switch stays exhaustive against the server's own
 * five-member enum rather than a narrowed guess of what one endpoint returns.
 *
 * **S-015's grant column uses this and not a second vocabulary.** KAFF-128 rule 8 gave the four
 * orphaned `audit.grant.*` keys a judgement — *"used by this screen or deleted from both catalogues,
 * not left orphaned a third time"* — and the judgement is delete. They named four of these same five
 * members in different words, so keeping them would have meant a second mapping function, incomplete
 * by one member (`None`), for a value the server sends as a `ProjectAccessPath` and this file already
 * translates exhaustively. One enum, one vocabulary.
 */
export function projectAccessPathKey(path: ProjectAccessPath): string {
  switch (path) {
    case 'OwnerGlobal':
      return 'enum.ProjectAccessPath.OwnerGlobal';
    case 'HrGlobal':
      return 'enum.ProjectAccessPath.HrGlobal';
    case 'Assignment':
      return 'enum.ProjectAccessPath.Assignment';
    case 'PortalClient':
      return 'enum.ProjectAccessPath.PortalClient';
    case 'None':
      return 'enum.ProjectAccessPath.None';
    default:
      return assertNever(path);
  }
}

/** S-015's action column. `Occurred` is the one that changed no entity — a sign-in, a sign-out. */
export function auditActionKey(action: AuditAction): string {
  switch (action) {
    case 'Created':
      return 'enum.AuditAction.Created';
    case 'Modified':
      return 'enum.AuditAction.Modified';
    case 'Deleted':
      return 'enum.AuditAction.Deleted';
    case 'Occurred':
      return 'enum.AuditAction.Occurred';
    default:
      return assertNever(action);
  }
}

/**
 * What an `Occurred` record was.
 *
 * `S-015`'s element table names `enum.AuditAction.*` and stops there, because it was drawn before
 * `AuditEventKind` existed (D-061). A row whose action is `Occurred` says only "Occurred" without
 * this, which for a failed sign-in against a real account is the one word that carries none of the
 * information — so the six members are translated rather than the enum being rendered raw.
 */
export function auditEventKindKey(kind: AuditEventKind): string {
  switch (kind) {
    case 'SignedIn':
      return 'enum.AuditEventKind.SignedIn';
    case 'SignedOut':
      return 'enum.AuditEventKind.SignedOut';
    case 'SignInFailed':
      return 'enum.AuditEventKind.SignInFailed';
    case 'SignInFailedUnknownUser':
      return 'enum.AuditEventKind.SignInFailedUnknownUser';
    case 'AccountLockedOut':
      return 'enum.AuditEventKind.AccountLockedOut';
    case 'DuplicatePhoneAcknowledged':
      return 'enum.AuditEventKind.DuplicatePhoneAcknowledged';
    default:
      return assertNever(kind);
  }
}

/** spec.md §6.7 — a client is a person or a company, and never neither. KAFF-126. */
export function clientKindKey(kind: ClientKind): string {
  switch (kind) {
    case 'Individual':
      return 'enum.ClientKind.Individual';
    case 'Corporate':
      return 'enum.ClientKind.Corporate';
    default:
      return assertNever(kind);
  }
}
