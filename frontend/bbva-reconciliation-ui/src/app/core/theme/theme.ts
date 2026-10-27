import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'bbva-reconciliation-theme';

/**
 * Light/dark theme, persisted per browser.
 *
 * `system` is the default and follows the OS preference live, so an operator who switches
 * their machine to dark at the end of the day does not have to touch this app.
 */
@Injectable({ providedIn: 'root' })
export class Theme {
  private readonly document = inject(DOCUMENT);

  /**
   * `matchMedia` is absent in jsdom and in any non-browser host. Treating it as optional
   * keeps the service usable there instead of throwing during construction — which, since
   * this is injected by the app shell, would take down the whole application.
   */
  private readonly media = this.queryColorScheme();

  private readonly systemPrefersDark = signal(this.media?.matches ?? false);

  readonly mode = signal<ThemeMode>(this.readStoredMode());

  readonly isDark = computed(() =>
    this.mode() === 'system' ? this.systemPrefersDark() : this.mode() === 'dark',
  );

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));

    effect(() => {
      // Spartan's dark tokens are scoped to `:root.dark`, so the class goes on <html>.
      this.document.documentElement.classList.toggle('dark', this.isDark());
    });

    effect(() => {
      try {
        this.document.defaultView?.localStorage.setItem(STORAGE_KEY, this.mode());
      } catch {
        // Private browsing or blocked site data — the theme just will not persist.
      }
    });
  }

  toggle(): void {
    this.mode.set(this.isDark() ? 'light' : 'dark');
  }

  set(mode: ThemeMode): void {
    this.mode.set(mode);
  }

  private queryColorScheme(): MediaQueryList | undefined {
    const view = this.document.defaultView;

    return typeof view?.matchMedia === 'function'
      ? view.matchMedia('(prefers-color-scheme: dark)')
      : undefined;
  }

  private readStoredMode(): ThemeMode {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(STORAGE_KEY);
      return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
    } catch {
      return 'system';
    }
  }
}
