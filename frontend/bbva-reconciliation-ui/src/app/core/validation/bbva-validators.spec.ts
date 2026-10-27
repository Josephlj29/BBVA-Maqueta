import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';

import {
  accountNumberValidator,
  dateRangeValidator,
  pageSizeValidator,
  toWireDate,
  transactionIdValidator,
} from './bbva-validators';

/**
 * These assert BBVA's error wording character for character, against the same sandbox
 * test-plan cases the backend is pinned to. If the client and the server ever disagree on
 * a message, an operator comparing this screen with the bank's console sees two different
 * sentences for one problem — so the strings are the contract, not the behaviour alone.
 */

function check(value: unknown, validate: ReturnType<typeof accountNumberValidator>) {
  return validate(new FormControl(value))?.['bbva'] as string | undefined;
}

describe('accountNumberValidator', () => {
  const validate = accountNumberValidator();

  it('accepts an 18-digit contract', () => {
    expect(check('001101300210484377', validate)).toBeUndefined();
  });

  it('reports a missing contract with the bank wording', () => {
    expect(check('', validate)).toBe('Mandatory parameter contracts not informed.');
  });

  it.each(['0011013001051180004', '00110130210522635'])('rejects %s by length', (value) => {
    expect(check(value, validate)).toBe('The account number must be 18 digits long.');
  });

  it('rejects a non-numeric contract of the right length', () => {
    // SRCH-15: 18 characters, but not all digits.
    expect(check('001101300F0.1*9854', validate)).toBe('The account number accepts only numbers.');
  });

  it('checks length before charset, as the bank does', () => {
    expect(check('0011013001051180ABC', validate)).toBe('The account number must be 18 digits long.');
  });
});

describe('transactionIdValidator', () => {
  const validate = transactionIdValidator();

  it('accepts a 31-character alphanumeric id', () => {
    expect(check('PE00110201300105109846000000014', validate)).toBeUndefined();
  });

  it.each(['PE001102013002105154930000000066', 'PE0011020130021051549300000065'])(
    'rejects %s by length',
    (value) => {
      expect(check(value, validate)).toBe('The transaction number must be 31 characters.');
    },
  );

  it('rejects punctuation at the right length', () => {
    // DTL-12: exactly 31 characters containing ';', '*' and '='.
    expect(check('PE0;110201300210515*930000=0065', validate)).toBe(
      'The transaction number only accepts letters and numbers.',
    );
  });
});

describe('pageSizeValidator', () => {
  const validate = pageSizeValidator();

  it('allows an absent page size', () => {
    expect(check('', validate)).toBeUndefined();
  });

  it('allows a positive page size', () => {
    expect(check(50, validate)).toBeUndefined();
  });

  it.each([0, -1])('rejects %s', (value) => {
    expect(check(value, validate)).toBe('The page size must be greater than zero.');
  });
});

describe('dateRangeValidator', () => {
  const validate = dateRangeValidator('fromDate', 'toDate');

  function range(from: string, to: string) {
    const group = new FormGroup({ fromDate: new FormControl(from), toDate: new FormControl(to) });
    return validate(group)?.['dateRange'] as string | undefined;
  }

  it('allows both bounds absent', () => {
    expect(range('', '')).toBeUndefined();
  });

  it('allows a valid range', () => {
    expect(range('2024-11-01', '2024-11-21')).toBeUndefined();
  });

  it('requires toDate once fromDate is supplied', () => {
    expect(range('2024-06-25', '')).toBe('Mandatory parameter operationDate.toDate not informed.');
  });

  it('requires fromDate once toDate is supplied', () => {
    expect(range('', '2024-11-18')).toBe(
      'Mandatory parameter operationDate.fromDate not informed.',
    );
  });

  it('rejects a fromDate before the supported epoch', () => {
    expect(range('2023-12-31', '2024-11-20')).toBe(
      'The parameter operationDate.fromDate must be later than or equal to the date ' +
        '2024-01-01T00:00:00.000Z',
    );
  });

  it('accepts the epoch boundary itself', () => {
    expect(range('2024-01-01', '2024-11-20')).toBeUndefined();
  });

  it('rejects a toDate in the future', () => {
    expect(range('2024-04-20', '2099-12-26')).toBe(
      'The parameter operationDate.toDate must be before than or equal to the current date.',
    );
  });

  it('rejects a toDate earlier than fromDate', () => {
    expect(range('2024-11-10', '2024-11-01')).toBe(
      'The parameter operationDate.toDate value must be later than operationDate.fromDate value.',
    );
  });
});

describe('toWireDate', () => {
  it('converts a date input to the format BBVA demands', () => {
    expect(toWireDate('2024-11-01')).toBe('2024-11-01T00:00:00.000Z');
  });

  it.each(['', '   ', null, undefined])('maps %s to null', (value) => {
    expect(toWireDate(value)).toBeNull();
  });
});
