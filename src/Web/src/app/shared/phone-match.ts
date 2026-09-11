/**
 * Who already holds a phone number, from any master's `phone-check` endpoint (clients, employees,
 * and any later population). Empty when nobody does.
 *
 * Lifted out of `core/clients/clients.api.ts` (S-024, decisions.md D-141/D-146) so the employee
 * register's `phone-check` shares one shape with the client one instead of a second copy that could
 * drift from it.
 */
export interface PhoneMatch {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly isArchived: boolean;
}
