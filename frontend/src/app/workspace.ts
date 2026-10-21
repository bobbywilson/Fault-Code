import { Component, Input, inject, signal, computed, effect } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BrnButton } from '@spartan-ng/brain/button';
import { Auth } from './auth';
import { Api, ApiError } from './api';
import { Fields } from './fields';
import { Records } from './records';
import { Schema, initial, validate, clean } from './schema';
import operations from './operations.json';
interface Operation {
  id: string;
  label: string;
  group: string;
  method: string;
  path: string;
  parameters: { name: string; where: string; required: boolean; schema: Schema }[];
  headers: string[];
  body: Schema | null;
  policies: string[][];
}
const ops = operations as Operation[];
export const areas = [
  ['Incidents', 'incidents'],
  ['Vehicles', 'vehicles'],
  ['Drivers', 'drivers'],
  ['Fault definitions', 'faults'],
  ['Ontology', 'ontology'],
  ['Documents', 'documents'],
  ['Engineering', 'engineering'],
  ['Agent activity', 'activity'],
  ['Notifications', 'notifications'],
  ['Users & roles', 'users'],
  ['Integrations', 'integrations'],
];
@Component({
  selector: 'app-workspace',
  imports: [RouterLink, RouterLinkActive, FormsModule, BrnButton, Fields, Records],
  template: ` <a class="skip" href="#main">Skip to workspace</a>
    <header>
      <div>
        <div class="brand">Fault<span>Code</span></div>
        <div class="tag">Administration workspace</div>
      </div>
      <div class="status">
        <span class="dot"></span
        ><span>{{
          auth.state() === 'signed-in' ? 'Authenticated workspace' : 'Protected workspace'
        }}</span>
        @if (auth.state() === 'signed-in') {
          <button brnButton (click)="logout()">Sign out</button>
        }
        <a href="/openapi/v1.json" target="_blank" rel="noopener">API reference</a>
      </div>
    </header>
    <div class="layout">
      <nav aria-label="Administration">
        @for (item of areas; track item[1]) {
          <a
            [routerLink]="'/admin/' + item[1]"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            >{{ item[0] }}</a
          >
        }
      </nav>
      <main id="main" tabindex="-1">
        <p class="eyebrow">Steering systems / Administration</p>
        <h1>{{ group() }}</h1>
        <p class="muted">{{ description() }}</p>
        @if (auth.state() === 'loading') {
          <div role="status" class="notice">Loading secure workspace…</div>
        } @else if (auth.state() === 'unconfigured') {
          <section class="card">
            <h2>Sign-in setup unavailable</h2>
            <p>
              Connect the dedicated Fault Code Microsoft Entra registration to use this workspace.
            </p>
            <p class="muted">
              No protected records are loaded. Identity and business storage must be configured
              before administration is available.
            </p>
          </section>
        } @else if (auth.state() === 'error') {
          <div role="alert" class="notice error">
            Sign-in could not be completed. Reload and try again.
          </div>
        } @else if (auth.state() !== 'signed-in') {
          <section class="card">
            <h2>
              {{ auth.state() === 'expired' ? 'Your session has expired' : 'Sign in to continue' }}
            </h2>
            <p>Your workspace requires an authorized Fault Code account.</p>
            <button brnButton class="primary" (click)="login()">Sign in with Microsoft</button>
          </section>
        } @else {
          @if (group() === 'Users & roles' || group() === 'Integrations') {
            <p class="notice">
              These actions manage local administration records. External identity grants and
              provider activation require separate configuration.
            </p>
          }
          @if (group() === 'Agent activity') {
            <p class="notice">
              Activity shows recorded investigations. Model usage and performance are unavailable
              when not recorded.
            </p>
          }
          <section class="card">
            <h2>Workspace actions</h2>
            <div class="field">
              <label for="action">Action</label
              ><select id="action" [ngModel]="op()?.id ?? ''" (ngModelChange)="select($event)">
                @for (action of actions(); track action.id) {
                  <option [value]="action.id">{{ action.label }}</option>
                }
              </select>
            </div>
            @if (permissionsLoading()) {
              <p role="status">Checking permissions…</p>
            } @else if (!op() || !allowed(op()!)) {
              <div role="alert" class="notice error">
                Access denied. This action requires additional permissions.
              </div>
            } @else if (op(); as action) {
              <form (ngSubmit)="execute()" class="form-grid">
                <app-fields [schema]="parameterSchema()" [model]="parameters" prefix="params" />
                @if (action.body) {
                  <app-fields [schema]="action.body" [model]="body" prefix="body" />
                }
                @if (action.headers.includes('If-Match')) {
                  <div class="field">
                    <label for="version">Current record version *</label
                    ><input
                      id="version"
                      name="version"
                      type="number"
                      min="1"
                      required
                      [(ngModel)]="version"
                    /><small class="muted"
                      >Read the current record before changing it. Conflicts require a fresh
                      read.</small
                    >
                  </div>
                }
                @if (mutation(action)) {
                  <div class="notice">
                    <label
                      ><input type="checkbox" name="confirm" [(ngModel)]="confirmed" /> Confirm
                      {{ action.label.toLowerCase() }} for the selected record.</label
                    >
                  </div>
                }
                <div class="toolbar" style="margin-top:20px">
                  <button brnButton class="primary" type="submit" [disabled]="busy() || blocked()">
                    {{
                      busy() ? 'Working…' : action.method === 'GET' ? 'Load records' : action.label
                    }}
                  </button>
                  @if (blocked()) {
                    <span class="muted">Reload the current record before retrying.</span>
                  }
                </div>
              </form>
            }
          </section>
          @if (message()) {
            <div
              [class]="'notice ' + (error() ? 'error' : 'success')"
              [attr.role]="error() ? 'alert' : 'status'"
            >
              {{ message() }}
              @if (correlation()) {
                <small>Reference: {{ correlation() }}</small>
              }
            </div>
          }
          @if (busy()) {
            <p role="status" aria-live="polite">Loading…</p>
          }
          @if (data() !== undefined) {
            <section class="card" aria-label="Recorded results">
              <h2>Recorded results</h2>
              <div class="results">
                @if (hasPage()) {
                  @for (record of data().items; track $index) {
                    <div class="record">
                      <app-records [value]="record" />
                      @if (record.id && canOpenRecord()) {
                        <button brnButton class="quiet" (click)="openRecord(record)">
                          Open record
                        </button>
                      }
                    </div>
                  }
                  @if (data().items.length === 0) {
                    <p class="empty">No recorded items.</p>
                  }
                } @else {
                  <app-records [value]="data()" />
                }
              </div>
              @if (hasPage()) {
                <div class="pager">
                  <button brnButton (click)="page(-1)" [disabled]="busy() || data().page <= 1">
                    Previous</button
                  ><span>Page {{ data().page }} · {{ data().totalCount }} recorded items</span
                  ><button
                    brnButton
                    (click)="page(1)"
                    [disabled]="busy() || data().page * data().pageSize >= data().totalCount"
                  >
                    Next
                  </button>
                </div>
              }
            </section>
          }
        }
        <p class="footer-note">
          Fault Code · Synthetic steering investigation proof of concept. Approved remedies are
          recommendations for dealership service.
        </p>
      </main>
    </div>`,
})
export class Workspace {
  auth = inject(Auth);
  private api = inject(Api);
  areas = areas;
  private routeArea = signal('incidents');
  @Input() set area(value: string) {
    this.routeArea.set(value ?? 'incidents');
  }
  group = computed(() => areas.find((a) => a[1] === this.routeArea())?.[0] ?? 'Incidents');
  actions = computed(() => ops.filter((o) => o.group === this.group()));
  op = signal<Operation | undefined>(undefined);
  permissions = signal<string[]>([]);
  permissionsLoading = signal(true);
  busy = signal(false);
  data = signal<any>(undefined);
  message = signal('');
  error = signal(false);
  correlation = signal('');
  blocked = signal(false);
  parameters: any = {};
  body: any = {};
  version: number | null = null;
  confirmed = false;
  private epoch = 0;
  private fingerprint = '';
  private key = '';
  constructor() {
    effect(() => {
      this.group();
      this.reset();
      const a = this.actions();
      this.choose(
        a.find((o) => o.method === 'GET' && !o.parameters.some((p) => p.where === 'path')) ?? a[0],
      );
      queueMicrotask(() => document.getElementById('main')?.focus());
    });
    effect(() => {
      if (this.auth.state() === 'signed-in') void this.loadPermissions();
      else {
        this.permissions.set([]);
        this.data.set(undefined);
        this.epoch++;
      }
    });
  }
  description() {
    return (
      {
        Incidents: 'Review fault intake, investigation history and workflow status.',
        Vehicles: 'Manage the steering fleet and driver associations.',
        Drivers: 'Manage driver records and local notification preferences.',
        Engineering: 'Track escalations, evidence and approved resolutions.',
        Notifications: 'Review recorded notification history and delivery status.',
        Documents: 'Manage approved technical documents and search grounded evidence.',
        Ontology: 'Manage versioned concepts, relationships and deterministic rules.',
        'Fault definitions': 'Maintain fault knowledge and engineering publication.',
        'Agent activity': 'Inspect recorded advisory and investigation activity.',
        'Users & roles': 'Review local users and least-privilege role assignments.',
        Integrations: 'Review authorized integration registrations.',
      } as Record<string, string>
    )[this.group()];
  }
  private async loadPermissions() {
    this.permissionsLoading.set(true);
    try {
      const r = await this.api.request('/api/v1/authorization/me');
      this.permissions.set(r.data.callerKind === 'delegated' ? r.data.permissions : []);
    } catch (e) {
      this.fail(e);
    } finally {
      this.permissionsLoading.set(false);
    }
  }
  allowed(o: Operation) {
    return o.policies.every((a) => a.some((p) => this.permissions().includes(p)));
  }
  mutation(o: Operation) {
    return o.method !== 'GET' && !['SearchEvidence', 'EvaluateOntology'].includes(o.id);
  }
  parameterSchema(): Schema {
    const o = this.op();
    return {
      type: 'object',
      properties: Object.fromEntries((o?.parameters ?? []).map((p) => [p.name, p.schema])),
      required: o?.parameters.filter((p) => p.required).map((p) => p.name) ?? [],
    };
  }
  reset() {
    this.epoch++;
    this.data.set(undefined);
    this.message.set('');
    this.error.set(false);
    this.correlation.set('');
    this.busy.set(false);
    this.blocked.set(false);
    this.confirmed = false;
    this.fingerprint = '';
    this.key = '';
  }
  choose(o?: Operation) {
    this.op.set(o);
    this.parameters = {};
    for (const p of o?.parameters ?? [])
      if (p.schema.default !== undefined) this.parameters[p.name] = p.schema.default;
    this.body = o?.body ? initial(o.body) : {};
    this.version = null;
  }
  select(id: string) {
    const o = this.actions().find((x) => x.id === id);
    const recordId = this.parameters.id;
    const version = this.version;
    this.reset();
    this.choose(o);
    if (recordId && o?.parameters.some((p) => p.name === 'id')) {
      this.parameters.id = recordId;
      this.version = version;
    }
  }
  canOpenRecord() {
    return this.actions().some(
      (a) =>
        a.method === 'GET' && a.parameters.some((p) => p.name === 'id') && a.path.endsWith('{id}'),
    );
  }
  openRecord(record: any) {
    const o = this.actions().find(
      (a) =>
        a.method === 'GET' && a.parameters.some((p) => p.name === 'id') && a.path.endsWith('{id}'),
    );
    if (!o) return;
    this.reset();
    this.choose(o);
    this.parameters.id = record.id;
    void this.execute();
  }
  async login() {
    try {
      await this.auth.login();
    } catch {
      this.message.set('Sign-in could not be started.');
      this.error.set(true);
    }
  }
  async logout() {
    this.reset();
    try {
      await this.auth.logout();
    } catch {
      this.message.set('Sign-out could not be completed.');
      this.error.set(true);
    }
  }
  async execute() {
    const o = this.op();
    if (!o || !this.allowed(o) || this.busy() || this.blocked()) return;
    if (
      !validate(this.parameterSchema(), this.parameters) ||
      (o.body && !validate(o.body, this.body))
    ) {
      this.message.set('Complete all required fields with valid values.');
      this.error.set(true);
      return;
    }
    if (this.mutation(o) && !this.confirmed) {
      this.message.set('Confirm the action before continuing.');
      this.error.set(true);
      return;
    }
    if (
      o.headers.includes('If-Match') &&
      (!Number.isSafeInteger(this.version) || (this.version ?? 0) < 1)
    ) {
      this.message.set('Read the current record and enter its version.');
      this.error.set(true);
      return;
    }
    let path = o.path;
    const query = new URLSearchParams();
    for (const p of o.parameters) {
      const v = this.parameters[p.name];
      if (v === undefined || v === '') continue;
      if (p.where === 'path')
        path = path.replace('{' + p.name + '}', encodeURIComponent(String(v)));
      else query.set(p.name, String(v));
    }
    if (query.size) path += '?' + query;
    const body = o.body ? clean(o.body, this.body) : undefined;
    const fingerprint = JSON.stringify([o.id, path, body, this.version]);
    if (fingerprint !== this.fingerprint) {
      this.fingerprint = fingerprint;
      this.key = crypto.randomUUID();
    }
    const headers: Record<string, string> = {};
    if (o.headers.includes('If-Match')) headers['If-Match'] = `"${this.version}"`;
    if (o.headers.includes('Idempotency-Key')) headers['Idempotency-Key'] = this.key;
    const epoch = ++this.epoch;
    this.busy.set(true);
    this.data.set(undefined);
    this.message.set('');
    this.error.set(false);
    try {
      const r = await this.api.request(path, o.method, body, headers);
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      this.data.set(r.data);
      if (r.etag && /^"[1-9][0-9]*"$/.test(r.etag)) this.version = Number(r.etag.slice(1, -1));
      if (this.mutation(o)) {
        this.message.set('The request completed. Review the recorded result below.');
        this.confirmed = false;
        this.blocked.set(true);
      }
    } catch (e) {
      if (epoch === this.epoch) this.fail(e);
    } finally {
      if (epoch === this.epoch) this.busy.set(false);
    }
  }
  fail(e: unknown) {
    this.data.set(undefined);
    this.error.set(true);
    if (e instanceof ApiError) {
      this.correlation.set(e.correlationId);
      const m: Record<number, string> = {
        400: 'Some values were rejected. Review the form and try again.',
        401: 'Your session has expired. Sign in again.',
        403: 'Access denied. This action requires additional permissions.',
        404: 'The record is unavailable or no longer accessible.',
        409: 'The record changed or this retry conflicts. Reload the current record before retrying.',
        422: 'The request cannot be completed with the current evidence.',
        429: 'Too many requests. Wait before trying again.',
        503: 'This service is unavailable. No successful change is recorded.',
        504: 'The request timed out. Review recorded history before retrying.',
      };
      this.message.set(
        m[e.status] ?? 'The request failed. Review recorded history before retrying.',
      );
      if (e.status === 409) {
        this.blocked.set(true);
        this.version = null;
      }
    } else
      this.message.set(
        'The request could not be completed. Check your connection or sign in again.',
      );
  }
  hasPage() {
    return (
      this.data() && Array.isArray(this.data().items) && Number.isInteger(this.data().totalCount)
    );
  }
  resultItems() {
    return this.hasPage() ? this.data().items : this.data();
  }
  page(d: number) {
    if (this.hasPage()) {
      this.parameters.page = this.data().page + d;
      this.parameters.pageSize = this.data().pageSize;
      void this.execute();
    }
  }
}
