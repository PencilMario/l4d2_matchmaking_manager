import { CoreApiError } from '../api/core-client';

export function isUnauthorized(error: unknown): error is CoreApiError {
  return error instanceof CoreApiError && error.status === 401;
}

export function describeCoreError(error: unknown): string {
  return error instanceof CoreApiError ? error.code : 'core_request_failed';
}
