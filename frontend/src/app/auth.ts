import { Injectable, signal, inject, Injector } from '@angular/core';
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
  readonly profileName = signal('');
  readonly profilePhoto = signal('');
  private refreshToken = true;
  private config?: BrowserConfig;
  private msal?: MsalService;
  async initialize(): Promise<void> {
    try {
      const response = await fetch('/browser-config.json', {
        cache: 'no-store',
        redirect: 'error',
      });
      const c = (await response.json()) as BrowserConfig;
      if (!response.ok || !validConfig(c)) {
        this.state.set('unconfigured');
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
  private clearPhoto(): void {
    this.profilePhoto.set('');
  }
  async login(): Promise<void> {
    if (this.msal && this.config)
      await firstValueFrom(this.msal.loginRedirect({ scopes: this.config.scopes }));
  }
  async logout(): Promise<void> {
    this.clearPhoto();
    this.state.set('signed-out');
    if (this.msal) await firstValueFrom(this.msal.logoutRedirect());
  }
  expire(): void {
    this.clearPhoto();
    this.state.set('expired');
  }
  async token(): Promise<string> {
    if (!this.msal || !this.config || this.state() !== 'signed-in') throw new Error('session');
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
