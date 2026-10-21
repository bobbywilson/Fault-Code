import { TestBed } from '@angular/core/testing';
import { Location } from '@angular/common';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { of, throwError } from 'rxjs';
const fake = vi.hoisted(() => ({
  active: null as any,
  accounts: [] as any[],
  redirect: null as any,
  silentError: false,
  settings: null as any,
  login: vi.fn(),
  logout: vi.fn(),
}));
vi.mock('@azure/msal-browser', () => ({
  BrowserCacheLocation: { SessionStorage: 'sessionStorage' },
  InteractionRequiredAuthError: class extends Error {},
  PublicClientApplication: class {
    constructor(c: any) {
      fake.settings = c;
    }
    getAllAccounts() {
      return fake.accounts;
    }
    getActiveAccount() {
      return fake.active;
    }
    setActiveAccount(a: any) {
      fake.active = a;
    }
  },
}));
vi.mock('@azure/msal-angular', async () => {
  const { of, throwError } = await import('rxjs');
  return {
    MSAL_INSTANCE: Symbol('test-msal'),
    MsalBroadcastService: class {},
    MsalService: class {
      instance: any;
      constructor(i: any) {
        this.instance = i;
      }
      initialize() {
        return of(undefined);
      }
      handleRedirectObservable() {
        return of(fake.redirect);
      }
      loginRedirect(r: any) {
        fake.login(r);
        return of(undefined);
      }
      logoutRedirect() {
        fake.logout();
        return of(undefined);
      }
      acquireTokenSilent() {
        return fake.silentError
          ? throwError(() => new Error('provider detail must not leak'))
          : of({ accessToken: 'synthetic-unit-token' });
      }
    },
  };
});
import { Auth } from './auth';
const tenant = '11111111-1111-4111-8111-111111111111';
const config = {
  tenantId: tenant,
  clientId: '22222222-2222-4222-8222-222222222222',
  apiClientId: '33333333-3333-4333-8333-333333333333',
  scopes: ['api://33333333-3333-4333-8333-333333333333/authorization.read'],
};
afterEach(() => {
  TestBed.resetTestingModule();
  vi.unstubAllGlobals();
  fake.active = null;
  fake.accounts = [];
  fake.redirect = null;
  fake.silentError = false;
  fake.login.mockClear();
  fake.logout.mockClear();
});
function auth(c: any = config) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(new Response(JSON.stringify(c), { status: 200 })),
  );
  TestBed.configureTestingModule({ providers: [{ provide: Location, useValue: {} }] });
  return TestBed.inject(Auth);
}
describe('MSAL wrapper lifecycle with isolated library doubles', () => {
  it('keeps missing configuration fail-closed without creating a session', async () => {
    const a = auth({});
    await a.initialize();
    expect(a.state()).toBe('unconfigured');
    await expect(a.token()).rejects.toThrow('session');
  });
  it('accepts only the dedicated tenant and uses tab-scoped redirect cache and fixed callback', async () => {
    fake.redirect = { account: { tenantId: tenant } };
    const a = auth();
    await a.initialize();
    expect(a.state()).toBe('signed-in');
    expect(fake.settings.auth.redirectUri).toBe(location.origin + '/auth/callback');
    expect(fake.settings.cache.cacheLocation).toBe('sessionStorage');
    expect(fake.settings.system.loggerOptions.piiLoggingEnabled).toBe(false);
    expect(await a.token()).toBe('synthetic-unit-token');
    await a.logout();
    expect(a.state()).toBe('signed-out');
    expect(fake.logout).toHaveBeenCalledOnce();
  });
  it('does not accept another tenant callback or an ambiguous account selection', async () => {
    fake.redirect = { account: { tenantId: 'other' } };
    fake.accounts = [{ tenantId: 'other' }];
    const a = auth();
    await a.initialize();
    expect(a.state()).toBe('signed-out');
    await a.login();
    expect(fake.login).toHaveBeenCalledWith({ scopes: config.scopes });
  });
  it('clears access on failed silent renewal and hides provider error details', async () => {
    fake.redirect = { account: { tenantId: tenant } };
    const a = auth();
    await a.initialize();
    fake.silentError = true;
    await expect(a.token()).rejects.toThrow('session');
    expect(a.state()).toBe('error');
  });
});
