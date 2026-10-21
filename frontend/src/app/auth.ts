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
}
export function validConfig(c: BrowserConfig): boolean {
  const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  return (
    !!c &&
    [c.tenantId, c.clientId, c.apiClientId].every((x) => typeof x === 'string' && guid.test(x)) &&
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
      this.state.set(instance.getActiveAccount() ? 'signed-in' : 'signed-out');
    } catch {
      this.state.set('error');
    }
  }
  async login(): Promise<void> {
    if (this.msal && this.config)
      await firstValueFrom(this.msal.loginRedirect({ scopes: this.config.scopes }));
  }
  async logout(): Promise<void> {
    this.state.set('signed-out');
    if (this.msal) await firstValueFrom(this.msal.logoutRedirect());
  }
  expire(): void {
    this.state.set('expired');
  }
  async token(): Promise<string> {
    if (!this.msal || !this.config || this.state() !== 'signed-in') throw new Error('session');
    try {
      return (
        await firstValueFrom(
          this.msal.acquireTokenSilent({
            scopes: this.config.scopes,
            account: this.msal.instance.getActiveAccount()!,
          }),
        )
      ).accessToken;
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) this.expire();
      else this.state.set('error');
      throw new Error('session');
    }
  }
}
