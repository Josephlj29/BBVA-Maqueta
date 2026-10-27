import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import type { MoneyFlow } from '../../core/models/movement.models';

/**
 * A monetary amount coloured by direction.
 *
 * Colour alone never carries the meaning: an explicit `+`/`−` sign is always rendered, so
 * the direction survives greyscale printing, colour-blindness and a screenshot pasted
 * into a ticket.
 */
@Component({
  selector: 'app-money-amount',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="font-numeric font-medium tabular-nums"
      [class.text-income]="flow() === 'INCOME'"
      [class.text-expense]="flow() === 'EXPENSE'"
      [class.text-foreground]="!flow()"
      [attr.title]="title()"
    >
      {{ display() }}
    </span>
  `,
})
export class MoneyAmount {
  readonly amount = input.required<number>();
  readonly currency = input<string>('');
  /** Omit to render the amount without directional colouring or a sign. */
  readonly flow = input<MoneyFlow | null>(null);
  readonly showCurrency = input(true);

  protected readonly display = computed(() => {
    const value = this.amount();
    const magnitude = Math.abs(value).toLocaleString('es-PE', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });

    const code = this.showCurrency() && this.currency() ? ` ${this.currency()}` : '';

    if (!this.flow()) {
      return `${magnitude}${code}`;
    }

    // U+2212 minus, not a hyphen: it aligns with digits at the same width.
    const sign = this.flow() === 'EXPENSE' ? '−' : '+';

    return `${sign}${magnitude}${code}`;
  });

  protected readonly title = computed(() =>
    this.flow() === 'EXPENSE' ? 'Egreso (EXPENSE)' : this.flow() === 'INCOME' ? 'Ingreso (INCOME)' : null,
  );
}
