import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * A movement status chip.
 *
 * An unrecognised status is rendered rather than hidden or normalised away. BBVA can
 * introduce a state before this client knows about it, and quietly swallowing it would
 * make a real change in bank behaviour invisible — so it shows with a warning tint and a
 * tooltip that says so.
 */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-medium
             whitespace-nowrap ring-1 ring-inset"
      [class]="tone()"
      [attr.title]="tooltip()"
    >
      @if (!isKnown()) {
        <span aria-hidden="true">?</span>
      }
      {{ status() ?? '—' }}
    </span>
  `,
})
export class StatusBadge {
  readonly status = input<string | null>(null);
  readonly isKnown = input(true);

  protected readonly tone = computed(() => {
    if (!this.isKnown()) {
      return 'bg-drift-muted text-drift ring-drift/30';
    }

    switch (this.status()) {
      case 'SETTLED':
        return 'bg-income-muted text-income ring-income/30';

      case 'PENDING':
      case 'AUTHORIZATION_PENDING':
      case 'SENT':
        return 'bg-drift-muted text-drift ring-drift/30';

      case 'REJECTED':
      case 'REFUSED':
      case 'RETURNED':
      case 'CANCELED':
      case 'CHARGEBACKED':
        return 'bg-expense-muted text-expense ring-expense/30';

      default:
        return 'bg-muted text-muted-foreground ring-border';
    }
  });

  protected readonly tooltip = computed(() =>
    this.isKnown() ? null : 'BBVA devolvió un estado que este cliente no reconoce',
  );
}
