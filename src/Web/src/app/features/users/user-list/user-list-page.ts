import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  TemplateRef,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { DepartmentSummary, DepartmentsApi } from '../../../core/departments/departments.api';
import { operationsSubDepartmentKey, roleKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { UserSummary, UsersApi } from '../../../core/users/users.api';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffTableHeader, TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * KAFF-926 finding 7: `GET /api/users` (`UsersApi.list`) returns every account, active and inactive,
 * in one call with no filter parameter — there is no server-side equivalent of `ClientListFilter` /
 * `CatalogueItemListFilter` to send. The filter below is client-side over the one response already
 * held in {@link UserListPage.users}, and only the *chosen value* round-trips through the URL
 * (`?status=`), the same query-param convention `catalogue-list-page.ts` uses for its own filter.
 */
type UserListFilter = 'active' | 'archived' | 'all';

const FILTERS: readonly UserListFilter[] = ['active', 'archived', 'all'];

/**
 * S-006 · the user list. The Owner's home, and the way into every identity act in the system.
 *
 * **Active and inactive in one list, not two.** D-049 ruling 5: *"Leavers are deactivated, never
 * deleted."* A list filtered to active accounts would hide the only people `POST
 * /api/users/{id}/reactivate` can act on, so the inactive chip is what distinguishes them —
 * `users.state.inactive`, neutral styling, not an error colour (S-006).
 *
 * **Still no search box.** S-006 draws one and `GET /api/users` carries no search parameter, which
 * is a deliberate omission recorded rather than a gap discovered: none of `AC-127-A`…`AC-127-I` asks
 * for it, and a query parameter with no criterion behind it would have been a second implementation
 * of `ListClients`' matching rules written on the assumption that users are searched the way clients
 * are. Owed.
 *
 * **The Active/Archived/All filter (KAFF-926) is client-side, unlike its siblings.** `catalogue`'s
 * and `client`'s filters are server round trips; `GET /api/users` has no filter parameter at all
 * (see {@link UserListFilter}'s doc), so this one filters the one already-fetched response instead.
 * Only the chosen value round-trips through `?status=`, the same URL convention.
 *
 * **The role and department resolve from the catalogue**, through the exhaustive `switch` of
 * `enum-keys.ts` rather than a key built by concatenation — `ux/rtl-and-i18n.md` §6 hard rule 4, so a
 * tenth role is a compile error under `strictTemplates` and not a raw key on screen.
 */
@Component({
  selector: 'kaff-user-list-page',
  imports: [RouterLink, KaffTableHeader, KaffTableRow, KaffBadge, KaffButton, KaffSegmentedFilter],
  templateUrl: './user-list-page.html',
  styleUrl: './user-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserListPage {
  private readonly api = inject(UsersApi);
  private readonly departmentsApi = inject(DepartmentsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly roleKey = roleKey;
  protected readonly operationsSubDepartmentKey = operationsSubDepartmentKey;
  protected readonly filters = FILTERS;

  /** KAFF-321 — department names are master data now, fetched once rather than an exhaustive switch. */
  private readonly departments = signal<readonly DepartmentSummary[]>([]);

  protected departmentLabel(departmentId: string): string | null {
    const department = this.departments().find((candidate) => candidate.id === departmentId);

    if (department === undefined) {
      return null;
    }

    return this.i18n.locale() === 'en' ? department.nameEn : department.nameAr;
  }

  /**
   * Grid columns for `kaff-table-row`: name (flexes) · role/department meta · username · phone.
   * Fixed px per fixed-width column, `minmax(0, 1fr)` for name — the one column whose content
   * length varies enough to need it (KAFF-926 finding 1). Username and phone are fixed-width
   * Latin figures; the meta column carries the longest Arabic role/department strings
   * (`enum.Role.MarketingSales` = "التسويق والمبيعات", the longest role label) plus an optional
   * sub-department and the inactive badge, so it gets the widest fixed track and wraps.
   */
  protected readonly rowColumns = 'minmax(0, 1fr) 14rem 9rem 9rem';

  /** `KAFF-925`: same column order the rows use — name · role/department meta · username · phone. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'users.field.full_name' },
    { labelKey: 'users.field.role' },
    { labelKey: 'users.field.username' },
    { labelKey: 'users.field.phone' },
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<UserListFilter>[] =
    FILTERS.map((filter) => ({ value: filter, labelKey: this.filterKey(filter) }));

  /** Bound by name from `?status=` — `withComponentInputBinding` (`app.config.ts`), same mechanism
   *  `catalogue-list-page.ts`'s `status` input uses. */
  readonly status = input<UserListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly users = signal<readonly UserSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  /** The one response from {@link UsersApi.list}, filtered client-side — see the type doc above. */
  protected readonly filteredUsers = computed<readonly UserSummary[]>(() => {
    const filter = this.statusFilter();
    const users = this.users();
    switch (filter) {
      case 'active':
        return users.filter((user) => user.isActive);
      case 'archived':
        return users.filter((user) => !user.isActive);
      case 'all':
        return users;
    }
  });

  /** KAFF-923's top-bar action slot — projects the create button there instead of the bottom link
   *  finding 5 replaces (`Main.dc.html` "بند جديد"). No other feature page wires this slot yet, so
   *  this is the first caller; cleared on destroy so the next page does not inherit it. */
  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');

  constructor() {
    void this.reload();
    void this.loadDepartments();

    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));
  }

  protected trackUser(_index: number, user: UserSummary): string {
    return user.id;
  }

  protected async onFilter(filter: UserListFilter): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: filter },
      queryParamsHandling: 'merge',
    });
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/users/new']);
  }

  private filterKey(filter: UserListFilter): string {
    switch (filter) {
      case 'active':
        return 'users.filter.active';
      case 'archived':
        return 'users.filter.archived';
      case 'all':
        return 'users.filter.all';
    }
  }

  private async loadDepartments(): Promise<void> {
    try {
      // 'all' — a leaver's row may still name an archived department (AC-321-E), and this list shows
      // leavers too (D-049 ruling 5).
      this.departments.set(await this.departmentsApi.list('all'));
    } catch {
      this.departments.set([]);
    }
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
