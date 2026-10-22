import { DatePipe } from '@angular/common';
import { Component, Input, inject, signal, computed, effect } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { ontologyRelationships } from './ontology-model';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmBreadcrumbImports } from '@spartan-ng/helm/breadcrumb';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideTriangleAlert,
  lucideCar,
  lucideUsers,
  lucideWrench,
  lucideNetwork,
  lucideFiles,
  lucideFlaskConical,
  lucideBot,
  lucideBell,
  lucidePlug,
  lucideShieldCheck,
  lucideArrowUpRight,
} from '@ng-icons/lucide';
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
  imports: [
    DatePipe,
    RouterLink,
    RouterLinkActive,
    FormsModule,
    HlmButton,
    HlmTableImports,
    HlmSidebarImports,
    HlmCardImports,
    HlmAlertImports,
    HlmBadge,
    HlmEmptyImports,
    HlmBreadcrumbImports,
    HlmFieldImports,
    HlmInput,
    HlmNativeSelectImports,
    HlmCheckbox,
    NgIcon,
    Fields,
    Records,
  ],
  providers: [
    provideIcons({
      lucideTriangleAlert,
      lucideCar,
      lucideUsers,
      lucideWrench,
      lucideNetwork,
      lucideFiles,
      lucideFlaskConical,
      lucideBot,
      lucideBell,
      lucidePlug,
      lucideShieldCheck,
      lucideArrowUpRight,
    }),
  ],
  template: ` <a hlmBtn class="sr-only focus:not-sr-only focus:fixed focus:z-50" href="#main"
      >Skip to workspace</a
    >
    <div hlmSidebarWrapper>
      @if (auth.state() === 'signed-in') {
        <hlm-sidebar
          variant="inset"
          collapsible="icon"
          role="complementary"
          aria-label="Workspace navigation"
        >
          <div hlmSidebarContent>
            <div hlmSidebarGroup>
              <div hlmSidebarGroupLabel>Workspace</div>
              <nav hlmSidebarGroupContent aria-label="Administration">
                <ul hlmSidebarMenu>
                  @for (item of areas; track item[1]; let index = $index) {
                    <li hlmSidebarMenuItem>
                      <a
                        hlmSidebarMenuButton
                        [routerLink]="'/admin/' + item[1]"
                        routerLinkActive
                        #active="routerLinkActive"
                        [isActive]="active.isActive"
                        ariaCurrentWhenActive="page"
                        [tooltip]="item[0]"
                      >
                        <ng-icon [name]="icons[index]" /><span>{{ item[0] }}</span>
                      </a>
                    </li>
                  }
                </ul>
              </nav>
            </div>
          </div>
          <div hlmSidebarFooter>
            <ul hlmSidebarMenu>
              <li hlmSidebarMenuItem>
                <a hlmSidebarMenuButton href="/openapi/v1.json" target="_blank" rel="noopener"
                  ><ng-icon name="lucideArrowUpRight" /><span>API reference</span></a
                >
              </li>
              <li hlmSidebarMenuItem>
                <span hlmBadge variant="outline" class="m-2"
                  ><ng-icon name="lucideShieldCheck" class="mr-1 size-3" />Protected workspace</span
                >
              </li>
            </ul>
          </div>
        </hlm-sidebar>
      }
      <main hlmSidebarInset id="main" tabindex="-1" class="min-w-0">
        <header class="flex h-16 shrink-0 items-center justify-between gap-4 border-b px-4 md:px-6">
          <div class="flex items-center gap-3">
            @if (auth.state() === 'signed-in') {
              <button hlmSidebarTrigger></button>
            }
            <nav hlmBreadcrumb>
              <ol hlmBreadcrumbList>
                <li hlmBreadcrumbItem><span hlmBreadcrumbLink>Workspace</span></li>
                <li hlmBreadcrumbSeparator></li>
                <li hlmBreadcrumbItem>
                  <span hlmBreadcrumbPage>{{ group() }}</span>
                </li>
              </ol>
            </nav>
          </div>
          <div class="flex items-center gap-3">
            <span hlmBadge variant="outline">{{
              auth.state() === 'signed-in' ? 'Authenticated' : 'Secure access'
            }}</span>
            @if (auth.state() === 'signed-in') {
              <button hlmBtn variant="ghost" size="sm" (click)="logout()">Sign out</button>
            }
          </div>
        </header>
        <div class="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-6 p-4 md:p-8">
          <div class="space-y-2">
            <div class="flex items-center gap-3">
              <h1 class="text-2xl font-semibold tracking-tight">{{ group() }}</h1>
              <span hlmBadge variant="secondary">{{
                group() === 'Ontology' ? 'Model reference' : 'Administration'
              }}</span>
            </div>
            <p class="max-w-2xl text-sm text-muted-foreground">{{ description() }}</p>
          </div>
          @if (auth.state() === 'loading') {
            <div role="status" hlmAlert>Loading secure workspace…</div>
          } @else if (auth.state() === 'unconfigured') {
            <section hlmCard class="p-6">
              <h2 hlmCardTitle>Sign-in setup unavailable</h2>
              <p>
                Connect the dedicated Fault Code Microsoft Entra registration to use this workspace.
              </p>
              <p class="text-sm text-muted-foreground">
                No protected records are loaded. Identity and business storage must be configured
                before administration is available.
              </p>
            </section>
          } @else if (auth.state() === 'error') {
            <div role="alert" hlmAlert variant="destructive">
              Sign-in could not be completed. Reload and try again.
            </div>
          } @else if (auth.state() !== 'signed-in') {
            <section hlmCard class="p-6">
              <h2 hlmCardTitle>
                {{
                  auth.state() === 'expired' ? 'Your session has expired' : 'Sign in to continue'
                }}
              </h2>
              <p>Your workspace requires an authorized Fault Code account.</p>
              <button hlmBtn class="w-fit" (click)="login()">Sign in with Microsoft</button>
            </section>
          } @else {
            @if (group() === 'Users & roles' || group() === 'Integrations') {
              <p hlmAlert role="note">
                These actions manage local administration records. External identity grants and
                provider activation require separate configuration.
              </p>
            }
            @if (group() === 'Agent activity') {
              <p hlmAlert role="note">
                Activity shows recorded investigations. Model usage and performance are unavailable
                when not recorded.
              </p>
            }
            @if (group() === 'Incidents' && !incidentReview()) {
              <section hlmCard class="gap-4 p-6" aria-label="Unmatched vehicle incidents">
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <div class="space-y-2">
                    <h2 hlmCardTitle>Unmatched vehicle reports</h2>
                    <p hlmCardDescription>
                      Vehicle-reported faults with no approved catalog match at intake.
                    </p>
                  </div>
                  <button
                    hlmBtn
                    variant="outline"
                    (click)="reloadIncidents()"
                    [disabled]="busy() || permissionsLoading() || !incidentAllowed()"
                  >
                    Refresh
                  </button>
                </div>
                @if (permissionsLoading() || busy()) {
                  <p role="status">Loading incidents…</p>
                } @else if (!incidentAllowed()) {
                  <p hlmAlert variant="destructive">
                    Access denied. You need permission to read incidents and the associated
                    vehicles.
                  </p>
                }
                @if (message() && error()) {
                  <div hlmAlert variant="destructive">
                    {{ message() }}
                    @if (correlation()) {
                      <small>Reference: {{ correlation() }}</small>
                    }
                  </div>
                }
                @if (hasPage()) {
                  <div hlmTableContainer>
                    <table hlmTable>
                      <caption hlmCaption>
                        {{
                          data().totalCount
                        }}
                        unmatched vehicle reports · page
                        {{
                          data().page
                        }}
                      </caption>
                      <thead hlmTHead>
                        <tr hlmTr>
                          <th hlmTh scope="col">Fault code</th>
                          <th hlmTh scope="col">Vehicle</th>
                          <th hlmTh scope="col">Driver</th>
                          <th hlmTh scope="col">System</th>
                          <th hlmTh scope="col">Reported (UTC)</th>
                          <th hlmTh scope="col">Status</th>
                          <th hlmTh scope="col"><span class="sr-only">Review incident</span></th>
                        </tr>
                      </thead>
                      <tbody hlmTBody>
                        @for (incident of data().items; track incident.id) {
                          <tr hlmTr>
                            <th hlmTh scope="row" class="text-foreground">{{ incident.code }}</th>
                            <td hlmTd>{{ incident.fleet?.vehicleIdentifier ?? 'Unavailable' }}</td>
                            <td hlmTd>{{ incident.fleet?.driverIdentifier ?? 'Unavailable' }}</td>
                            <td hlmTd>{{ incident.system }}</td>
                            <td hlmTd>{{ incident.occurredAt | date: 'medium' : 'UTC' }}</td>
                            <td hlmTd>
                              <span hlmBadge variant="secondary">{{ incident.status }}</span>
                            </td>
                            <td hlmTd>
                              <button
                                hlmBtn
                                variant="ghost"
                                size="sm"
                                (click)="reviewIncident(incident)"
                                [attr.aria-label]="'Review incident ' + incident.code"
                              >
                                Review <ng-icon name="lucideArrowUpRight" />
                              </button>
                            </td>
                          </tr>
                        } @empty {
                          <tr hlmTr>
                            <td hlmTd colspan="7">
                              <div hlmEmpty>
                                <div hlmEmptyHeader>
                                  <div hlmEmptyMedia variant="icon">
                                    <ng-icon name="lucideTriangleAlert" />
                                  </div>
                                  <p hlmEmptyTitle>No unmatched incidents</p>
                                  <p hlmEmptyDescription>
                                    Vehicle reports without a known fault match will appear here.
                                  </p>
                                </div>
                              </div>
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                  <div class="flex items-center justify-between gap-3">
                    <button
                      hlmBtn
                      variant="outline"
                      (click)="reloadIncidents(data().page - 1)"
                      [disabled]="busy() || data().page <= 1"
                    >
                      Previous</button
                    ><button
                      hlmBtn
                      variant="outline"
                      (click)="reloadIncidents(data().page + 1)"
                      [disabled]="busy() || data().page * data().pageSize >= data().totalCount"
                    >
                      Next
                    </button>
                  </div>
                }
              </section>
            } @else if (group() === 'Ontology') {
              <section hlmCard class="gap-4 p-6" aria-label="Fault code ontology">
                <div class="space-y-2">
                  <h2 hlmCardTitle>Model relationships</h2>
                  <p hlmCardDescription>
                    Explore how fault codes connect to incidents, vehicles, drivers and
                    notifications.
                  </p>
                </div>
                <div hlmField class="max-w-sm">
                  <label hlmFieldLabel for="ontology-search">Find a relationship</label>
                  <input
                    hlmInput
                    id="ontology-search"
                    type="search"
                    placeholder="Search concepts or relationships…"
                    [ngModel]="ontologyQuery()"
                    (ngModelChange)="ontologyQuery.set($event)"
                  />
                </div>
                <div hlmTableContainer>
                  <table hlmTable>
                    <caption hlmCaption>
                      Fault-code domain model ·
                      {{
                        filteredRelationships().length
                      }}
                      relationships
                    </caption>
                    <thead hlmTHead>
                      <tr hlmTr>
                        <th hlmTh scope="col">Concept</th>
                        <th hlmTh scope="col">Relationship</th>
                        <th hlmTh scope="col">Related concept</th>
                        <th hlmTh scope="col">Meaning</th>
                        <th hlmTh scope="col">
                          <span class="sr-only">Browse related records</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (row of filteredRelationships(); track row.source) {
                        <tr hlmTr>
                          <th hlmTh scope="row" class="text-foreground">{{ row.from }}</th>
                          <td hlmTd class="text-muted-foreground">{{ row.relationship }}</td>
                          <td hlmTd>
                            <span hlmBadge variant="outline">{{ row.to }}</span>
                          </td>
                          <td hlmTd class="min-w-64 whitespace-normal">{{ row.detail }}</td>
                          <td hlmTd>
                            <a
                              hlmBtn
                              variant="ghost"
                              size="sm"
                              [routerLink]="'/admin/' + row.route"
                              [attr.aria-label]="
                                'Browse ' +
                                row.to.toLowerCase() +
                                ' records for ' +
                                row.from.toLowerCase()
                              "
                              >Browse <ng-icon name="lucideArrowUpRight"
                            /></a>
                          </td>
                        </tr>
                      } @empty {
                        <tr hlmTr>
                          <td hlmTd colspan="5" class="py-8 text-center text-muted-foreground">
                            No matching relationships. Try another search.
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </section>
            } @else {
              @if (group() === 'Incidents') {
                <button hlmBtn variant="outline" class="w-fit" (click)="reloadIncidents()">
                  Back to incidents
                </button>
              }
              <section hlmCard class="p-6">
                <h2 hlmCardTitle>
                  {{ group() === 'Incidents' ? 'Review incident' : 'Workspace actions' }}
                </h2>
                <div hlmField>
                  <label hlmFieldLabel for="action">Action</label
                  ><hlm-native-select
                    selectId="action"
                    [ngModel]="op()?.id ?? ''"
                    (ngModelChange)="select($event)"
                  >
                    @for (action of actions(); track action.id) {
                      <option [value]="action.id">{{ action.label }}</option>
                    }
                  </hlm-native-select>
                </div>
                @if (permissionsLoading()) {
                  <p role="status">Checking permissions…</p>
                } @else if (!op() || !allowed(op()!)) {
                  <div role="alert" hlmAlert variant="destructive">
                    Access denied. This action requires additional permissions.
                  </div>
                } @else if (op(); as action) {
                  <form (ngSubmit)="execute()" class="grid max-w-3xl gap-5">
                    <app-fields [schema]="parameterSchema()" [model]="parameters" prefix="params" />
                    @if (action.body) {
                      <app-fields [schema]="action.body" [model]="body" prefix="body" />
                    }
                    @if (action.headers.includes('If-Match')) {
                      <div hlmField>
                        <label hlmFieldLabel for="version">Current record version *</label
                        ><input
                          hlmInput
                          id="version"
                          name="version"
                          type="number"
                          min="1"
                          required
                          [(ngModel)]="version"
                        /><small class="text-sm text-muted-foreground"
                          >Read the current record before changing it. Conflicts require a fresh
                          read.</small
                        >
                      </div>
                    }
                    @if (mutation(action)) {
                      <div hlmField orientation="horizontal">
                        <hlm-checkbox
                          inputId="confirm-action"
                          name="confirm"
                          [(ngModel)]="confirmed"
                          aria-label="Confirm action"
                        /><label hlmFieldLabel for="confirm-action">
                          Confirm {{ action.label.toLowerCase() }} for the selected record.</label
                        >
                      </div>
                    }
                    <div class="flex flex-wrap items-center gap-3">
                      <button hlmBtn type="submit" [disabled]="busy() || blocked()">
                        {{
                          busy()
                            ? 'Working…'
                            : action.method === 'GET'
                              ? 'Load records'
                              : action.label
                        }}
                      </button>
                      @if (blocked()) {
                        <span class="text-sm text-muted-foreground"
                          >Reload the current record before retrying.</span
                        >
                      }
                    </div>
                  </form>
                }
              </section>
              @if (message()) {
                <div
                  [variant]="error() ? 'destructive' : 'default'"
                  hlmAlert
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
                <section hlmCard class="p-6" aria-label="Recorded results">
                  <h2 hlmCardTitle>Recorded results</h2>
                  <div class="space-y-4 overflow-x-auto">
                    @if (hasPage()) {
                      @for (record of data().items; track $index) {
                        <div class="space-y-4 border-b pb-4">
                          <app-records [value]="record" />
                          @if (record.id && canOpenRecord()) {
                            <button hlmBtn variant="outline" size="sm" (click)="openRecord(record)">
                              Open record
                            </button>
                          }
                        </div>
                      }
                      @if (data().items.length === 0) {
                        <div hlmEmpty>
                          <div hlmEmptyHeader>
                            <div hlmEmptyMedia variant="icon"><ng-icon name="lucideFiles" /></div>
                            <p hlmEmptyTitle>No recorded items.</p>
                            <p hlmEmptyDescription>
                              Records will appear here when this workspace has available data.
                            </p>
                          </div>
                        </div>
                      }
                    } @else {
                      <app-records [value]="data()" />
                    }
                  </div>
                  @if (hasPage()) {
                    <div class="flex flex-wrap items-center gap-4 border-t pt-4 text-sm">
                      <button hlmBtn (click)="page(-1)" [disabled]="busy() || data().page <= 1">
                        Previous</button
                      ><span>Page {{ data().page }} · {{ data().totalCount }} recorded items</span
                      ><button
                        hlmBtn
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
          }
        </div>
        <footer class="border-t px-4 py-4 text-xs text-muted-foreground md:px-8">
          <p>
            Fault Code · Synthetic steering investigation proof of concept. Approved remedies are
            recommendations for dealership service.
          </p>
        </footer>
      </main>
    </div>`,
})
export class Workspace {
  auth = inject(Auth);
  private api = inject(Api);
  areas = areas;
  incidentReview = signal(false);
  ontologyQuery = signal('');
  filteredRelationships = computed(() => {
    const query = this.ontologyQuery().trim().toLowerCase();
    return ontologyRelationships.filter((row) =>
      [row.from, row.relationship, row.to, row.detail].some((value) =>
        value.toLowerCase().includes(query),
      ),
    );
  });
  icons = [
    'lucideTriangleAlert',
    'lucideCar',
    'lucideUsers',
    'lucideWrench',
    'lucideNetwork',
    'lucideFiles',
    'lucideFlaskConical',
    'lucideBot',
    'lucideBell',
    'lucideUsers',
    'lucidePlug',
  ];
  private routeArea = signal('incidents');
  @Input() set area(value: string) {
    this.routeArea.set(value ?? 'incidents');
  }
  group = computed(() => areas.find((a) => a[1] === this.routeArea())?.[0] ?? 'Incidents');
  actions = computed(() =>
    ops.filter((o) => o.group === this.group() && o.id !== 'CreateIncident'),
  );
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
      this.ontologyQuery.set('');
      this.incidentReview.set(false);
      const a = this.group() === 'Ontology' ? [] : this.actions();
      this.choose(
        a.find((o) => o.method === 'GET' && !o.parameters.some((p) => p.where === 'path')) ?? a[0],
      );
      queueMicrotask(() => {
        document.getElementById('main')?.focus();
        if (
          this.group() === 'Incidents' &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadIncidents();
      });
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
        Incidents:
          'Investigate vehicle-reported fault codes that were not found in the approved fault catalog.',
        Vehicles: 'Manage the steering fleet and driver associations.',
        Drivers: 'Manage driver records and local notification preferences.',
        Engineering: 'Track escalations, evidence and approved resolutions.',
        Notifications: 'Review recorded notification history and delivery status.',
        Documents: 'Manage approved technical documents and search grounded evidence.',
        Ontology: 'Browse the fault-code model and its connections to operational records.',
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
      if (this.group() === 'Incidents' && this.auth.state() === 'signed-in')
        void this.reloadIncidents();
    }
  }
  incidentAllowed() {
    const operation = ops.find((o) => o.id === 'ListIncidents');
    return !!operation && this.allowed(operation);
  }
  async reloadIncidents(page = 1) {
    if (this.auth.state() !== 'signed-in' || this.permissionsLoading()) return;
    this.incidentReview.set(false);
    this.reset();
    this.choose(ops.find((o) => o.id === 'ListIncidents'));
    this.parameters = { page, pageSize: 20, unmatchedOnly: true };
    if (this.incidentAllowed()) await this.execute();
  }
  async reviewIncident(record: any) {
    this.incidentReview.set(true);
    this.reset();
    this.choose(ops.find((o) => o.id === 'GetIncident'));
    this.parameters = { id: record.id };
    await this.execute();
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
        503: 'This service is unavailable. Review recorded history before retrying.',
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
