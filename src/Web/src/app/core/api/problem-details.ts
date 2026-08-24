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
}

const UNKNOWN_ERROR_KEY = 'errors.unknown';

/** Extracts the problem from an HTTP failure, falling back to a generic key. */
export function toProblem(error: unknown): KaffProblem {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { code?: string; messageKey?: string } | null;

    return {
      status: error.status,
      code: body?.code ?? `http.${error.status}`,
      messageKey: body?.messageKey ?? UNKNOWN_ERROR_KEY,
    };
  }

  return { status: 0, code: 'client.unknown', messageKey: UNKNOWN_ERROR_KEY };
}
