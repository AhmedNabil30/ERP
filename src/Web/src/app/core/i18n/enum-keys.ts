import {
  AssignmentLevel,
  Department,
  OperationsSubDepartment,
  ProjectAccessPath,
  Role,
} from '../auth/auth.service';

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
