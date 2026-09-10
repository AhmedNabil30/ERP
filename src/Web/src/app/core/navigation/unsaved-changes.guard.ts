import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

import { I18nService } from '../i18n/i18n.service';

/** Implemented by any routed form page that can hold edits the operator has not saved yet. */
export interface UnsavedChangesAware {
  hasUnsavedChanges(): boolean;
}

/**
 * A half-typed catalogue item lost to a stray back gesture on a phone is a real loss on a building
 * site — apple-erp-design §7.3, KAFF-202's own "warn before navigating away from unsaved changes".
 *
 * **`window.confirm`, not a custom dialog.** No shared confirm-dialog component exists in this
 * codebase yet (`ux/components.md` §11 describes one; nothing has built it), and a hard reload or tab
 * close cannot be intercepted by anything except the browser's own `beforeunload` mechanics anyway —
 * `confirm()` is the native platform feature for the in-app navigation half of the same problem, and
 * `catalogue-form-page.ts`'s own `beforeunload` listener covers the other half.
 */
export const confirmUnsavedChangesGuard: CanDeactivateFn<UnsavedChangesAware> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }

  const i18n = inject(I18nService);
  return confirm(i18n.t('form.confirm.discard_changes'));
};
