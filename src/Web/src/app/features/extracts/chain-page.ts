import { DestroyRef, Directive, Signal, TemplateRef, effect, inject, signal } from '@angular/core';

import { toProblem } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../core/layout/header-actions.service';
import { projectPermissions } from './extract-display';

/**
 * What every screen of the extract chain shares: the one primary in the shell's slot (KAFF-936), the busy state,
 * and a refusal shown as the server worded it. A screen says whether it has a primary now; while a sheet or a
 * dialog is open it has none, because the sheet carries its own (README "Extract chain", "Primary is in the shell
 * slot on every screen").
 */
@Directive()
export abstract class ChainPage {
  protected readonly i18n = inject(I18nService);
  protected readonly auth = inject(AuthService);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly actionErrorParams = signal<Record<string, string | number>>({});

  /** The `#primaryAction` template. */
  protected abstract readonly primaryTemplate: Signal<TemplateRef<unknown> | undefined>;
  /** True when the screen has a primary to place now. */
  protected abstract readonly hasPrimary: Signal<boolean>;

  constructor() {
    effect(() => this.headerActions.set(this.hasPrimary() ? (this.primaryTemplate() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  /** What this session holds on the project. Convenience only: every route checks role and assignment again. */
  protected held(projectId: string): ReadonlySet<string> {
    return projectPermissions(this.auth.current(), projectId);
  }

  /** Runs a write. True when it went through; on a refusal the server's key and its parameters are shown. */
  protected async act(call: () => Promise<unknown>): Promise<boolean> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await call();
      return true;
    } catch (error) {
      const problem = toProblem(error);
      const params: Record<string, string | number> = {};
      for (const [key, value] of Object.entries(problem.extensions)) {
        if (typeof value === 'string' || typeof value === 'number') {
          params[key] = value;
        }
      }
      this.actionErrorParams.set(params);
      this.actionError.set(problem.messageKey);
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
