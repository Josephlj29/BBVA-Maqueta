/**
 * Mirrors the contracts exposed by the Bbva.Reconciliation.Api backend.
 *
 * These are the API's shapes, not BBVA's raw wire format: the backend already normalises
 * the bank's response and signs every outbound call. Nothing here should be sent to
 * BBVA directly — the browser never holds the client secret or the signing key.
 */

export type MoneyFlow = 'INCOME' | 'EXPENSE';

/** The eleven states BBVA documents. Any other value arrives with `isKnownStatus: false`. */
export type TransactionStatus =
  | 'SETTLED'
  | 'PENDING'
  | 'SENT'
  | 'RETURNED'
  | 'REJECTED'
  | 'AUTHORIZATION_PENDING'
  | 'CANCELED'
  | 'REFUSED'
  | 'REFUND'
  | 'CHARGEBACKED'
  | 'CHARGED';

export interface SearchMovementsRequest {
  /** BBVA accepts exactly one 18-digit contract. */
  readonly contracts: readonly string[];
  readonly currency?: string | null;
  /** ISO-8601, `yyyy-MM-ddTHH:mm:ss.SSSZ`. Not before 2024-01-01. */
  readonly fromDate?: string | null;
  /** Required whenever `fromDate` is supplied. Cannot be in the future. */
  readonly toDate?: string | null;
  readonly moneyFlow?: MoneyFlow | null;
  readonly keyword?: string | null;
  readonly pageSize?: number | null;
  /** Opaque cursor from a previous page's `pagination.next`. Never build one by hand. */
  readonly paginationKey?: string | null;
}

export interface Concept {
  readonly id: string;
  readonly name: string;
}

export interface Money {
  readonly amount: number;
  readonly currency: string;
}

export interface ContractRef {
  readonly id: string;
  readonly number: string;
}

export interface OperationRef {
  readonly id: string;
  readonly date: string | null;
}

export interface TransactionRef {
  readonly id: string;
  readonly amount: number;
  readonly currency: string;
}

export interface Movement {
  readonly id: string;
  readonly comments: string | null;
  readonly isFinanceable: boolean;
  readonly moneyFlow: MoneyFlow;
  /** Echoed verbatim from BBVA, including values this client does not recognise. */
  readonly status: string | null;
  readonly isKnownStatus: boolean;
  readonly valuationDate: string | null;
  /** Negative for expenses. BBVA reports a positive magnitude plus a direction. */
  readonly signedAmount: number;
  readonly concept: Concept;
  readonly postedBalance: Money;
  readonly contract: ContractRef;
  readonly operation: OperationRef;
  readonly transaction: TransactionRef;
}

export interface Pagination {
  readonly totalPages: number | null;
  readonly totalElements: number | null;
  readonly pageSize: number | null;
  readonly first: string | null;
  readonly last: string | null;
  readonly previous: string | null;
  readonly next: string | null;
  readonly hasNext: boolean;
}

export interface MovementPage {
  readonly movements: readonly Movement[];
  readonly pagination: Pagination | null;
  /**
   * Set when BBVA returned no matches, typically `"No transactions to show."`.
   * This accompanies a 200 — it is an empty result, not a failure.
   */
  readonly infoMessage: string | null;
}

/** One entry of BBVA's uniform error envelope, preserved by the backend. */
export interface BbvaMessage {
  readonly code: string;
  readonly message: string;
  readonly type: 'ERROR' | 'INFO';
}

/** RFC 9457 problem details, with BBVA's own envelope carried in `messages`. */
export interface ApiProblem {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly messages?: readonly BbvaMessage[];
  readonly traceId?: string;
}
