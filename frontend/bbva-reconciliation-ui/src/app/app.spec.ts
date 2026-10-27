import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';

import { App } from './app';
import { Theme } from './core/theme/theme';

describe('App shell', () => {
  let http: HttpTestingController;

  /**
   * The shell renders a live health indicator, so it always has one request in flight.
   * That request has to be answered before the fixture can settle — awaiting stability
   * without draining it just waits for something that will never arrive.
   */
  async function render() {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    for (const request of http.match(() => true)) {
      request.flush({
        status: 'healthy',
        baseUrl: 'https://sbx.pe.bbvaapimarket.com',
        tokenAcquired: true,
        tokenExpiresAt: null,
        tokenValidForSeconds: 900,
        detail: null,
      });
    }

    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  afterEach(() => http?.verify());

  it('creates without a browser matchMedia implementation', async () => {
    // jsdom has no matchMedia. The theme service is injected by the shell, so if it
    // threw on a missing matchMedia the whole application would fail to bootstrap.
    const fixture = await render();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('exposes the three feature sections', async () => {
    const fixture = await render();
    const links = fixture.nativeElement.querySelectorAll('nav a');

    expect(Array.from(links, (link) => (link as HTMLElement).textContent?.trim())).toEqual([
      'Movimientos',
      'Playground',
      'Sandbox',
    ]);
  });

  it('states that secrets stay on the server', async () => {
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('nunca los ve');
  });
});

describe('Theme', () => {
  function create(): Theme {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    return TestBed.inject(Theme);
  }

  it('defaults to following the system preference', () => {
    expect(create().mode()).toBe('system');
  });

  it('toggles between light and dark', () => {
    const theme = create();

    theme.set('light');
    expect(theme.isDark()).toBe(false);

    theme.toggle();
    expect(theme.isDark()).toBe(true);

    theme.toggle();
    expect(theme.isDark()).toBe(false);
  });
});
