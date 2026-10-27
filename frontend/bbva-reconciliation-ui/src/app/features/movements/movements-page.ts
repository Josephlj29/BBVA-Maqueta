import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { BbvaApi, BbvaApiError } from '../../core/api/bbva-api';
import { SANDBOX_CONTRACTS } from '../../core/config/sandbox-contracts';
import type { Movement, MovementPage, SearchMovementsRequest } from '../../core/models/movement.models';
import {
  accountNumberValidator,
  bbvaError,
  dateRangeValidator,
  MINIMUM_FROM_DATE,
  pageSizeValidator,
  toWireDate,
  today,
} from '../../core/validation/bbva-validators';
import { JsonViewer } from '../../shared/components/json-viewer';
import { MoneyAmount } from '../../shared/components/money-amount';
import { StatusBadge } from '../../shared/components/status-badge';
import { CopyButton } from '../../shared/components/copy-button';
import { MovementDetailPanel } from './movement-detail-panel';

@Component({
  selector: 'app-movements-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, MoneyAmount, StatusBadge, JsonViewer, CopyButton, MovementDetailPanel],
  templateUrl: './movements-page.html',
})
export class MovementsPage {
  private readonly api = inject(BbvaApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly contracts = SANDBOX_CONTRACTS;
  protected readonly minimumDate = MINIMUM_FROM_DATE;
  protected readonly maximumDate = today();

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      contract: ['001101300210484377', [Validators.required, accountNumberValidator()]],
      currency: [''],
      fromDate: [''],
      toDate: [''],
      moneyFlow: [''],
      keyword: [''],
      pageSize: [null as number | null, [pageSizeValidator()]],
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  /**
   * Accumulated results. BBVA's pagination is a one-way opaque cursor, so pages are
   * appended rather than replaced — there is no way to jump back to page 3 by number.
   */
  protected readonly movements = signal<readonly Movement[]>([]);
  protected readonly page = signal<MovementPage | null>(null);
  protected readonly error = signal<BbvaApiError | null>(null);
  protected readonly isSearching = signal(false);
  protected readonly isLoadingMore = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly selected = signal<Movement | null>(null);
  protected readonly showRawJson = signal(false);

  protected readonly isEmpty = computed(
    () => this.hasSearched() && !this.isSearching() && this.movements().length === 0,
  );

  protected readonly hasNext = computed(() => this.page()?.pagination?.hasNext ?? false);

  protected readonly totals = computed(() => {
    let income = 0;
    let expense = 0;

    for (const movement of this.movements()) {
      if (movement.moneyFlow === 'INCOME') {
        income += movement.transaction.amount;
      } else {
        expense += movement.transaction.amount;
      }
    }

    // BBVA already signs transaction.amount (EXPENSE arrives negative), so the net is a
    // plain sum, not a subtraction — subtracting would flip its sign.
    return { income, expense, net: income + expense };
  });

  protected readonly currencyOfResults = computed(
    () => this.movements()[0]?.transaction.currency ?? '',
  );

  protected readonly formError = computed(() => {
    const group = this.form;
    return group.touched || group.dirty ? (group.errors?.['dateRange'] as string | undefined) : undefined;
  });

  protected fieldError(name: string): string | null {
    return bbvaError(this.form.get(name));
  }

  protected pickContract(number: string): void {
    this.form.controls.contract.setValue(number);
    this.form.controls.contract.markAsTouched();
  }

  protected search(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.isSearching.set(true);
    this.error.set(null);
    this.selected.set(null);

    this.api.searchMovements(this.buildRequest(null)).subscribe({
      next: (result) => {
        this.movements.set(result.movements);
        this.page.set(result);
        this.hasSearched.set(true);
        this.isSearching.set(false);
      },
      error: (failure: BbvaApiError) => {
        this.movements.set([]);
        this.page.set(null);
        this.error.set(failure);
        this.hasSearched.set(true);
        this.isSearching.set(false);
      },
    });
  }

  protected loadMore(): void {
    const cursor = this.page()?.pagination?.next;

    if (!cursor || this.isLoadingMore()) {
      return;
    }

    this.isLoadingMore.set(true);

    this.api.searchMovements(this.buildRequest(cursor)).subscribe({
      next: (result) => {
        this.movements.update((current) => [...current, ...result.movements]);
        this.page.set(result);
        this.isLoadingMore.set(false);
      },
      error: (failure: BbvaApiError) => {
        this.error.set(failure);
        this.isLoadingMore.set(false);
      },
    });
  }

  protected reset(): void {
    this.form.reset({
      contract: '001101300210484377',
      currency: '',
      fromDate: '',
      toDate: '',
      moneyFlow: '',
      keyword: '',
      pageSize: null,
    });

    this.movements.set([]);
    this.page.set(null);
    this.error.set(null);
    this.hasSearched.set(false);
    this.selected.set(null);
  }

  protected select(movement: Movement): void {
    this.selected.set(movement);
  }

  protected closeDetail(): void {
    this.selected.set(null);
  }

  private buildRequest(paginationKey: string | null): SearchMovementsRequest {
    const value = this.form.getRawValue();

    return {
      contracts: [value.contract.trim()],
      currency: value.currency || null,
      fromDate: toWireDate(value.fromDate),
      toDate: toWireDate(value.toDate),
      moneyFlow: (value.moneyFlow || null) as SearchMovementsRequest['moneyFlow'],
      keyword: value.keyword?.trim() || null,
      pageSize: value.pageSize ? Number(value.pageSize) : null,
      paginationKey,
    };
  }
}
