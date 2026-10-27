import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';

import { BbvaApi } from '../../core/api/bbva-api';

/**
 * Live OAuth connectivity indicator in the header.
 *
 * Whether the service currently holds a valid BBVA token is the first thing that matters
 * when a call fails, and it is the difference between "my filters are wrong" and "the
 * credentials expired". Putting it in the chrome means nobody has to go looking.
 */
@Component({
  selector: 'app-health-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="flex items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-xs
             font-medium transition-colors hover:bg-accent focus-visible:outline-none
             focus-visible:ring-2 focus-visible:ring-ring"
      [attr.title]="tooltip()"
      [attr.aria-label]="tooltip()"
      (click)="refresh()"
    >
      <span
        class="size-2 shrink-0 rounded-full"
        [class.bg-pass]="state() === 'healthy'"
        [class.bg-fail]="state() === 'unhealthy'"
        [class.bg-muted-foreground]="state() === 'loading'"
        [class.animate-pulse]="state() === 'loading'"
        aria-hidden="true"
      ></span>
      <span class="hidden text-muted-foreground md:inline">{{ label() }}</span>
    </button>
  `,
})
export class HealthIndicator {
  private readonly api = inject(BbvaApi);
  private readonly reloadToken = signal(0);

  private readonly health = rxResource({
    params: () => this.reloadToken(),
    stream: () => this.api.getHealth(),
  });

  protected readonly state = computed<'loading' | 'healthy' | 'unhealthy'>(() => {
    if (this.health.isLoading()) {
      return 'loading';
    }

    return this.health.value()?.status === 'healthy' ? 'healthy' : 'unhealthy';
  });

  protected readonly label = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Conectando…';
      case 'healthy':
        return 'Sandbox OK';
      default:
        return 'Sin conexión';
    }
  });

  protected readonly tooltip = computed(() => {
    const value = this.health.value();

    if (this.state() === 'loading') {
      return 'Verificando conexión con BBVA…';
    }

    if (this.state() === 'unhealthy') {
      return `No se pudo obtener token de BBVA. ${value?.detail ?? ''}`.trim();
    }

    const seconds = value?.tokenValidForSeconds ?? 0;
    const minutes = Math.floor(seconds / 60);

    return `Token OAuth válido por ${minutes} min · ${value?.baseUrl}. Click para revalidar.`;
  });

  protected refresh(): void {
    this.reloadToken.update((value) => value + 1);
  }
}
