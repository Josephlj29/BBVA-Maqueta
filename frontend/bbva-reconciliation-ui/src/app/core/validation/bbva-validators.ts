import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Client-side mirrors of the rules the backend and BBVA both enforce.
 *
 * The messages are BBVA's own wording, character for character. That is deliberate: an
 * operator comparing this screen against the sandbox test plan or BBVA's console should
 * see the same sentence in all three places. Validating here does not replace the server
 * check — it just means an obviously malformed search fails in a keystroke rather than
 * after an 800 ms round trip, and never spends API quota.
 */

/** Earliest date BBVA accepts for `operationDate.fromDate`. */
export const MINIMUM_FROM_DATE = '2024-01-01';

export const ACCOUNT_LENGTH = 18;
export const TRANSACTION_ID_LENGTH = 31;

/** An 18-digit, numeric-only account number. */
export function accountNumberValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().trim();

    if (!value) {
      return { bbva: 'Mandatory parameter contracts not informed.' };
    }

    // Length before charset, matching the precedence BBVA itself applies.
    if (value.length !== ACCOUNT_LENGTH) {
      return { bbva: 'The account number must be 18 digits long.' };
    }

    if (!/^\d+$/.test(value)) {
      return { bbva: 'The account number accepts only numbers.' };
    }

    return null;
  };
}

/** A 31-character alphanumeric movement identifier. */
export function transactionIdValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().trim();

    if (!value) {
      return { bbva: 'Mandatory parameter transaction-id not informed.' };
    }

    if (value.length !== TRANSACTION_ID_LENGTH) {
      return { bbva: 'The transaction number must be 31 characters.' };
    }

    if (!/^[A-Za-z0-9]+$/.test(value)) {
      return { bbva: 'The transaction number only accepts letters and numbers.' };
    }

    return null;
  };
}

export function pageSizeValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;

    if (value === null || value === undefined || value === '') {
      return null;
    }

    return Number(value) > 0 ? null : { bbva: 'The page size must be greater than zero.' };
  };
}

/**
 * Cross-field rule for the date range: both bounds or neither, `fromDate` no earlier than
 * 2024-01-01, `toDate` no later than today, and `toDate` strictly after `fromDate`.
 *
 * Applied to the form group rather than to either control because every one of these
 * checks needs both values.
 */
export function dateRangeValidator(fromKey: string, toKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const from = (group.get(fromKey)?.value ?? '').toString().trim();
    const to = (group.get(toKey)?.value ?? '').toString().trim();

    if (!from && !to) {
      return null;
    }

    if (from && !to) {
      return { dateRange: 'Mandatory parameter operationDate.toDate not informed.' };
    }

    if (!from && to) {
      return { dateRange: 'Mandatory parameter operationDate.fromDate not informed.' };
    }

    if (from < MINIMUM_FROM_DATE) {
      return {
        dateRange:
          'The parameter operationDate.fromDate must be later than or equal to the date ' +
          '2024-01-01T00:00:00.000Z',
      };
    }

    if (to > today()) {
      return {
        dateRange:
          'The parameter operationDate.toDate must be before than or equal to the current date.',
      };
    }

    if (to <= from) {
      return {
        dateRange:
          'The parameter operationDate.toDate value must be later than ' +
          'operationDate.fromDate value.',
      };
    }

    return null;
  };
}

/** Today as `yyyy-MM-dd`, for comparison against `<input type="date">` values. */
export function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Converts a `yyyy-MM-dd` date input into the wire format BBVA demands,
 * `yyyy-MM-ddTHH:mm:ss.SSSZ`.
 */
export function toWireDate(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim();
  return trimmed ? `${trimmed}T00:00:00.000Z` : null;
}

/** Pulls the first BBVA-worded message out of a control or group. */
export function bbvaError(control: AbstractControl | null | undefined): string | null {
  if (!control || (!control.touched && !control.dirty)) {
    return null;
  }

  return (control.errors?.['bbva'] as string) ?? (control.errors?.['dateRange'] as string) ?? null;
}
