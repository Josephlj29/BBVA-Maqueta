import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import { API_BASE_URL } from '../config/api-config';
import type {
  HealthStatus,
  SandboxCaseResult,
  SandboxTestPlan,
  SignaturePreview,
  SignaturePreviewRequest,
} from '../models/diagnostics.models';
import type {
  PlaygroundExecution,
  PlaygroundExecutionRequest,
} from '../models/playground.models';
import type {
  ApiProblem,
  Movement,
  MovementPage,
  SearchMovementsRequest,
} from '../models/movement.models';

/**
 * A failure surfaced by the backend, carrying BBVA's own error code and wording.
 *
 * The bank's message is preserved verbatim rather than paraphrased, because operators
 * reconcile what this UI shows against what BBVA's own console and the sandbox test plan
 * say. A reworded message breaks that comparison.
 */
export class BbvaApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly traceId?: string,
  ) {
    super(message);
    this.name = 'BbvaApiError';
  }

  /** True when BBVA refused the account rather than rejecting the request shape. */
  get isAccessDenied(): boolean {
    return this.code === 'forbidden';
  }

  static fromResponse(response: HttpErrorResponse): BbvaApiError {
    const problem = response.error as ApiProblem | null | undefined;
    const first = problem?.messages?.[0];

    if (first) {
      return new BbvaApiError(response.status, first.code, first.message, problem?.traceId);
    }

    if (problem?.detail) {
      return new BbvaApiError(response.status, 'badRequest', problem.detail, problem.traceId);
    }

    // Status 0 means the request never reached the backend at all.
    return new BbvaApiError(
      response.status,
      'unreachable',
      response.status === 0
        ? 'Could not reach the API. Check that the backend is running.'
        : `The API returned an unexpected HTTP ${response.status}.`,
    );
  }
}

@Injectable({ providedIn: 'root' })
export class BbvaApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  searchMovements(request: SearchMovementsRequest): Observable<MovementPage> {
    return this.http
      .post<MovementPage>(`${this.baseUrl}/transactions/search`, request)
      .pipe(catchError(toApiError));
  }

  getMovement(accountId: string, transactionId: string): Observable<Movement> {
    return this.http
      .get<Movement>(
        `${this.baseUrl}/accounts/${encodeURIComponent(accountId)}` +
          `/transactions/${encodeURIComponent(transactionId)}`,
      )
      .pipe(catchError(toApiError));
  }

  getHealth(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${this.baseUrl}/health`).pipe(catchError(toApiError));
  }

  previewSignature(request: SignaturePreviewRequest): Observable<SignaturePreview> {
    return this.http
      .post<SignaturePreview>(`${this.baseUrl}/diagnostics/signature-preview`, request)
      .pipe(catchError(toApiError));
  }

  executePlayground(request: PlaygroundExecutionRequest): Observable<PlaygroundExecution> {
    return this.http
      .post<PlaygroundExecution>(`${this.baseUrl}/playground/execute`, request)
      .pipe(catchError(toApiError));
  }

  getSandboxPlan(): Observable<SandboxTestPlan> {
    return this.http
      .get<SandboxTestPlan>(`${this.baseUrl}/sandbox/cases`)
      .pipe(catchError(toApiError));
  }

  runSandboxCase(caseId: string, compareVolatileValues = false): Observable<SandboxCaseResult> {
    return this.http
      .post<SandboxCaseResult>(
        `${this.baseUrl}/sandbox/cases/${encodeURIComponent(caseId)}/run` +
          `?compareVolatileValues=${compareVolatileValues}`,
        {},
      )
      .pipe(catchError(toApiError));
  }
}

function toApiError(response: HttpErrorResponse): Observable<never> {
  return throwError(() => BbvaApiError.fromResponse(response));
}
