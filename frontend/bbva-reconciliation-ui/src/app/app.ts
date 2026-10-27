import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { Theme } from './core/theme/theme';
import { HealthIndicator } from './features/health/health-indicator';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, HealthIndicator],
  template: `
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50
             focus:rounded-md focus:bg-primary focus:px-4 focus:py-2
             focus:text-primary-foreground"
    >
      Saltar al contenido
    </a>

    <div class="flex min-h-dvh flex-col bg-background">
      <header class="sticky top-0 z-40 border-b border-border bg-background/85 backdrop-blur">
        <div class="mx-auto flex h-14 w-full max-w-[1600px] items-center gap-3 px-4 sm:px-6">
          <a routerLink="/movimientos" class="flex shrink-0 items-center gap-2.5">
            <span
              class="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground"
              aria-hidden="true"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25"
                   stroke-linecap="round" stroke-linejoin="round" class="size-4">
                <path d="M3 17h4l3-9 4 14 3-9h4" />
              </svg>
            </span>
            <span class="hidden leading-tight sm:block">
              <span class="block text-sm font-semibold">Business Reconciliation</span>
              <span class="block text-[11px] text-muted-foreground">BBVA Perú · v1.0</span>
            </span>
          </a>

          <nav class="ml-2 flex items-center gap-1 overflow-x-auto" aria-label="Secciones">
            @for (item of navigation; track item.path) {
              <a
                [routerLink]="item.path"
                routerLinkActive="bg-secondary text-secondary-foreground"
                class="rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap
                       text-muted-foreground transition-colors hover:bg-accent
                       hover:text-accent-foreground"
              >
                {{ item.label }}
              </a>
            }
          </nav>

          <div class="ml-auto flex items-center gap-2">
            <app-health-indicator />

            <button
              type="button"
              class="grid size-9 place-items-center rounded-md border border-border
                     text-muted-foreground transition-colors hover:bg-accent
                     hover:text-accent-foreground focus-visible:outline-none
                     focus-visible:ring-2 focus-visible:ring-ring"
              [attr.aria-label]="theme.isDark() ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'"
              (click)="theme.toggle()"
            >
              @if (theme.isDark()) {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                     stroke-linecap="round" stroke-linejoin="round" class="size-4"
                     aria-hidden="true">
                  <circle cx="12" cy="12" r="4" />
                  <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2
                           M6.3 17.7l-1.4 1.4M19.1 4.9l-1.4 1.4" />
                </svg>
              } @else {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                     stroke-linecap="round" stroke-linejoin="round" class="size-4"
                     aria-hidden="true">
                  <path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" />
                </svg>
              }
            </button>
          </div>
        </div>
      </header>

      <main id="main" class="mx-auto w-full max-w-[1600px] flex-1 px-4 py-6 sm:px-6">
        <router-outlet />
      </main>

      <footer class="border-t border-border py-4">
        <p class="mx-auto max-w-[1600px] px-4 text-xs text-muted-foreground sm:px-6">
          Entorno <strong class="font-medium text-foreground">sandbox</strong>. La clave privada y
          el client secret viven solo en el backend — este navegador nunca los ve.
        </p>
      </footer>
    </div>
  `,
})
export class App {
  protected readonly theme = inject(Theme);

  protected readonly navigation = [
    { path: '/movimientos', label: 'Movimientos' },
    { path: '/playground', label: 'Playground' },
    { path: '/sandbox', label: 'Sandbox' },
  ] as const;
}
