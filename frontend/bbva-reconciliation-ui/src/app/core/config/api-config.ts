import { InjectionToken } from '@angular/core';

/**
 * Base URL of the Bbva.Reconciliation.Api backend.
 *
 * Injected rather than imported from an environment file so the value can be swapped in
 * tests and, in a container deployment, read from a runtime-provided config without
 * rebuilding the bundle.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => 'http://localhost:5150/api/v1',
});
