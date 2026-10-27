
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';

import { BbvaApi } from '../../core/api/bbva-api';
import type { Movement } from '../../core/models/movement.models';
import { CopyButton } from '../../shared/components/copy-button';
import { JsonViewer } from '../../shared/components/json-viewer';
import { MoneyAmount } from '../../shared/components/money-amount';
import { StatusBadge } from '../../shared/components/status-badge';

/**
 * Slide-over detail for a single movement.
 *
 * The row already carries the full movement, so the panel renders instantly from it and
 * *then* calls `GET /accounts/{id}/transactions/{id}` in the background. That second call
 * is not redundant: it is the only way to exercise the detail endpoint from this screen,
 * and it proves the two endpoints agree. When they disagree, that is worth seeing.
 */
@Component({
  selector: 'app-movement-detail-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyAmount, StatusBadge, JsonViewer, CopyButton],
  template: `
    <div class="fixed inset-0 z-50 flex justify-end">
      <div
        class="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
        (click)="closed.emit()"
        aria-hidden="true"
      ></div>

      <aside
        class="animate-in slide-in-from-right relative flex h-full w-full max-w-xl flex-col
               border-l border-border bg-card shadow-2xl duration-200"
        role="dialog"
        aria-modal="true"
        aria-labelledby="detail-title"
      >
        <header class="flex items-start justify-between gap-3 border-b border-border p-4">
          <div class="min-w-0">
            <h2 id="detail-title" class="text-sm font-semibold">Detalle del movimiento</h2>
            <p class="font-numeric break-value mt-0.5 text-xs text-muted-foreground">
              {{ movement().id }}
            </p>
          </div>

          <div class="flex shrink-0 items-center gap-2">
            <app-copy-button [value]="movement().id" label="ID" />
            <button
              type="button"
              (click)="closed.emit()"
              class="grid size-8 place-items-center rounded-md text-muted-foreground
                     transition-colors hover:bg-accent focus-visible:outline-none
                     focus-visible:ring-2 focus-visible:ring-ring"
              aria-label="Cerrar"
            >
              <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2" stroke-linecap="round" aria-hidden="true">
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </button>
          </div>
        </header>

        <div class="border-b border-border px-4">
          <div class="flex gap-1" role="tablist">
            @for (tab of tabs; track tab.id) {
              <button
                type="button"
                role="tab"
                [attr.aria-selected]="activeTab() === tab.id"
                (click)="activeTab.set(tab.id)"
                class="border-b-2 px-3 py-2.5 text-sm font-medium transition-colors"
                [class]="
                  activeTab() === tab.id
                    ? 'border-primary text-foreground'
                    : 'border-transparent text-muted-foreground hover:text-foreground'
                "
              >
                {{ tab.label }}
              </button>
            }
          </div>
        </div>

        <div class="flex-1 overflow-y-auto p-4">
          @switch (activeTab()) {
            @case ('summary') {
              <div class="space-y-5">
                <div class="rounded-lg border border-border p-4">
                  <div class="flex items-baseline justify-between gap-3">
                    <span class="text-xs uppercase tracking-wide text-muted-foreground">
                      Importe
                    </span>
                    <span class="text-lg">
                      <app-money-amount
                        [amount]="movement().transaction.amount"
                        [currency]="movement().transaction.currency"
                        [flow]="movement().moneyFlow"
                      />
                    </span>
                  </div>
                  <div class="mt-2 flex items-baseline justify-between gap-3 border-t border-border pt-2">
                    <span class="text-xs uppercase tracking-wide text-muted-foreground">
                      Saldo posterior
                    </span>
                    <app-money-amount
                      [amount]="movement().postedBalance.amount"
                      [currency]="movement().postedBalance.currency"
                    />
                  </div>
                </div>

                <dl class="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2">
                  @for (row of rows(); track row.label) {
                    <div [class.sm:col-span-2]="row.wide">
                      <dt class="text-[11px] uppercase tracking-wide text-muted-foreground">
                        {{ row.label }}
                      </dt>
                      <dd
                        class="mt-0.5 text-sm"
                        [class.font-numeric]="row.numeric"
                        [class.break-value]="row.wide"
                      >
                        {{ row.value || '—' }}
                      </dd>
                    </div>
                  }

                  <div>
                    <dt class="text-[11px] uppercase tracking-wide text-muted-foreground">Estado</dt>
                    <dd class="mt-1">
                      <app-status-badge
                        [status]="movement().status"
                        [isKnown]="movement().isKnownStatus"
                      />
                    </dd>
                  </div>
                </dl>
              </div>
            }

            @case ('endpoint') {
              <div class="space-y-3">
                <div class="rounded-lg border border-border bg-muted/40 p-3">
                  <p class="text-[11px] uppercase tracking-wide text-muted-foreground">
                    Verificación contra el endpoint de detalle
                  </p>
                  <p class="font-numeric break-value mt-1 text-xs">
                    <span class="font-semibold text-primary">GET</span>
                    /accounts/{{ movement().contract.number }}/transactions/{{ movement().id }}
                  </p>
                </div>

                @if (detail.isLoading()) {
                  <p class="text-sm text-muted-foreground">Consultando el endpoint de detalle…</p>
                } @else if (detail.error()) {
                  <p class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
                     role="alert">
                    {{ detailErrorMessage() }}
                  </p>
                } @else if (detail.hasValue()) {
                  <p
                    class="rounded-md px-3 py-2 text-sm"
                    [class]="
                      matchesListing()
                        ? 'bg-pass-muted text-pass'
                        : 'bg-drift-muted text-drift'
                    "
                  >
                    {{
                      matchesListing()
                        ? 'El detalle coincide con lo que devolvió el listado.'
                        : 'El detalle difiere del listado — vale la pena mirarlo.'
                    }}
                  </p>
                  <app-json-viewer [value]="detail.value()" maxHeight="26rem" />
                }
              </div>
            }

            @case ('json') {
              <app-json-viewer [value]="movement()" maxHeight="34rem" />
            }
          }
        </div>
      </aside>
    </div>
  `,
})
export class MovementDetailPanel {
  private readonly api = inject(BbvaApi);

