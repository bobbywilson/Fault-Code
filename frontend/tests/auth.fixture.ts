// Test-only replacement, never imported by production source or assets.
import { Injectable, signal } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class Auth {
  state = signal<'loading' | 'unconfigured' | 'signed-out' | 'signed-in' | 'expired' | 'error'>(
    'loading',
  );
  async initialize() {
    try {
      const r = await fetch('/__test/session');
      this.state.set((await r.json()).state);
    } catch {
      this.state.set('error');
    }
  }
  async token() {
    if (this.state() !== 'signed-in') throw new Error('session');
    return 'synthetic-test-token';
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
