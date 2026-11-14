import { HttpErrorResponse } from '@angular/common/http';

import { ProblemDetails } from '../api/models';

/** Error codes the UI has a localized message for (keys under `errors.*`). */
export const KNOWN_ERROR_CODES = [
  'VALIDATION_FAILED',
  'UNAUTHORIZED',
  'INVALID_CREDENTIALS',
  'FORBIDDEN',
  'NOT_FOUND',
  'CLINIC_NOT_FOUND',
  'JOB_NOT_FOUND',
  'FEATURE_NOT_FOUND',
  'FEATURE_NOT_AVAILABLE',
  'SLUG_TAKEN',
  'EMAIL_TAKEN',
  'LAST_ADMIN',
  'JOB_IN_PROGRESS',
  'CLINIC_NOT_ACTIVE',
  'INTERNAL_ERROR',
  'NETWORK',
] as const;

export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/** Stable machine code for an HTTP failure: the API's `code`, or one derived from the status. */
export function errorCode(error: unknown): string {
  const problem = problemOf(error);
  if (problem?.code) return problem.code;
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'NETWORK';
    if (error.status === 401) return 'UNAUTHORIZED';
    if (error.status === 403) return 'FORBIDDEN';
    if (error.status === 404) return 'NOT_FOUND';
    if (error.status >= 500) return 'INTERNAL_ERROR';
  }
  return 'UNKNOWN';
}

export function errorMessageKey(code: string): string {
  return (KNOWN_ERROR_CODES as readonly string[]).includes(code)
    ? `errors.${code}`
    : 'errors.UNKNOWN';
}

/** Field errors of a 400 problem, keyed by field name. */
export function fieldErrors(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}