  readonly movement = input.required<Movement>();
  readonly closed = output<void>();

  protected readonly tabs = [
    { id: 'summary', label: 'Resumen' },
    { id: 'endpoint', label: 'Endpoint detalle' },
    { id: 'json', label: 'JSON' },
  ] as const;

  protected readonly activeTab = signal<'summary' | 'endpoint' | 'json'>('summary');

  protected readonly detail = rxResource({
    params: () => ({
      account: this.movement().contract.number,
      transaction: this.movement().id,
    }),
    stream: ({ params }) => this.api.getMovement(params.account, params.transaction),
  });

  protected readonly detailErrorMessage = computed(() => {
    const error = this.detail.error() as { message?: string } | undefined;
    return error?.message ?? 'No se pudo consultar el endpoint de detalle.';
  });

  protected readonly matchesListing = computed(() => {
    const fetched = this.detail.value();

    if (!fetched) {
      return false;
    }

    const listed = this.movement();

    return (
      fetched.id === listed.id &&
      fetched.transaction.amount === listed.transaction.amount &&
      fetched.moneyFlow === listed.moneyFlow &&
      fetched.status === listed.status
    );
  });

  protected readonly rows = computed(() => {
    const movement = this.movement();

    return [
      { label: 'Concepto', value: `${movement.concept.id} · ${movement.concept.name}` },
      { label: 'Flujo', value: movement.moneyFlow },
      { label: 'Financiable', value: movement.isFinanceable ? 'Sí' : 'No' },
      {
        label: 'Fecha de operación',
        value: format(movement.operation.date),
        numeric: true,
      },
      { label: 'Fecha de valuación', value: format(movement.valuationDate), numeric: true },
      { label: 'Contrato', value: movement.contract.number, numeric: true },
      { label: 'ID de contrato', value: movement.contract.id, numeric: true },
      { label: 'Nº de transacción', value: movement.transaction.id, numeric: true },
      { label: 'ID de operación', value: movement.operation.id, numeric: true, wide: true },
      { label: 'Descripción', value: movement.comments ?? '', wide: true },
    ] as ReadonlyArray<{ label: string; value: string; numeric?: boolean; wide?: boolean }>;
  });
}

function format(value: string | null): string {
  if (!value) {
    return '';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString('es-PE');
}
