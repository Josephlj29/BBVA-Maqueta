import { KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { BbvaApi } from '../../core/api/bbva-api';
import type {
  SandboxCaseResult,
  SandboxOutcome,
  SandboxTestCase,
} from '../../core/models/diagnostics.models';
import { CopyButton } from '../../shared/components/copy-button';
import { JsonViewer } from '../../shared/components/json-viewer';

type Filter = 'all' | 'search' | 'detail' | 'failing';

@Component({
  selector: 'app-sandbox-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyValuePipe, JsonViewer, CopyButton],
  templateUrl: './sandbox-page.html',
})
export class SandboxPage {
  private readonly api = inject(BbvaApi);

  protected readonly plan = rxResource({
    params: () => ({}),
    stream: () => this.api.getSandboxPlan(),
  });

  /** Results keyed by case id. A Map keeps per-case updates from re-rendering the list. */
  protected readonly results = signal<ReadonlyMap<string, SandboxCaseResult>>(new Map());
  protected readonly running = signal<ReadonlySet<string>>(new Set());
  protected readonly isRunningAll = signal(false);
  protected readonly progress = signal({ done: 0, total: 0 });
  protected readonly expanded = signal<string | null>(null);
  protected readonly filter = signal<Filter>('all');

  /**
   * Whether to compare amounts, balances and dates too.
   *
   * Off by default: the workbook captured those values at a point in time and BBVA's
   * sandbox keeps moving them, so comparing them paints every scenario red and buries the
   * differences that actually matter — status codes, error messages and response shape.
   */
  protected readonly compareVolatile = signal(false);

  protected readonly cases = computed(() => this.plan.value()?.cases ?? []);

  protected readonly visibleCases = computed(() => {
    const all = this.cases();

    switch (this.filter()) {
      case 'search':
        return all.filter((item) => item.group === 'search');
      case 'detail':
        return all.filter((item) => item.group === 'detail');
      case 'failing':
        return all.filter((item) => {
          const outcome = this.results().get(item.id)?.outcome;
          return outcome === 'Failed' || outcome === 'Errored' || outcome === 'Drifted';
        });
      default:
        return all;
    }
  });

  protected readonly summary = computed(() => {
    const tally = { Passed: 0, Drifted: 0, Failed: 0, Errored: 0 };

    for (const result of this.results().values()) {
      tally[result.outcome] += 1;
    }

    const executed = tally.Passed + tally.Drifted + tally.Failed + tally.Errored;

    return { ...tally, executed, total: this.cases().length };
  });

  protected readonly isCertified = computed(() => {
    const { executed, total, Failed, Errored } = this.summary();
    return total > 0 && executed === total && Failed === 0 && Errored === 0;
  });

  protected resultFor(caseId: string): SandboxCaseResult | undefined {
    return this.results().get(caseId);
  }

  protected isRunning(caseId: string): boolean {
    return this.running().has(caseId);
  }

  protected toggle(caseId: string): void {
    this.expanded.update((current) => (current === caseId ? null : caseId));
  }

  protected async runOne(testCase: SandboxTestCase): Promise<void> {
    if (this.isRunning(testCase.id)) {
      return;
    }

    this.running.update((current) => new Set(current).add(testCase.id));

    try {
      const result = await firstValueFrom(
        this.api.runSandboxCase(testCase.id, this.compareVolatile()),
      );

      this.results.update((current) => new Map(current).set(testCase.id, result));
    } catch {
      this.results.update((current) =>
        new Map(current).set(testCase.id, {
          caseId: testCase.id,
          description: testCase.description,
          outcome: 'Errored',
          expectedStatusCode: testCase.expectedStatusCode,
          actualStatusCode: 0,
          expectedResponse: testCase.expectedResponse,
          actualResponse: null,
          differences: [],
          elapsedMilliseconds: 0,
          statusMatches: false,
          bodyMatches: false,
        }),
      );
    } finally {
      this.running.update((current) => {
        const next = new Set(current);
        next.delete(testCase.id);
        return next;
      });
    }
  }

  /**
   * Runs the whole plan sequentially.
   *
   * Deliberately not parallel: these are real calls against BBVA's sandbox, and hammering
   * a bank's test environment with 41 concurrent signed requests is how an integration
   * gets rate limited on the day it most needs to work.
   */
  protected async runAll(): Promise<void> {
    if (this.isRunningAll()) {
      return;
    }

    const all = this.cases();

    this.isRunningAll.set(true);
    this.results.set(new Map());
    this.progress.set({ done: 0, total: all.length });

    for (const [index, testCase] of all.entries()) {
      await this.runOne(testCase);
      this.progress.set({ done: index + 1, total: all.length });
    }

    this.isRunningAll.set(false);
  }

  protected outcomeTone(outcome: SandboxOutcome | undefined): string {
    switch (outcome) {
      case 'Passed':
        return 'bg-pass-muted text-pass ring-pass/30';
      case 'Drifted':
        return 'bg-drift-muted text-drift ring-drift/30';
      case 'Failed':
      case 'Errored':
        return 'bg-fail-muted text-fail ring-fail/30';
      default:
        return 'bg-muted text-muted-foreground ring-border';
    }
  }

  protected outcomeLabel(outcome: SandboxOutcome | undefined): string {
    switch (outcome) {
      case 'Passed':
        return 'OK';
      case 'Drifted':
        return 'Difiere';
      case 'Failed':
        return 'Falla';
      case 'Errored':
        return 'Error';
      default:
        return 'Sin correr';
    }
  }

  /** Certification evidence as Markdown, ready to paste into a ticket or an email. */
  protected buildReport(): string {
    const plan = this.plan.value();
    const summary = this.summary();
    const stamp = new Date().toISOString();

    const lines = [
      '# Plan de pruebas Sandbox — BBVA Business Reconciliation',
      '',
      `- **Fuente**: ${plan?.source ?? '—'}`,
      `- **Entorno**: ${plan?.sandboxBaseUrl ?? '—'}`,
      `- **Ejecutado**: ${stamp}`,
      `- **Casos**: ${summary.executed}/${summary.total}`,
      `- **Resultado**: ${summary.Passed} OK · ${summary.Drifted} difieren · ` +
        `${summary.Failed} fallan · ${summary.Errored} con error`,
      '',
      `> Los importes, saldos y fechas ${this.compareVolatile() ? 'sí' : 'no'} se comparan.`,
      '',
      '| Caso | Descripción | Esperado | Obtenido | Resultado | Difs | ms |',
      '| --- | --- | ---: | ---: | --- | ---: | ---: |',
    ];

    for (const testCase of this.cases()) {
      const result = this.results().get(testCase.id);

      lines.push(
        `| ${testCase.id} | ${testCase.description.replace(/\|/g, '\\|')} | ` +
          `${testCase.expectedStatusCode} | ${result?.actualStatusCode ?? '—'} | ` +
          `${this.outcomeLabel(result?.outcome)} | ${result?.differences.length ?? '—'} | ` +
          `${result?.elapsedMilliseconds ?? '—'} |`,
      );
    }

    return lines.join('\n');
  }

  protected downloadReport(): void {
    const blob = new Blob([this.buildReport()], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = `sandbox-certification-${new Date().toISOString().slice(0, 10)}.md`;
    link.click();

    URL.revokeObjectURL(url);
  }
}
