import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  departmentKey,
  operationsSubDepartmentKey,
  roleKey,
} from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UserSummary, UsersApi } from '../../../core/users/users.api';

/**
 * S-006 · the user list. The Owner's home, and the way into every identity act in the system.
 *
 * **Active and inactive in one list, not two.** D-049 ruling 5: *"Leavers are deactivated, never
 * deleted."* A list filtered to active accounts would hide the only people `POST
 * /api/users/{id}/reactivate` can act on, so the inactive chip is what distinguishes them —
 * `users.state.inactive`, neutral styling, not an error colour (S-006).
 *
 * **No search box and no filter chips.** S-006 draws both and `GET /api/users` carries neither, which
 * is a deliberate omission recorded rather than a gap discovered: none of `AC-127-A`…`AC-127-I` asks
 * for either, and a query parameter with no criterion behind it would have been a second
 * implementation of `ListClients`' matching rules written on the assumption that users are searched
 * the way clients are. Owed.
 *
 * **The role and department resolve from the catalogue**, through the exhaustive `switch` of
 * `enum-keys.ts` rather than a key built by concatenation — `ux/rtl-and-i18n.md` §6 hard rule 4, so a
 * tenth role is a compile error under `strictTemplates` and not a raw key on screen.
 */
@Component({
  selector: 'kaff-user-list-page',
  imports: [RouterLink],
  templateUrl: './user-list-page.html',
  styleUrl: './user-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserListPage {
  private readonly api = inject(UsersApi);

  protected readonly i18n = inject(I18nService);
  protected readonly roleKey = roleKey;
  protected readonly departmentKey = departmentKey;
  protected readonly operationsSubDepartmentKey = operationsSubDepartmentKey;

  protected readonly users = signal<readonly UserSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    void this.reload();
  }

  protected trackUser(_index: number, user: UserSummary): string {
    return user.id;
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.users.set(await this.api.list());
    } catch (error) {
      // A 403 here is the mechanism working, not a bug — spec.md §9 makes the server the decider, and
      // a role without UserManage reaching this route by URL is exactly what it decides. The key is
      // the server's; this file does not choose it.
      this.failure.set(toProblem(error).messageKey);
      this.users.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
