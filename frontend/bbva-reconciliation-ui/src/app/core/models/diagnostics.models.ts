/** Contracts for the diagnostics and sandbox-certification endpoints. */

export interface HealthStatus {
  readonly status: 'healthy' | 'unhealthy';
  readonly baseUrl: string;
  readonly tokenAcquired: boolean;
  readonly tokenExpiresAt: string | null;
  readonly tokenValidForSeconds: number | null;
  readonly detail: string | null;
}

export interface SignaturePreviewRequest {
  readonly method: string;
  readonly path: string;
  readonly body: string | null;
}

/**
 * Everything the signing step produces, without sending anything.
 *
 * BBVA answers a bad signature with a bare 401 and no diagnostics, so `stringToSign` —
 * the exact LF-separated bytes that were signed — is the single most useful field here.
 */
export interface SignaturePreview {
  readonly method: string;
  readonly path: string;
  readonly host: string;
  readonly body: string;
  readonly digest: string;
  readonly date: string;
  readonly stringToSign: string;
  readonly signature: string;
  readonly headers: Readonly<Record<string, string>>;
}

export type SandboxGroup = 'search' | 'detail';

export interface SandboxFollowUp {
  readonly step: number;
  readonly requestBody: unknown;
  readonly expectedStatusCode: number;
  readonly expectedResponse: unknown;
}

/** One scenario from the official BBVA sandbox test plan workbook. */
export interface SandboxTestCase {
  readonly id: string;
  readonly group: SandboxGroup;
  readonly name: string;
  /** The original Spanish description from the workbook. */
  readonly description: string;
  readonly method: string;
  readonly path: string;
  readonly expectedStatusCode: number;
  readonly requestBody: unknown;
  readonly expectedResponse: unknown;
  readonly pathParameters?: Readonly<Record<string, string>>;
  readonly followUp?: readonly SandboxFollowUp[];
}

export interface SandboxTestPlan {
  readonly source: string;
  readonly api: string;
  readonly sandboxBaseUrl: string;
  readonly totalCases: number;
  readonly totalRequests: number;
  readonly cases: readonly SandboxTestCase[];
}

/**
 * - `Passed`: status and body both match the documentation.
 * - `Drifted`: status matches, body moved — normally the sandbox data set, not a defect.
 * - `Failed`: the status code differs from what the documentation promises.
 * - `Errored`: the call could not be made at all.
 */
export type SandboxOutcome = 'Passed' | 'Drifted' | 'Failed' | 'Errored';

export interface JsonDifference {
  /** Located path, e.g. `movements[0].postedBalance.amount`. */
  readonly path: string;
  readonly expected: string | null;
  readonly actual: string | null;
}

export interface SandboxCaseResult {
  readonly caseId: string;
  readonly description: string;
  readonly outcome: SandboxOutcome;
  readonly expectedStatusCode: number;
  readonly actualStatusCode: number;
  readonly expectedResponse: unknown;
  readonly actualResponse: unknown;
  readonly differences: readonly JsonDifference[];
  readonly elapsedMilliseconds: number;
  readonly statusMatches: boolean;
  readonly bodyMatches: boolean;
}
