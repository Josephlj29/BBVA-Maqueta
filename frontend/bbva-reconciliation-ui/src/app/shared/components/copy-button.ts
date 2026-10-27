import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';

/**
 * Copies a value to the clipboard and confirms it inline.
 *
 * Signatures, digests and pagination cursors are hundreds of characters long and get
 * pasted into tickets and diff tools constantly; selecting them by hand is error prone
 * precisely because a truncated paste still looks plausible.
 */
@Component({
  selector: 'app-copy-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="inline-flex items-center gap-1.5 rounded-md border border-border bg-background
             px-2 py-1 text-xs font-medium text-muted-foreground transition-colors
             hover:bg-accent hover:text-accent-foreground focus-visible:outline-none
             focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1
             disabled:opacity-50"
      [attr.aria-label]="copied() ? 'Copiado' : 'Copiar ' + label()"
      (click)="copy()"
    >
      @if (copied()) {
        <svg class="size-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M20 6 9 17l-5-5" />
        </svg>
        Copiado
      } @else {
        <svg class="size-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <rect width="14" height="14" x="8" y="8" rx="2" ry="2" />
          <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
        </svg>
        {{ label() }}
      }
    </button>

    <!-- Announced to assistive tech without stealing focus. -->
    <span class="sr-only" role="status" aria-live="polite">
      {{ copied() ? 'Copiado al portapapeles' : '' }}
    </span>
  `,
})
export class CopyButton {
  readonly value = input.required<string>();
  readonly label = input('Copiar');

  protected readonly copied = signal(false);

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.value());
    } catch {
      // Clipboard access is denied outside a secure context; fall back to a selection.
      const area = document.createElement('textarea');
      area.value = this.value();
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      document.execCommand('copy');
      document.body.removeChild(area);
    }

    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1600);
  }
}
