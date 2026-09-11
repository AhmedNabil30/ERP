import { HttpErrorResponse } from '@angular/common/http';

/**
 * The error shape the API returns.
 *
 * `messageKey` is an i18n key, never a sentence — CLAUDE.md forbids the server sending user-facing
 * prose, so the client resolves it through `I18nService.t`.
 */
export interface KaffProblem {
  readonly status: number;
  readonly code: string;
  readonly messageKey: string;
  /**
   * Every other field the ProblemDetails body carried — `ResultExtensions.Problem`'s
   * `extraExtensions`, e.g. `errors.master.bab_has_active_items`'s `count` (`KAFF-213`, `AC-213-B`).
   * Pass straight through to `I18nService.t`'s `params`, which is what turns the `{count}` placeholder
   * already in both catalogues into the refused count, bidi-isolated.
   */
  readonly extensions: Readonly<Record<string, unknown>>;
}

const UNKNOWN_ERROR_KEY = 'errors.unknown';
const KNOWN_KEYS = new Set(['code', 'messageKey', 'type', 'title', 'status', 'detail', 'instance']);

/** Extracts the problem from an HTTP failure, falling back to a generic key. */
export function toProblem(error: unknown): KaffProblem {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as Record<string, unknown> | null;
    const code = body?.['code'];
    const messageKey = body?.['messageKey'];

    const extensions: Record<string, unknown> = {};
    if (body) {
      for (const [key, value] of Object.entries(body)) {
        if (!KNOWN_KEYS.has(key)) {
          extensions[key] = value;
        }
      }
    }

    return {
      status: error.status,
      code: typeof code === 'string' ? code : `http.${error.status}`,
      messageKey: typeof messageKey === 'string' ? messageKey : UNKNOWN_ERROR_KEY,
      extensions,
    };
  }

  return { status: 0, code: 'client.unknown', messageKey: UNKNOWN_ERROR_KEY, extensions: {} };
}
