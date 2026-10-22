import { Injectable, signal, inject, Injector, isDevMode } from '@angular/core';
import { Location } from '@angular/common';
import { MSAL_INSTANCE, MsalBroadcastService, MsalService } from '@azure/msal-angular';
import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
} from '@azure/msal-browser';
import { firstValueFrom } from 'rxjs';
export interface BrowserConfig {
  tenantId: string;
  clientId: string;
  apiClientId: string;
  scopes: string[];
  profileImages?: Record<string, string>;
}
export function validConfig(c: BrowserConfig): boolean {
  const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  return (
    !!c &&
    [c.tenantId, c.clientId, c.apiClientId].every((x) => typeof x === 'string' && guid.test(x)) &&
    (c.profileImages === undefined ||
      (typeof c.profileImages === 'object' &&
        c.profileImages !== null &&
        Object.entries(c.profileImages).every(
          ([id, path]) =>
            guid.test(id) && typeof path === 'string' && /^\/profiles\/[a-z0-9-]+\.png$/.test(path),
        ))) &&
    Array.isArray(c.scopes) &&
    c.scopes.length > 0 &&
    c.scopes.length <= 30 &&
    c.scopes.every(
      (s) =>
        typeof s === 'string' &&
        s.startsWith(`api://${c.apiClientId}/`) &&
        /^[a-z.]+$/.test(s.slice(s.lastIndexOf('/') + 1)),
    )
  );
}
@Injectable({ providedIn: 'root' })
export class Auth {
  readonly state = signal<
    'loading' | 'unconfigured' | 'signed-out' | 'signed-in' | 'expired' | 'error'
  >('loading');
  private parent = inject(Injector);
  private location = inject(Location);
  readonly kind = signal<'entra' | 'demo'>('entra');
  readonly demoAvailable = signal(false);
  private csrfToken = '';
  private demoUser: {
    id: string;
    displayName: string;
    email: string;
    roles: string[];
    permissions: string[];
    tokenExpiresAt: string | null;
    profilePhoto?: string;
  } | null = null;
  readonly profileName = signal('');
  readonly profilePhoto = signal('');
  private refreshToken = true;
  private config?: BrowserConfig;
  private msal?: MsalService;
  async initialize(): Promise<void> {
    // Local Entra callback is registered for this exact origin. Do not start a
    // flow from an alternate ng-serve preview origin with incompatible MSAL storage.
    if (
      isDevMode() &&
      ['localhost', '127.0.0.1'].includes(location.hostname) &&
      location.origin !== 'http://localhost:4200'
    ) {
      location.replace(
        'http://localhost:4200' + location.pathname + location.search + location.hash,
      );
      return;
    }
    try {
      await this.loadDemoSession();
      if (this.demoUser) return;
      const response = await fetch('/browser-config.json', {
        cache: 'no-store',
        redirect: 'error',
      });
      const c = (await response.json()) as BrowserConfig;
      if (!response.ok || !validConfig(c)) {
        this.state.set(this.demoAvailable() ? 'signed-out' : 'unconfigured');
        return;
      }
      this.config = c;
      const instance = new PublicClientApplication({
        auth: {
          clientId: c.clientId,
          authority: `https://login.microsoftonline.com/${c.tenantId}`,
          redirectUri: location.origin + '/auth/callback',
          postLogoutRedirectUri: location.origin + '/',
        },
        cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
        system: { loggerOptions: { piiLoggingEnabled: false, loggerCallback: () => {} } },
      });
      this.msal = new MsalService(
        instance,
        this.location,
        Injector.create({
          providers: [{ provide: MSAL_INSTANCE, useValue: instance }, MsalBroadcastService],
          parent: this.parent,
        }),
      );
      await firstValueFrom(this.msal.initialize());
      const result = await firstValueFrom(
        this.msal.handleRedirectObservable({ navigateToLoginRequestUrl: false }),
      );
      if (instance.getActiveAccount()?.tenantId !== c.tenantId) instance.setActiveAccount(null);
      const accounts = instance.getAllAccounts().filter((a) => a.tenantId === c.tenantId);
      if (result?.account && result.account.tenantId === c.tenantId)
        instance.setActiveAccount(result.account);
      else if (accounts.length === 1) instance.setActiveAccount(accounts[0]);
      this.profileName.set(instance.getActiveAccount()?.name ?? 'Account');
      this.state.set(instance.getActiveAccount() ? 'signed-in' : 'signed-out');
      const account = instance.getActiveAccount();
      this.profilePhoto.set(account ? (c.profileImages?.[account.localAccountId] ?? '') : '');
    } catch {
      this.state.set('error');
    }
  }
  private async loadDemoSession(): Promise<void> {
    const r = await fetch('/session', {
      credentials: 'same-origin',
      cache: 'no-store',
      redirect: 'error',
    });
    if (!r.ok) return;
    const data = await r.json();
    this.demoAvailable.set(data.demoAvailable === true);
    this.csrfToken = data.csrfToken ?? '';
    if (data.user && data.demoAvailable) this.acceptDemo(data.user);
  }
  private acceptDemo(user: any): void {
    this.kind.set('demo');
    this.demoUser = user;
    this.profileName.set(user.displayName);
    this.profilePhoto.set(user.profilePhoto ?? '');
    this.state.set('signed-in');
  }
  async loginDemo(username: string, password: string): Promise<void> {
    const r = await fetch('/session/login', {
      method: 'POST',
      credentials: 'same-origin',
      cache: 'no-store',
      redirect: 'error',
      headers: { 'Content-Type': 'application/json', 'X-FaultCode-CSRF': this.csrfToken },
      body: JSON.stringify({ username, password }),
    });
    if (!r.ok)
      throw new Error(
        r.status === 429
          ? 'Too many attempts. Try again in a minute.'
          : 'Username or password is incorrect.',
      );
    await this.loadDemoSession();
    if (!this.demoUser) throw new Error('Sign-in could not be completed.');
  }
  async apiHeaders(): Promise<Record<string, string>> {
    return this.kind() === 'demo'
      ? { 'X-FaultCode-CSRF': this.csrfToken }
      : { Authorization: `Bearer ${await this.token()}` };
  }
  private clearPhoto(): void {
    this.profilePhoto.set('');
  }
  async login(): Promise<void> {
    if (!this.msal) await this.initialize();
    if (this.msal && this.config)
      await firstValueFrom(this.msal.loginRedirect({ scopes: this.config.scopes }));
  }
  async logout(): Promise<void> {
    this.clearPhoto();
    this.state.set('signed-out');
    if (this.kind() === 'demo') {
      const r = await fetch('/session/logout', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        redirect: 'error',
        headers: { 'X-FaultCode-CSRF': this.csrfToken },
      });
      this.demoUser = null;
      this.csrfToken = '';
      if (!r.ok) {
        this.state.set('error');
        throw new Error('Sign-out could not be completed.');
      }
      this.kind.set('entra');
      await this.loadDemoSession();
      return;
    }
    if (this.msal) await firstValueFrom(this.msal.logoutRedirect());
  }
  expire(): void {
    if (this.kind() === 'demo') {
      this.demoUser = null;
      void this.loadDemoSession();
    }
    this.clearPhoto();
    this.state.set('expired');
  }
  async identity(): Promise<{
    id: string;
    displayName: string;
    email: string;
    roles: string[];
    permissions: string[];
    tokenExpiresAt: string | null;
    profilePhoto?: string;
  }> {
    if (this.kind() === 'demo' && this.demoUser) return this.demoUser;
    // UI projection only. The API validates signatures, issuer, audience, scopes and roles independently.
    const token = await this.token();
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return {
      id: payload.oid,
      displayName: payload.name ?? this.profileName(),
      email: payload.preferred_username ?? '',
      tokenExpiresAt: new Date(payload.exp * 1000).toISOString(),
      roles: payload.roles ?? [],
      permissions: typeof payload.scp === 'string' ? payload.scp.split(' ') : [],
    };
  }
  async apiToken(
    refresh = false,
  ): Promise<{ accessToken: string; expiresAt: string | null; identityName: string }> {
    if (this.state() !== 'signed-in') throw new Error('session');
    if (this.kind() === 'demo') {
      const r = await fetch('/session/api-token?refresh=' + refresh, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        redirect: 'error',
        headers: { 'X-FaultCode-CSRF': this.csrfToken },
      });
      if (!r.ok) throw new Error('Demo API token is unavailable.');
      const result = await r.json();
      if (this.kind() !== 'demo' || this.state() !== 'signed-in') throw new Error('session');
      return result;
    }
    const accessToken = await (refresh ? this.refreshAccessToken() : this.token());
    const user = await this.identity();
    return { accessToken, expiresAt: user.tokenExpiresAt, identityName: user.displayName };
  }
  async refreshAccessToken(): Promise<string> {
    this.refreshToken = true;
    return this.token();
  }
  async token(): Promise<string> {
    if (this.kind() === 'demo' || !this.msal || !this.config || this.state() !== 'signed-in')
      throw new Error('session');
    try {
      const result = await firstValueFrom(
        this.msal.acquireTokenSilent({
          scopes: this.config.scopes,
          account: this.msal.instance.getActiveAccount()!,
          forceRefresh: this.refreshToken,
        }),
      );
      this.refreshToken = false;
      return result.accessToken;
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) this.expire();
      else this.state.set('error');
      throw new Error('session');
    }
  }
}
