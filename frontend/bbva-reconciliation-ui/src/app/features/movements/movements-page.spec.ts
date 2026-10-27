import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { BbvaApi } from '../../core/api/bbva-api';
import type { MoneyFlow, Movement } from '../../core/models/movement.models';
import { MovementsPage } from './movements-page';

/**
 * BBVA already signs transaction.amount — EXPENSE movements arrive negative, INCOME
 * movements arrive positive (verified against the real sandbox response,
 * evidence/2026-09-21/sandbox-run-results.json: 154 of 154 EXPENSE amounts negative, 0
 * positive). The `totals` computed signal must therefore sum income and expense rather
 * than subtract them, or the NETO figure shown to operators comes out with the wrong sign
 * on any account that has expenses.
 */

function buildMovement(moneyFlow: MoneyFlow, amount: number): Movement {
  return {
    id: 'PE00000000000000000000000000001',
    comments: null,
    isFinanceable: false,
    moneyFlow,
    status: 'SETTLED',
    isKnownStatus: true,
    valuationDate: '2024-11-27T00:00:00.000-0500',
    signedAmount: amount,
    concept: { id: '612', name: 'CARGO POR TRASPASO' },
    postedBalance: { amount: 0, currency: 'PEN' },
    contract: { id: 'PE001101300105109846', number: '001101300105109846' },
    operation: { id: 'PE00000000000000000000000000001', date: '2024-11-27T17:40:00.000-0500' },
    transaction: { id: '1', amount, currency: 'PEN' },
  };
}

function createComponent(): MovementsPage {
  TestBed.configureTestingModule({
    imports: [MovementsPage],
    // totals is a pure reduction over the seeded `movements` signal, so BbvaApi never
    // needs to make a real call here — a stub avoids pulling HttpClient into these tests.
    providers: [{ provide: BbvaApi, useValue: {} }],
  });

  return TestBed.createComponent(MovementsPage).componentInstance;
}

describe('MovementsPage totals', () => {
  it('sums a negative net for an expenses-only account (SRCH-05 sandbox shape)', () => {
    // Real sandbox response for contract 001101300105109846 (SRCH-05, "cuenta bloqueada"):
    // nine EXPENSE movements, all negative, no INCOME. This is the reported regression —
    // the UI showed "NETO +5,283.05 PEN" when the correct figure is -5,283.05 PEN. Under
    // the old `income - expense` code this test fails, because 0 - (-5283.05) is positive.
    const component = createComponent();

    component['movements'].set([
      buildMovement('EXPENSE', -500),
      buildMovement('EXPENSE', -677),
      buildMovement('EXPENSE', -0.05),
      buildMovement('EXPENSE', -1000),
      buildMovement('EXPENSE', -560),
      buildMovement('EXPENSE', -400),
      buildMovement('EXPENSE', -546),
      buildMovement('EXPENSE', -800),
      buildMovement('EXPENSE', -800),
    ]);

    const totals = component['totals']();

    expect(totals.income).toBe(0);
    expect(totals.expense).toBeCloseTo(-5283.05, 2);
    expect(totals.net).toBeCloseTo(-5283.05, 2);
    expect(totals.net).toBeLessThan(0);
  });

  it('keeps a positive net equal to income for an income-only account (SRCH-03 sandbox shape)', () => {
    // Real sandbox response for contract 001101300210484377 (SRCH-03): four INCOME
    // movements, no EXPENSE. This shape passes under both the buggy `income - expense` and
    // the fixed `income + expense`, since expense is 0 either way — it guards against
    // "fixing" the sign regression by negating expense instead of summing it.
    const component = createComponent();

    component['movements'].set([
      buildMovement('INCOME', 430334.77),
      buildMovement('INCOME', 416364.92),
      buildMovement('INCOME', 430152.75),
      buildMovement('INCOME', 429701.5),
    ]);

    const totals = component['totals']();

    expect(totals.expense).toBe(0);
    expect(totals.income).toBeCloseTo(1706553.94, 2);
    expect(totals.net).toBe(totals.income);
    expect(totals.net).toBeGreaterThan(0);
  });

  it('sums to a negative net when expenses outweigh income on a mixed account', () => {
    // Both flows present, with round amounts chosen so the arithmetic is exact. The old
    // `income - expense` code would report +2,000 here; the correct net is -1,000 — an
    // unmistakably different sign, not a rounding difference.
    const component = createComponent();

    component['movements'].set([
      buildMovement('INCOME', 300),
      buildMovement('INCOME', 200),
      buildMovement('EXPENSE', -1200),
      buildMovement('EXPENSE', -300),
    ]);

    const totals = component['totals']();

    expect(totals.income).toBe(500);
    expect(totals.expense).toBe(-1500);
    expect(totals.net).toBe(-1000);
  });
});
