import { TestBed } from '@angular/core/testing';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { Auth, validConfig } from './auth';
import { Api, ApiError, apiPath } from './api';
afterEach(() => {
  vi.unstubAllGlobals();
  TestBed.resetTestingModule();
});
describe('exclusive protected API client', () => {
  it('validates isolated public configuration and rejects alternate resource scopes', () => {
    const c = {
      tenantId: '11111111-1111-4111-8111-111111111111',
      clientId: '22222222-2222-4222-8222-222222222222',
      apiClientId: '33333333-3333-4333-8333-333333333333',
      scopes: ['api://33333333-3333-4333-8333-333333333333/authorization.read'],
    };
    expect(validConfig(c)).toBe(true);
    expect(validConfig({ ...c, scopes: ['https://graph.microsoft.com/.default'] })).toBe(false);
    expect(validConfig({ ...c, tenantId: '' })).toBe(false);
  });
  it('never attaches tokens to arbitrary origins or alternate routes', () => {
    for (const p of [
      'https://evil.invalid/api/v1/users',
      '//evil.invalid',
      '/api/v2/users',
      '/api/v1//evil',
      '/api/v1/users#x',
      '/api/v1/../../outside',
      '/api/v1/%2e%2e/outside',
      '/api/v1/users%2f..%2f..',
    ])
      expect(apiPath(p)).toBe(false);
    expect(apiPath('/api/v1/vehicles?page=1')).toBe(true);
  });
  it('preserves preconditions, rejects redirect following and expires a401 session', async () => {
    const expire = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: Auth, useValue: { token: async () => 'synthetic', expire } }],
    });
    const fetcher = vi
      .fn()
      .mockResolvedValue(
        new Response('{}', { status: 401, headers: { 'X-Correlation-ID': 'synthetic-reference' } }),
      );
    vi.stubGlobal('fetch', fetcher);
    await expect(
      TestBed.inject(Api).request('/api/v1/vehicles', 'POST', {}, { 'Idempotency-Key': 'one' }),
    ).rejects.toBeInstanceOf(ApiError);
    expect(expire).toHaveBeenCalledOnce();
    expect(fetcher.mock.calls[0][1]).toMatchObject({
      redirect: 'error',
      cache: 'no-store',
      headers: { Authorization: 'Bearer synthetic', 'Idempotency-Key': 'one' },
    });
  });
});
