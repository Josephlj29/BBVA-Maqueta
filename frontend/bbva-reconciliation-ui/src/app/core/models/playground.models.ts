import type { SignaturePreview } from './diagnostics.models';

export interface PlaygroundExecutionRequest {
  readonly method: 'GET' | 'POST';
  /** Absolute BBVA path under `/pe/business-reconciliation/v1`. */
  readonly path: string;
  readonly body: string | null;
}

/**
 * The raw outcome of a playground call, plus everything the signing step produced.
 *
 * The signature travels with the response on purpose: when BBVA answers 401 there is no
 * other way to tell a credential problem from a malformed `stringToSign`.
 */
export interface PlaygroundExecution {
  readonly method: string;
  readonly path: string;
  /** BBVA's status code, or 0 when the call could not be made at all. */
  readonly statusCode: number;
  readonly body: unknown;
  readonly rawBody: string;
  readonly failureReason: string | null;
  readonly elapsedMilliseconds: number;
  readonly signature: SignaturePreview;
}
