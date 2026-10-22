import { inject, Injectable } from '@angular/core';
import { Auth } from './auth';
export class ApiError extends Error {
  constructor(
    public status: number,
    public correlationId = '',
  ) {
    super('Request failed');
  }
}
export function apiPath(path: string): boolean {
  if (!path.startsWith('/api/v1/') || /[\\\r\n#]/.test(path) || path.includes('//')) return false;
  try {
    const u = new URL(path, 'https://faultcode.invalid');
    const decoded = decodeURIComponent(u.pathname);
    return (
      u.origin === 'https://faultcode.invalid' &&
      u.pathname.startsWith('/api/v1/') &&
      !decoded.split('/').some((p) => p === '.' || p === '..') &&
      !/%2f|%5c/i.test(u.pathname)
    );
  } catch {
    return false;
  }
}
@Injectable({ providedIn: 'root' })
export class Api {
  private auth = inject(Auth);
  async request(
    path: string,
    method = 'GET',
    body?: unknown,
    headers: Record<string, string> = {},
  ): Promise<{ data: any; etag: string | null }> {
    if (!apiPath(path)) throw new Error('Invalid API boundary');
    const authorization = await this.auth.apiHeaders();
    const target = this.auth.kind() === 'demo' ? '/workspace' + path : path;
    const response = await fetch(target, {
      method,
      redirect: 'error',
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { ...headers, 'Content-Type': 'application/json', ...authorization },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      if (response.status === 401) this.auth.expire();
      throw new ApiError(response.status, response.headers.get('X-Correlation-ID') ?? '');
    }
    return {
      data: response.status === 204 ? undefined : await response.json(),
      etag: response.headers.get('ETag'),
    };
  }
}
