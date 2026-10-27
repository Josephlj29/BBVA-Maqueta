import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { BbvaApi, BbvaApiError } from '../../core/api/bbva-api';
import { SANDBOX_SAMPLE_MOVEMENT } from '../../core/config/sandbox-contracts';
import type { PlaygroundExecution } from '../../core/models/playground.models';
import { CopyButton } from '../../shared/components/copy-button';
import { JsonViewer } from '../../shared/components/json-viewer';

interface EndpointPreset {
  readonly id: string;
  readonly label: string;
  readonly method: 'GET' | 'POST';
  readonly path: string;
  readonly body: string;
  readonly hint: string;
}

const API_BASE = '/pe/business-reconciliation/v1';

@Component({
  selector: 'app-playground-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, JsonViewer, CopyButton],
  templateUrl: './playground-page.html',
})
export class PlaygroundPage {
  private readonly api = inject(BbvaApi);

  protected readonly presets: readonly EndpointPreset[] = [
    {
      id: 'search',
      label: 'Listar movimientos',
      method: 'POST',
      path: `${API_BASE}/transactions/search`,
      body: JSON.stringify({ contracts: ['001101300210484377'], moneyFlow: 'INCOME' }, null, 2),
      hint: 'Un solo contrato de 18 dígitos.',
    },
    {
      id: 'detail',
      label: 'Detalle de movimiento',
      method: 'GET',
      path:
        `${API_BASE}/accounts/${SANDBOX_SAMPLE_MOVEMENT.accountId}` +
        `/transactions/${SANDBOX_SAMPLE_MOVEMENT.transactionId}`,
      body: '',
      hint: 'Sin body. El digest se calcula sobre la cadena vacía.',
    },
    {
      id: 'forbidden',
      label: 'Cuenta no asociada (403)',
      method: 'POST',
      path: `${API_BASE}/transactions/search`,
      body: JSON.stringify({ contracts: ['001101300210522600'] }, null, 2),
      hint: 'Contrato válido pero no habilitado — BBVA responde 403.',
    },
    {
      id: 'invalid',
      label: 'moneyFlow inválido (400)',
      method: 'POST',
      path: `${API_BASE}/transactions/search`,
      body: JSON.stringify({ contracts: ['001101300210515493'], moneyFlow: 'ENTRY' }, null, 2),
      hint: 'Se envía tal cual para ver el mensaje exacto del banco.',
    },
  ];

  protected readonly method = signal<'GET' | 'POST'>('POST');
  protected readonly path = signal(this.presets[0].path);
  protected readonly body = signal(this.presets[0].body);
  protected readonly activePreset = signal<string>('search');

  protected readonly result = signal<PlaygroundExecution | null>(null);
  protected readonly error = signal<BbvaApiError | null>(null);
  protected readonly isSending = signal(false);

  /** Local JSON check, so a typo is caught before it costs a round trip. */
  protected readonly bodyError = computed(() => {
    const raw = this.body().trim();

    if (!raw || this.method() === 'GET') {
      return null;
    }

    try {
      JSON.parse(raw);
      return null;
    } catch (failure) {
      return failure instanceof Error ? failure.message : 'El body no es JSON válido.';
    }
  });

  protected readonly canSend = computed(
    () => !this.isSending() && !this.bodyError() && this.path().trim().startsWith(API_BASE),
  );

  protected readonly statusTone = computed(() => {
    const status = this.result()?.statusCode ?? 0;

    if (status === 0) {
      return 'bg-fail-muted text-fail';
    }

    return status < 400 ? 'bg-pass-muted text-pass' : 'bg-fail-muted text-fail';
  });

  /** The signed string rendered line by line, so the LF boundaries are visible. */
  protected readonly signedLines = computed(
    () => this.result()?.signature.stringToSign.split('\n') ?? [],
  );

  protected applyPreset(preset: EndpointPreset): void {
    this.activePreset.set(preset.id);
    this.method.set(preset.method);
    this.path.set(preset.path);
    this.body.set(preset.body);
    this.result.set(null);
    this.error.set(null);
  }

  protected send(): void {
    if (!this.canSend()) {
      return;
    }

    this.isSending.set(true);
    this.error.set(null);

    this.api
      .executePlayground({
        method: this.method(),
        path: this.path().trim(),
        body: this.method() === 'GET' ? null : this.body().trim() || null,
      })
      .subscribe({
        next: (execution) => {
          this.result.set(execution);
          this.isSending.set(false);
        },
        error: (failure: BbvaApiError) => {
          this.error.set(failure);
          this.result.set(null);
          this.isSending.set(false);
        },
      });
  }

  /** A ready-to-paste curl, with the exact headers that were signed. */
  protected asCurl(): string {
    const execution = this.result();

    if (!execution) {
      return '';
    }

    const { signature } = execution;
    const url = `https://${signature.host}${execution.path}`;

    const lines = [
      `curl -X ${execution.method} '${url}' \\`,
      `  -H 'Authorization: Bearer <TOKEN>' \\`,
      `  -H 'Content-Type: application/json' \\`,
      `  -H 'Digest: ${signature.digest}' \\`,
      `  -H 'Date: ${signature.date}' \\`,
      `  -H 'Host: ${signature.host}' \\`,
      `  -H 'Signature: ${signature.signature}'`,
    ];

    if (execution.method === 'POST' && signature.body) {
      lines[lines.length - 1] += ' \\';
      lines.push(`  -d '${signature.body}'`);
    }

    return lines.join('\n');
  }
}
