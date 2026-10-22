// Test-only replacement, never imported by production source or assets.
import { Injectable, signal } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class Auth {
  kind = signal<'entra' | 'demo'>('entra');
  demoAvailable = signal(false);
  async apiHeaders() {
    return { Authorization: `Bearer ${await this.token()}` };
  }
  async loginDemo(username: string, password: string) {
    this.kind.set('demo');
    this.profileName.set('John Henry');
    this.profilePhoto.set('');
    this.state.set('signed-in');
  }
  profileName = signal('Bobby Wilson');
  profilePhoto = signal('');
  state = signal<'loading' | 'unconfigured' | 'signed-out' | 'signed-in' | 'expired' | 'error'>(
    'loading',
  );
  private permissions: string[] = [];
  async initialize() {
    try {
      const r = await fetch('/__test/session');
      const session = await r.json();
      this.kind.set(session.kind ?? 'entra');
      if (session.kind === 'demo') {
        this.profileName.set('John Henry');
        this.profilePhoto.set('');
      }
      this.demoAvailable.set(session.demoAvailable ?? false);
      this.permissions = session.permissions ?? [];
      this.state.set(session.state);
    } catch {
      this.state.set('error');
    }
  }
  async token() {
    if (this.state() !== 'signed-in') throw new Error('session');
    return 'demo-test-token';
  }
  async identity() {
    return {
      id: '55555555-5555-4555-8555-555555555555',
      displayName: this.profileName(),
      email: 'bobby@example.com',
      roles: ['Operator', 'Reader'],
      permissions: this.permissions,
      tokenExpiresAt: '2030-01-01T00:00:00Z',
    };
  }
  async apiToken(refresh = false) {
    return {
      accessToken: 'test-application-token',
      expiresAt: '2030-01-01T00:00:00Z',
      identityName: this.kind() === 'demo' ? 'Fault Code Demo API' : 'Bobby Wilson',
    };
  }
  async refreshAccessToken() {
    return this.token();
  }
  async login() {
    this.state.set('signed-in');
  }
  async logout() {
    this.state.set('signed-out');
  }
  expire() {
    this.state.set('expired');
  }
}
