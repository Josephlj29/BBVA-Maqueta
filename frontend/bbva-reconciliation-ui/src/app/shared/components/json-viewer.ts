import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { CopyButton } from './copy-button';

/**
 * Pretty-prints a JSON value in a scrollable, copyable block.
 *
 * The container scrolls on its own axis rather than letting the page scroll sideways —
 * a wide response body should not push the whole layout off screen.
 */
@Component({
  selector: 'app-json-viewer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CopyButton],
  template: `
    <div class="relative">
      @if (showCopy()) {
        <div class="absolute right-2 top-2 z-10">
          <app-copy-button [value]="formatted()" label="JSON" />
        </div>
      }

      <pre
        class="max-h-[var(--json-max-height)] overflow-auto rounded-lg border border-border
               bg-muted/40 p-3 text-xs leading-relaxed font-mono"
        [style.--json-max-height]="maxHeight()"
      ><code>{{ formatted() }}</code></pre>
    </div>
  `,
})
export class JsonViewer {
  readonly value = input.required<unknown>();
  readonly maxHeight = input('24rem');
  readonly showCopy = input(true);

  protected readonly formatted = computed(() => {
    const value = this.value();

    if (value === null || value === undefined) {
      return '—';
    }

    if (typeof value === 'string') {
      // Response bodies often arrive as a JSON string; render them structured if possible.
      try {
        return JSON.stringify(JSON.parse(value), null, 2);
      } catch {
        return value;
      }
    }

    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  });
}
