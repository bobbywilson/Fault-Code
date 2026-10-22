import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmToasterImports } from '@spartan-ng/helm/sonner';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAvatar, HlmAvatarImage, HlmAvatarFallback } from '@spartan-ng/helm/avatar';
import {
  HlmDropdownMenu,
  HlmDropdownMenuTrigger,
  HlmDropdownMenuItem,
  HlmDropdownMenuLabel,
  HlmDropdownMenuSeparator,
} from '@spartan-ng/helm/dropdown-menu';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { Component, Input, inject, signal, computed, effect } from '@angular/core';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';
import { HlmBreadcrumbImports } from '@spartan-ng/helm/breadcrumb';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { HlmCheckbox } from '@spartan-ng/helm/checkbox';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideLayoutDashboard,
  lucideArrowRight,
  lucideArrowDown,
  lucideDatabase,
  lucideMail,
  lucideTriangleAlert,
  lucideCar,
  lucideUsers,
  lucideWrench,
  lucideCopy,
  lucideKeyRound,
  lucideChevronDown,
  lucideNetwork,
  lucideFiles,
  lucideFlaskConical,
  lucideBot,
  lucideBell,
  lucideHeartPulse,
  lucideArrowUpRight,
} from '@ng-icons/lucide';
import { Auth } from './auth';
import { Api, ApiError } from './api';
import { Fields } from './fields';
import { Records } from './records';
import { Tickets } from './tickets';
import { Schema, initial, validate, clean } from './schema';
import operations from './operations.json';
import domainModel from './domain-model.json';
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
  ['Dashboard', 'dashboard'],
  ['Vehicles', 'vehicles'],
  ['Drivers', 'drivers'],
  ['Incidents', 'incidents'],
  ['Notifications', 'notifications'],
  ['Users', 'users'],
  ['Ontology', 'ontology'],
  ['Fault codes', 'fault-codes'],
  ['Token', 'token'],
  ['Tickets', 'tickets'],
];
@Component({
  selector: 'app-workspace',
  imports: [
    DatePipe,
    HlmToasterImports,
    HlmDialogImports,
    RouterLink,
    RouterLinkActive,
    FormsModule,
    HlmButton,
    HlmAvatar,
    HlmAvatarImage,
    HlmAvatarFallback,
    HlmDropdownMenu,
    HlmDropdownMenuTrigger,
    HlmDropdownMenuItem,
    HlmDropdownMenuLabel,
    HlmDropdownMenuSeparator,
    HlmTableImports,
    HlmSidebarImports,
    HlmCardImports,
    HlmAlertImports,
    HlmBadge,
    HlmEmptyImports,
    HlmBreadcrumbImports,
    HlmFieldImports,
    HlmInput,
    HlmSelectImports,
    HlmTextarea,
    HlmCheckbox,
    NgIcon,
    Fields,
    Records,
    Tickets,
  ],
  providers: [
    provideIcons({
      lucideLayoutDashboard,
      lucideArrowRight,
      lucideArrowDown,
      lucideDatabase,
      lucideMail,
      lucideTriangleAlert,
      lucideCar,
      lucideUsers,
      lucideWrench,
      lucideCopy,
      lucideKeyRound,
      lucideChevronDown,
      lucideNetwork,
      lucideFiles,
      lucideFlaskConical,
      lucideBot,
      lucideBell,
      lucideHeartPulse,
      lucideArrowUpRight,
    }),
  ],
  template: ` <hlm-toaster />
    <a hlmBtn class="sr-only focus:not-sr-only focus:fixed focus:z-50" href="#main"
      >Skip to content</a
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
                  @for (item of visibleAreas(); track item[1]) {
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
                        <ng-icon [name]="icons[areas.indexOf(item)]" /><span>{{ item[0] }}</span>
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
                <a
                  hlmSidebarMenuButton
                  tooltip="API reference"
                  [href]="documentationUrl"
                  target="_blank"
                  rel="noopener"
                  ><ng-icon name="lucideArrowUpRight" /><span>API reference</span></a
                >
              </li>
              <li hlmSidebarMenuItem>
                <a
                  hlmSidebarMenuButton
                  tooltip="Development stack"
                  routerLink="/admin/dashboard"
                  fragment="development-stack"
                  ><ng-icon name="lucideFiles" /><span>Development stack</span></a
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
              <nav hlmBreadcrumb>
                <ol hlmBreadcrumbList>
                  <li hlmBreadcrumbItem><span hlmBreadcrumbLink>Workspace</span></li>
                  <li hlmBreadcrumbSeparator></li>
                  <li hlmBreadcrumbItem>
                    <span hlmBreadcrumbPage>{{ group() }}</span>
                  </li>
                </ol>
              </nav>
            } @else {
              <nav aria-label="Documentation" class="flex flex-wrap items-center gap-2">
                <a hlmBtn variant="outline" [href]="documentationUrl" target="_blank" rel="noopener"
                  >API documentation</a
                >
                <a hlmBtn variant="outline" [href]="apiTestingUrl" target="_blank" rel="noopener"
                  >Test API</a
                >
              </nav>
            }
          </div>
          @if (auth.state() === 'signed-in') {
            <button
              hlmBtn
              variant="ghost"
              size="sm"
              class="gap-1 rounded-full px-1"
              [attr.aria-label]="'Account menu for ' + auth.profileName() + ' ' + accountInitials()"
              [hlmDropdownMenuTrigger]="accountMenu"
              align="end"
            >
              <hlm-avatar aria-hidden="true">
                @if (auth.profilePhoto()) {
                  <img
                    hlmAvatarImage
                    [src]="auth.profilePhoto()"
                    [alt]="auth.profileName() + ' profile picture'"
                  />
                }
                <span hlmAvatarFallback class="text-foreground">{{ accountInitials() }}</span>
              </hlm-avatar>
              <ng-icon name="lucideChevronDown" class="size-3" aria-hidden="true" />
            </button>
            <ng-template #accountMenu>
              <hlm-dropdown-menu class="min-w-44" aria-label="Account">
                <div hlmDropdownMenuLabel>{{ auth.profileName() || 'Account' }}</div>
                <div hlmDropdownMenuSeparator></div>
                <button hlmDropdownMenuItem (triggered)="logout()">Sign out</button>
              </hlm-dropdown-menu>
            </ng-template>
          }
        </header>
        <div
          class="mx-auto flex w-full flex-1 flex-col gap-6 p-4 md:p-8"
          [class.max-w-6xl]="group() !== 'Incidents'"
          [class.max-w-7xl]="group() === 'Incidents'"
          [class.items-center]="auth.state() !== 'signed-in'"
          [class.justify-center]="auth.state() !== 'signed-in'"
        >
          @if (auth.state() === 'signed-in') {
            <div class="space-y-2">
              <div class="flex items-center gap-3">
                <h1 class="text-2xl font-semibold tracking-tight">{{ group() }}</h1>
                <span hlmBadge variant="secondary">{{
                  group() === 'Dashboard'
                    ? 'Overview'
                    : group() === 'Ontology'
                      ? 'Model reference'
                      : 'Administration'
                }}</span>
              </div>
              <p class="max-w-2xl text-sm text-muted-foreground">{{ description() }}</p>
            </div>
          }
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
            <section hlmCard class="w-full max-w-lg p-6" aria-label="Sign in">
              <h2 hlmCardTitle>
                {{
                  auth.state() === 'expired' ? 'Your session has expired' : 'Sign in to continue'
                }}
              </h2>
              <p>Your workspace requires an authorized Fault Code account.</p>
              @if (auth.demoAvailable()) {
                <form class="flex flex-col gap-4" (ngSubmit)="loginDemo()">
                  <div hlmField>
                    <label hlmFieldLabel for="demo-username">Username</label
                    ><input
                      hlmInput
                      id="demo-username"
                      name="username"
                      autocomplete="username"
                      required
                      maxlength="64"
                      [(ngModel)]="demoUsername"
                    />
                  </div>
                  <div hlmField>
                    <label hlmFieldLabel for="demo-password">Password</label
                    ><input
                      hlmInput
                      id="demo-password"
                      name="password"
                      type="password"
                      autocomplete="current-password"
                      required
                      maxlength="256"
                      [(ngModel)]="demoPassword"
                    />
                  </div>
                  @if (demoLoginError()) {
                    <p role="alert" class="text-sm text-destructive">{{ demoLoginError() }}</p>
                  }
                  <div class="grid grid-cols-2 gap-2">
                    <button
                      hlmBtn
                      type="submit"
                      class="h-auto min-h-8 w-full whitespace-normal py-2"
                      [disabled]="demoLoginBusy() || !demoUsername || !demoPassword"
                    >
                      Sign in with credentials
                    </button>
                    <button
                      hlmBtn
                      type="button"
                      variant="outline"
                      class="h-auto min-h-8 w-full whitespace-normal py-2"
                      [disabled]="demoLoginBusy()"
                      (click)="login()"
                    >
                      Sign in with Microsoft
                    </button>
                  </div>
                </form>
              } @else {
                <button hlmBtn class="w-fit" (click)="login()">Sign in with Microsoft</button>
              }
            </section>
          } @else {
            @if (group() === 'Tickets') {
              <app-tickets />
            } @else if (group() === 'Dashboard') {
              <section hlmCard class="gap-5 p-6" aria-label="Agentic fault flow">
                <div class="space-y-2">
                  <span hlmBadge variant="outline" class="border-red-600"
                    >Agentic flow · Shriram</span
                  >
                  <h2 hlmCardTitle>From a vehicle signal to driver guidance</h2>
                  <p class="max-w-3xl text-sm leading-relaxed text-muted-foreground">
                    A fault code starts a traceable investigation. The agent coordinates scoped
                    tools; the service verifies the evidence, controls record changes and records
                    delivery.
                  </p>
                </div>
                <div role="group" aria-label="Fault investigation flow diagram" class="space-y-4">
                  <ol class="grid gap-8 md:grid-cols-4" aria-label="Fault processing steps">
                    @for (step of dashboardFlow; track step.title; let last = $last) {
                      <li class="relative">
                        <div hlmCard class="h-full gap-3 bg-muted/30 p-4">
                          <div class="flex items-center justify-between">
                            <ng-icon [name]="step.icon" class="size-5" aria-hidden="true" />
                            <span class="text-xs text-muted-foreground">{{ step.number }}</span>
                          </div>
                          <h3 class="text-sm font-semibold">{{ step.title }}</h3>
                          <p class="text-xs leading-relaxed text-muted-foreground">
                            {{ step.detail }}
                          </p>
                        </div>
                        @if (!last) {
                          <ng-icon
                            name="lucideArrowRight"
                            aria-hidden="true"
                            class="absolute -bottom-6 left-1/2 size-4 -translate-x-1/2 rotate-90 text-muted-foreground md:-right-6 md:bottom-auto md:left-auto md:top-1/2 md:translate-x-0 md:-translate-y-1/2 md:rotate-0"
                          />
                        }
                      </li>
                    }
                  </ol>
                  <div class="flex flex-col items-center gap-2 py-1" aria-hidden="true">
                    <ng-icon name="lucideArrowDown" class="size-4 text-muted-foreground" />
                    <span hlmBadge variant="secondary">Is the code verified?</span>
                  </div>
                  <div class="grid gap-4 md:grid-cols-2">
                    <section hlmCard class="gap-2 p-4" aria-label="Known fault path">
                      <span hlmBadge variant="secondary" class="w-fit">Known fault</span>
                      <h3 class="text-sm font-semibold">Explain the verified meaning</h3>
                      <p class="text-sm leading-relaxed text-muted-foreground">
                        Use approved fault knowledge and applicable remedy guidance. The agent
                        prepares an evidence-grounded email; the service validates it before
                        sending.
                      </p>
                    </section>
                    <section hlmCard class="gap-2 p-4" aria-label="Unknown fault path">
                      <span hlmBadge variant="outline" class="w-fit">Unknown fault</span>
                      <h3 class="text-sm font-semibold">Investigate, then follow up</h3>
                      <p class="text-sm leading-relaxed text-muted-foreground">
                        Retain the incident and tell the driver it is being investigated. Resolving
                        the incident triggers a follow-up notification with the available verified
                        facts.
                      </p>
                    </section>
                  </div>
                  <div class="flex justify-center" aria-hidden="true">
                    <ng-icon name="lucideArrowDown" class="size-4 text-muted-foreground" />
                  </div>
                  <div hlmCard class="flex-row items-start gap-3 bg-muted/30 p-4">
                    <ng-icon name="lucideMail" class="mt-0.5 size-5 shrink-0" aria-hidden="true" />
                    <div class="space-y-1">
                      <h3 class="text-sm font-semibold">Driver notification + delivery history</h3>
                      <p class="text-sm leading-relaxed text-muted-foreground">
                        Mailpit captures the email. The notification keeps its message, status and
                        delivery attempts linked to the driver and incident.
                      </p>
                    </div>
                  </div>
                </div>
                <div class="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
                  <p class="max-w-2xl text-xs leading-relaxed text-muted-foreground">
                    Workflow traceability: incident, lookup, agent tools and email delivery. Datadog
                    is the telemetry destination; this diagram is a workflow reference, not a live
                    ingestion status.
                  </p>
                  <a hlmBtn variant="outline" routerLink="/admin/vehicles"
                    >Try vehicle dispatch
                    <ng-icon name="lucideArrowUpRight" class="ml-2 size-4" aria-hidden="true"
                  /></a>
                </div>
              </section>
              <div class="grid gap-6 lg:grid-cols-2">
                <section hlmCard class="gap-4 p-6" aria-label="OpenSpec overview">
                  <div class="space-y-2">
                    <span hlmBadge variant="outline" class="border-red-600"
                      >OpenSpec · Kristof</span
                    >
                    <h2 hlmCardTitle>Specify the behavior before changing it</h2>
                    <p class="text-sm leading-relaxed text-muted-foreground">
                      OpenSpec keeps the intended behavior and acceptance scenarios alongside the
                      application code. A change captures its proposal, specifications, design and
                      implementation tasks so the work can be checked against explicit requirements.
                    </p>
                  </div>
                  <ol class="space-y-2 text-sm">
                    <li>
                      <span class="font-medium">Propose</span
                      ><span class="text-muted-foreground"> — describe the problem and scope.</span>
                    </li>
                    <li>
                      <span class="font-medium">Specify</span
                      ><span class="text-muted-foreground">
                        — define requirements and acceptance scenarios.</span
                      >
                    </li>
                    <li>
                      <span class="font-medium">Implement and validate</span
                      ><span class="text-muted-foreground">
                        — follow tasks and verify the behavior.</span
                      >
                    </li>
                    <li>
                      <span class="font-medium">Archive</span
                      ><span class="text-muted-foreground">
                        — reconcile completed changes into the lasting specifications.</span
                      >
                    </li>
                  </ol>
                  <p class="text-sm leading-relaxed text-muted-foreground">
                    <span class="font-medium text-foreground">Playwright</span> checks the specified
                    user journeys in the browser, including fault dispatch, incident resolution,
                    navigation and accessibility.
                  </p>
                  <p class="text-xs text-muted-foreground">
                    Package: <code>&#64;fission-ai/openspec</code>
                  </p>
                  <a
                    hlmBtn
                    variant="outline"
                    class="mt-auto w-fit"
                    href="https://github.com/Fission-AI/OpenSpec"
                    target="_blank"
                    rel="noopener"
                    >OpenSpec package
                    <ng-icon name="lucideArrowUpRight" class="ml-2 size-4" aria-hidden="true"
                  /></a>
                </section>
                <section hlmCard class="gap-4 p-6" aria-label="Ontology overview">
                  <div class="space-y-2">
                    <span hlmBadge variant="outline" class="border-red-600">Ontology · Nathan</span>
                    <h2 hlmCardTitle>A shared model of the fault domain</h2>
                    <p class="text-sm leading-relaxed text-muted-foreground">
                      The ontology names the concepts and relationships that give fault knowledge
                      its meaning. A fault code is reported in an incident; the incident belongs to
                      a vehicle, involves a driver and has notifications. Symptoms, classifications
                      and remedies add context for investigation.
                    </p>
                  </div>
                  <div
                    class="flex flex-wrap items-center gap-2"
                    aria-label="Core ontology concepts"
                  >
                    @for (
                      concept of ['Fault code', 'Incident', 'Vehicle', 'Driver', 'Notification'];
                      track concept
                    ) {
                      <span hlmBadge variant="secondary">{{ concept }}</span>
                    }
                  </div>
                  <p class="text-sm leading-relaxed text-muted-foreground">
                    The ontology describes how concepts relate. SQL Server stores the actual
                    records; Qdrant supports lookup and retrieval. Similarity alone never
                    establishes a fault diagnosis.
                  </p>
                  <a hlmBtn variant="outline" class="mt-auto w-fit" routerLink="/admin/ontology"
                    >Explore ontology
                    <ng-icon name="lucideArrowUpRight" class="ml-2 size-4" aria-hidden="true"
                  /></a>
                </section>
              </div>
              <section
                hlmCard
                id="development-stack"
                class="scroll-mt-6 gap-4 p-6"
                aria-label="Development stack"
              >
                <div class="space-y-2">
                  <h2 hlmCardTitle>Development stack</h2>
                  <p class="text-sm text-muted-foreground">
                    The tools and services used to build and run Fault Code.
                  </p>
                </div>
                <dl class="grid gap-5 sm:grid-cols-2">
                  @for (layer of developmentStack; track layer.name) {
                    <div class="space-y-1 border-l-2 border-border pl-4">
                      <dt class="text-sm font-semibold">{{ layer.name }}</dt>
                      <dd class="text-sm leading-relaxed text-muted-foreground">
                        {{ layer.detail }}
                      </dd>
                    </div>
                  }
                </dl>
              </section>
            } @else if (group() === 'Vehicles' || group() === 'Drivers') {
              <section hlmCard class="gap-4 p-6" [attr.aria-label]="group() + ' list'">
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <h2 hlmCardTitle>
                    {{ fleetIsVehicles() ? 'Vehicle fleet' : 'Driver directory' }}
                  </h2>
                  <div class="flex gap-2">
                    <button
                      hlmBtn
                      variant="outline"
                      [disabled]="busy() || permissionsLoading()"
                      (click)="reloadFleet()"
                    >
                      Refresh
                    </button>
                  </div>
                </div>
                @if (busy() || permissionsLoading()) {
                  <p role="status">Loading {{ group().toLowerCase() }}…</p>
                } @else if (error()) {
                  <div hlmAlert variant="destructive" role="alert">{{ message() }}</div>
                } @else if (fleetRows().length === 0) {
                  <p>No {{ group().toLowerCase() }} recorded.</p>
                } @else {
                  <div class="overflow-x-auto">
                    <table hlmTable class="text-left" [attr.aria-label]="group() + ' records'">
                      <thead hlmTHead>
                        <tr hlmTr>
                          @if (fleetIsVehicles()) {
                            <th hlmTh>Vehicle</th>
                            <th hlmTh>System</th>
                          }
                          <th hlmTh>{{ fleetIsVehicles() ? 'Driver' : 'Name' }}</th>
                          <th hlmTh>Email</th>
                          @if (!fleetIsVehicles()) {
                            <th hlmTh>Notifications</th>
                          }
                          @if (fleetIsVehicles()) {
                            <th hlmTh>Status</th>
                            <th hlmTh>Fault code</th>
                            <th hlmTh>Actions</th>
                            <th hlmTh>Incidents</th>
                          }
                        </tr>
                      </thead>
                      <tbody hlmTBody>
                        @for (row of fleetRows(); track row.id) {
                          <tr hlmTr>
                            @if (fleetIsVehicles()) {
                              <td hlmTd>{{ vehicleName(row) }}</td>
                              <td hlmTd>{{ row.system }}</td>
                            }
                            <td hlmTd>
                              @if (fleetIsVehicles()) {
                                @if (row.associationError) {
                                  <span>Driver associations unavailable</span>
                                } @else if (!row.drivers.length) {
                                  <span>No active drivers</span>
                                }
                                @for (driver of row.drivers; track driver.id) {
                                  <div>{{ driverName(driver) }}</div>
                                }
                              } @else {
                                {{ driverName(row) }}
                              }
                            </td>
                            <td hlmTd>
                              @if (fleetIsVehicles()) {
                                @if (row.associationError) {
                                  <span>Unavailable</span>
                                } @else if (!row.drivers.length) {
                                  <span>—</span>
                                }
                                @for (driver of row.drivers; track driver.id) {
                                  <div>{{ driver.email || 'No email recorded' }}</div>
                                }
                              } @else {
                                {{ row.email || 'No email recorded' }}
                              }
                            </td>
                            @if (!fleetIsVehicles()) {
                              <td hlmTd>
                                <a
                                  hlmBtn
                                  variant="outline"
                                  routerLink="/admin/notifications"
                                  [queryParams]="{ driverId: row.id }"
                                  >View notifications</a
                                >
                              </td>
                            }
                            @if (fleetIsVehicles()) {
                              <td hlmTd>{{ row.active ? 'Active' : 'Inactive' }}</td>
                              <td hlmTd>
                                <label class="sr-only" [for]="'fault-code-' + row.id"
                                  >Fault code for {{ vehicleName(row) }} —
                                  {{ driverName(row.drivers[0] || {}) }}</label
                                >
                                <hlm-select
                                  [value]="selectedFaults()[row.id] || ''"
                                  (valueChange)="selectVehicleFault(row.id, $event ?? '')"
                                  [disabled]="
                                    dispatching()[row.id] || !row.active || !canDispatch()
                                  "
                                >
                                  <hlm-select-trigger
                                    [buttonId]="'fault-code-' + row.id"
                                    class="w-full min-w-52"
                                  >
                                    <hlm-select-value placeholder="Select fault code" />
                                  </hlm-select-trigger>
                                  <hlm-select-content
                                    *hlmSelectPortal
                                    role="region"
                                    aria-label="Fault code choices"
                                  >
                                    @for (fault of vehicleFaultCodes(row); track fault.code) {
                                      <hlm-select-item [value]="fault.code">
                                        {{ fault.code }} —
                                        {{ fault.unlisted ? 'Unknown' : 'Catalog' }}
                                      </hlm-select-item>
                                    }
                                  </hlm-select-content>
                                </hlm-select>
                              </td>
                              <td hlmTd>
                                <button
                                  hlmBtn
                                  variant="outline"
                                  [disabled]="
                                    !selectedFaults()[row.id] ||
                                    dispatching()[row.id] ||
                                    !row.active ||
                                    !canDispatch() ||
                                    row.associationError ||
                                    !row.drivers.length
                                  "
                                  (click)="dispatchFault(row)"
                                >
                                  {{ dispatching()[row.id] ? 'Dispatching…' : 'Dispatch' }}
                                </button>
                              </td>
                              <td hlmTd>
                                @if (
                                  incidentAllowed() && (vehicleIncidentCounts()[row.id] ?? 0) > 0
                                ) {
                                  <a
                                    hlmBtn
                                    variant="outline"
                                    size="sm"
                                    routerLink="/admin/incidents"
                                    [queryParams]="{ vehicleId: row.id }"
                                    [attr.aria-label]="
                                      'View ' +
                                      vehicleIncidentCounts()[row.id] +
                                      ' incidents for ' +
                                      vehicleName(row) +
                                      ' — ' +
                                      driverName(row.drivers[0] || {})
                                    "
                                  >
                                    View incidents
                                    <span hlmBadge variant="secondary">{{
                                      vehicleIncidentCounts()[row.id]
                                    }}</span>
                                  </a>
                                } @else {
                                  <button
                                    hlmBtn
                                    variant="outline"
                                    size="sm"
                                    disabled
                                    [attr.aria-label]="
                                      'View incidents for ' +
                                      vehicleName(row) +
                                      ' — ' +
                                      driverName(row.drivers[0] || {})
                                    "
                                  >
                                    View incidents
                                    @if (vehicleIncidentCounts()[row.id] === 0) {
                                      <span hlmBadge variant="secondary">0</span>
                                    }
                                  </button>
                                }
                              </td>
                            }
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
                <div class="flex items-center gap-3">
                  <button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || fleetPage() <= 1"
                    (click)="reloadFleet(fleetPage() - 1)"
                  >
                    Previous</button
                  ><span>Page {{ fleetPage() }} · {{ fleetTotal() }} records</span
                  ><button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || fleetPage() * 20 >= fleetTotal()"
                    (click)="reloadFleet(fleetPage() + 1)"
                  >
                    Next
                  </button>
                </div>
              </section>
            } @else if (group() === 'Incidents' && !incidentReview()) {
              <section hlmCard class="gap-4 p-6" aria-label="Vehicle incidents">
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <div class="space-y-2">
                    <h2 hlmCardTitle>Incidents</h2>
                    <p hlmCardDescription>Incidents created from vehicle fault reports.</p>
                    @if (incidentVehicleFilter()) {
                      <div class="flex flex-wrap items-center gap-2">
                        <span hlmBadge variant="secondary">Filtered by vehicle</span>
                        <button hlmBtn variant="outline" size="sm" routerLink="/admin/incidents">
                          Clear filter
                        </button>
                      </div>
                    }
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
                <ng-container>
                  <div hlmTableContainer>
                    <table hlmTable aria-label="Incidents records">
                      <caption hlmCaption>
                        @if (hasPage()) {
                          {{ data().totalCount }} incidents · page {{ data().page }}
                        } @else {
                          Vehicle-reported incidents
                        }
                      </caption>
                      <thead hlmTHead>
                        <tr hlmTr>
                          <th hlmTh scope="col">Fault code</th>
                          <th hlmTh scope="col">Vehicle</th>
                          <th hlmTh scope="col">Driver</th>
                          <th hlmTh scope="col">System</th>
                          <th hlmTh scope="col">Reported (UTC)</th>
                          <th hlmTh scope="col">Status</th>
                          <th hlmTh scope="col">Actions</th>
                        </tr>
                      </thead>
                      <tbody hlmTBody>
                        @for (incident of data()?.items ?? []; track incident.id) {
                          <tr hlmTr>
                            <th hlmTh scope="row" class="text-foreground">{{ incident.code }}</th>
                            <td hlmTd>
                              <a
                                hlmBtn
                                variant="link"
                                routerLink="/admin/vehicles"
                                [queryParams]="{ vehicleId: incident.fleet.vehicleId }"
                                >{{
                                  vehicleName({ identifier: incident.fleet.vehicleIdentifier })
                                }}</a
                              >
                            </td>
                            <td hlmTd>
                              <a
                                hlmBtn
                                variant="link"
                                routerLink="/admin/drivers"
                                [queryParams]="{ driverId: incident.fleet.driverId }"
                                >{{ incident.fleet.driverIdentifier }}</a
                              >
                            </td>
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
                              <a
                                hlmBtn
                                variant="outline"
                                size="sm"
                                routerLink="/admin/notifications"
                                [queryParams]="{ incidentId: incident.id }"
                                >Notifications</a
                              >
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
                      (click)="reloadIncidents((data()?.page ?? 1) - 1)"
                      [disabled]="!hasPage() || busy() || (data()?.page ?? 1) <= 1"
                    >
                      Previous</button
                    ><button
                      hlmBtn
                      variant="outline"
                      (click)="reloadIncidents((data()?.page ?? 1) + 1)"
                      [disabled]="
                        !hasPage() ||
                        busy() ||
                        (data()?.page ?? 1) * (data()?.pageSize ?? 20) >= (data()?.totalCount ?? 0)
                      "
                    >
                      Next
                    </button>
                  </div>
                </ng-container>
              </section>
            } @else if (group() === 'Notifications') {
              <section hlmCard class="gap-4 p-6" aria-label="Driver notifications list">
                <div class="flex items-center justify-between">
                  <h2 class="font-semibold">Notifications</h2>
                  <button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy()"
                    (click)="reloadNotifications()"
                  >
                    Refresh
                  </button>
                </div>
                @if (notificationDriver() || incidentFilter()) {
                  <a
                    hlmBtn
                    variant="outline"
                    class="w-fit self-start"
                    routerLink="/admin/notifications"
                    >Show all notifications</a
                  >
                }
                @if (busy()) {
                  <p role="status">Loading notifications…</p>
                } @else if (error()) {
                  <p role="alert">{{ message() }}</p>
                } @else if (!notificationRows().length) {
                  <p>No driver notifications recorded.</p>
                } @else {
                  <div class="overflow-x-auto">
                    <table hlmTable class="text-left" aria-label="Driver notifications records">
                      <thead hlmTHead>
                        <tr hlmTr>
                          <th hlmTh>Driver</th>
                          <th hlmTh>Email</th>
                          <th hlmTh>Message</th>
                          <th hlmTh>Status</th>
                          <th hlmTh>Created</th>
                          <th hlmTh>Attempts</th>
                        </tr>
                      </thead>
                      <tbody hlmTBody>
                        @for (row of notificationRows(); track row.id) {
                          <tr hlmTr>
                            <td hlmTd>{{ row.driverName }}</td>
                            <td hlmTd>{{ row.email }}</td>
                            <td hlmTd>
                              <hlm-dialog>
                                <button
                                  hlmBtn
                                  variant="outline"
                                  size="sm"
                                  hlmDialogTrigger
                                  [disabled]="!row.message"
                                >
                                  View message
                                </button>
                                <hlm-dialog-content *hlmDialogPortal class="max-w-2xl">
                                  <hlm-dialog-header
                                    ><h3 hlmDialogTitle>Driver email</h3>
                                    <p hlmDialogDescription>
                                      {{ row.driverName }} · {{ row.email }}
                                    </p></hlm-dialog-header
                                  >
                                  <p
                                    class="max-h-[60vh] overflow-y-auto whitespace-pre-wrap text-sm"
                                  >
                                    {{ row.message }}
                                  </p>
                                </hlm-dialog-content>
                              </hlm-dialog>
                              @if (!row.message && canDispatch()) {
                                <button
                                  hlmBtn
                                  variant="outline"
                                  size="sm"
                                  [disabled]="processingNotifications()[row.id]"
                                  (click)="processNotification(row)"
                                >
                                  Process email
                                </button>
                              }
                            </td>
                            <td hlmTd>{{ row.state }}</td>
                            <td hlmTd>{{ row.createdAt | date: 'medium' }}</td>
                            <td hlmTd>{{ row.attemptCount ?? 0 }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
                <div class="flex items-center gap-3">
                  <button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || notificationPage() <= 1"
                    (click)="reloadNotifications(notificationPage() - 1)"
                  >
                    Previous</button
                  ><span>Page {{ notificationPage() }} · {{ notificationTotal() }} records</span
                  ><button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || notificationPage() * 20 >= notificationTotal()"
                    (click)="reloadNotifications(notificationPage() + 1)"
                  >
                    Next
                  </button>
                </div>
              </section>
            } @else if (group() === 'Token') {
              <section hlmCard class="gap-4 p-6" aria-label="API token">
                <div class="flex items-center justify-between gap-3">
                  <h2 hlmCardTitle>API token</h2>
                  <button
                    hlmBtn
                    variant="outline"
                    [disabled]="tokenBusy()"
                    (click)="loadToken(true)"
                  >
                    Refresh token
                  </button>
                </div>
                <p class="text-sm text-muted-foreground">
                  {{
                    auth.kind() === 'demo'
                      ? 'Entra application token for Fault Code Demo API.'
                      : 'Your Entra access token for the Fault Code API.'
                  }}
                </p>
                <p class="text-sm text-muted-foreground">
                  200 API requests per day per identity. Resets at midnight UTC. Refreshing the
                  token does not reset usage.
                </p>
                @if (tokenBusy()) {
                  <p role="status">Loading token…</p>
                }
                @if (tokenError()) {
                  <p role="alert">{{ tokenError() }}</p>
                }
                @if (accessToken()) {
                  <div class="flex w-full items-center gap-2 md:w-1/2">
                    <input
                      hlmInput
                      aria-label="Entra API access token"
                      readonly
                      class="min-w-0 flex-1 font-mono text-xs"
                      [value]="accessToken()"
                    />
                    <button
                      hlmBtn
                      variant="outline"
                      size="icon-sm"
                      aria-label="Copy Entra API access token"
                      (click)="copyAccessToken()"
                    >
                      <ng-icon name="lucideCopy" />
                    </button>
                  </div>
                  <p class="text-sm text-muted-foreground">Identity: {{ tokenIdentity() }}</p>
                  @if (tokenExpiresAt()) {
                    <p class="text-sm text-muted-foreground">
                      Expires {{ tokenExpiresAt() | date: 'medium' }}
                    </p>
                  }
                }
              </section>
            } @else if (group() === 'Users' && auth.kind() === 'demo') {
              <section hlmCard class="p-6">
                <p>This account cannot access user administration.</p>
              </section>
            } @else if (group() === 'Users') {
              <section hlmCard class="gap-4 p-6" aria-label="Users list">
                <div class="flex items-center justify-between">
                  <h2 class="font-semibold">Users</h2>
                  <button hlmBtn variant="outline" [disabled]="busy()" (click)="reloadUsers()">
                    Refresh
                  </button>
                </div>
                @if (busy()) {
                  <p role="status">Loading users…</p>
                }
                @if (error()) {
                  <p role="alert">{{ message() }}</p>
                }
                <div hlmTableContainer>
                  <table hlmTable class="text-left" aria-label="Users records">
                    <thead hlmTHead>
                      <tr hlmTr>
                        <th hlmTh>Name</th>
                        <th hlmTh>Roles</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (user of userRows(); track user.id) {
                        <tr hlmTr>
                          <td hlmTd>{{ user.displayName }}</td>
                          <td hlmTd>
                            @for (role of user.roles; track role) {
                              <span hlmBadge variant="secondary" class="mr-1">{{ role }}</span>
                            } @empty {
                              No roles assigned
                            }
                          </td>
                        </tr>
                      } @empty {
                        @if (!busy() && !error()) {
                          <tr hlmTr>
                            <td hlmTd colspan="2">No users recorded.</td>
                          </tr>
                        }
                      }
                    </tbody>
                  </table>
                </div>
                <div class="flex items-center gap-3">
                  <button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || error() || userPage() <= 1"
                    (click)="reloadUsers(userPage() - 1)"
                  >
                    Previous</button
                  ><span>
                    @if (!error()) {
                      Page {{ userPage() }} · {{ userTotal() }} records
                    }</span
                  ><button
                    hlmBtn
                    variant="outline"
                    [disabled]="busy() || error() || userPage() * 20 >= userTotal()"
                    (click)="reloadUsers(userPage() + 1)"
                  >
                    Next
                  </button>
                </div>
              </section>
            } @else if (group() === 'Fault codes') {
              <section hlmCard class="gap-4 p-6" aria-label="Fault codes">
                <h2 hlmCardTitle>Fault codes</h2>
                <p hlmCardDescription>{{ faultCodeScope() }}</p>
                <div hlmTableContainer>
                  <table hlmTable>
                    <thead hlmTHead>
                      <tr hlmTr>
                        <th hlmTh>Code</th>
                        <th hlmTh>System</th>
                        <th hlmTh>Vehicles</th>
                        <th hlmTh>Meaning</th>
                      </tr>
                    </thead>
                    <tbody hlmTBody>
                      @for (row of faultCodeRows(); track row.code) {
                        <tr hlmTr>
                          <td hlmTd>{{ row.code }}</td>
                          <td hlmTd>{{ row.system }}</td>
                          <td hlmTd>{{ row.vehicles.join(', ') }}</td>
                          <td hlmTd class="whitespace-normal">{{ row.description }}</td>
                        </tr>
                      } @empty {
                        <tr hlmTr>
                          <td hlmTd colspan="4">No fault codes.</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </section>
            } @else if (group() === 'Ontology') {
              <section hlmCard class="gap-4 p-6" aria-label="Fault code ontology">
                <div class="space-y-2">
                  <div class="flex flex-wrap items-center justify-between gap-3">
                    <h2 hlmCardTitle>Model relationships</h2>
                  </div>
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
                @if (ontologyError()) {
                  <p role="alert">{{ ontologyError() }}</p>
                }
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
                        </tr>
                      } @empty {
                        <tr hlmTr>
                          <td hlmTd colspan="4" class="py-8 text-center text-muted-foreground">
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
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <h2 hlmCardTitle>
                    {{ group() === 'Incidents' ? 'Review incident' : 'Workspace actions' }}
                  </h2>
                  @if (op()?.id === 'GetIncident' && incidentDatadogUrl(); as datadogUrl) {
                    <a
                      hlmBtn
                      variant="outline"
                      [href]="datadogUrl"
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      View in Datadog <ng-icon name="lucideArrowUpRight" aria-hidden="true" />
                    </a>
                  } @else if (op()?.id === 'GetIncident') {
                    <button
                      hlmBtn
                      variant="outline"
                      disabled
                      title="The Datadog incident has not been linked yet."
                    >
                      View in Datadog <ng-icon name="lucideArrowUpRight" aria-hidden="true" />
                    </button>
                  }
                </div>
                @if (op()?.id !== 'GetIncident') {
                  <div hlmField class="w-full" [class.md:w-1/2]="group() === 'Incidents'">
                    <label hlmFieldLabel for="action">Action</label>
                    <hlm-select
                      [itemToString]="actionLabel"
                      [value]="op()?.id ?? ''"
                      (valueChange)="select($event ?? '')"
                    >
                      <hlm-select-trigger buttonId="action" class="w-full">
                        <hlm-select-value placeholder="Choose action" />
                      </hlm-select-trigger>
                      <hlm-select-content
                        *hlmSelectPortal
                        role="region"
                        aria-label="Action choices"
                      >
                        @for (action of actions(); track action.id) {
                          <hlm-select-item [value]="action.id">{{ action.label }}</hlm-select-item>
                        }
                      </hlm-select-content>
                    </hlm-select>
                  </div>
                }
                @if (permissionsLoading()) {
                  <p role="status">Checking permissions…</p>
                } @else if (!op() || !allowed(op()!)) {
                  <div role="alert" hlmAlert variant="destructive">
                    Access denied. This action requires additional permissions.
                  </div>
                } @else if (op()?.id === 'GetIncident') {
                  @if (canResolveIncident()) {
                    <form class="grid w-full gap-4 md:w-1/2" (ngSubmit)="resolveIncident()">
                      <div hlmField>
                        <label hlmFieldLabel for="incident-resolution">Resolution *</label>
                        <textarea
                          hlmTextarea
                          id="incident-resolution"
                          name="resolution"
                          rows="4"
                          maxlength="2000"
                          required
                          [(ngModel)]="resolutionNotes"
                          [disabled]="busy() || data()?.status === 'Resolved'"
                          placeholder="Record the investigation findings and recommended next step."
                        ></textarea>
                      </div>
                      <button
                        hlmBtn
                        type="submit"
                        class="w-fit"
                        [disabled]="
                          busy() ||
                          !data()?.id ||
                          data()?.status === 'Resolved' ||
                          !resolutionNotes.trim()
                        "
                      >
                        {{ busy() ? 'Resolving…' : 'Resolve incident' }}
                      </button>
                    </form>
                  }
                } @else if (op(); as action) {
                  <form
                    (ngSubmit)="execute()"
                    class="grid w-full max-w-3xl gap-5"
                    [class.md:w-1/2]="group() === 'Incidents'"
                  >
                    <app-fields [schema]="parameterSchema()" [model]="parameters" prefix="params" />
                    @if (action.body) {
                      <app-fields [schema]="bodyFields(action)" [model]="body" prefix="body" />
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
                              ? action.id === 'GetIncident'
                                ? 'Refresh incident'
                                : 'Load records'
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
      </main>
    </div>`,
})
export class Workspace {
  auth = inject(Auth);
  accountInitials = computed(() =>
    (this.auth.profileName() || 'Account')
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase(),
  );
  private api = inject(Api);
  areas = areas;
  incidentReview = signal(false);
  resolutionNotes = '';
  private resolutionAttempt: { fingerprint: string; key: string } | null = null;
  faultCodeRows = signal<any[]>([]);
  faultCodeScope = signal('');
  ontologyRows = signal<any[]>([]);
  ontologyError = signal('');
  ontologyQuery = signal('');
  filteredRelationships = computed(() => {
    const query = this.ontologyQuery().trim().toLowerCase();
    return this.ontologyRows().filter((row) =>
      [row.from, row.relationship, row.to, row.detail].some((value) =>
        value.toLowerCase().includes(query),
      ),
    );
  });
  readonly developmentStack = [
    {
      name: 'Frontend',
      detail: 'Angular 22 and TypeScript, Spartan UI, Tailwind CSS and Lucide icons.',
    },
    {
      name: 'API',
      detail: 'ASP.NET Core on .NET 10 with C# and versioned REST resource endpoints.',
    },
    {
      name: 'Identity',
      detail:
        'Microsoft Entra for API bearer tokens and Microsoft sign-in; server-verified credentials and an HttpOnly session for John Henry.',
    },
    {
      name: 'Agent and tools',
      detail:
        'Microsoft Agent Framework with OpenAI and a permission-aware MCP server for fault lookup and driver email.',
    },
    {
      name: 'Persistence and retrieval',
      detail:
        'SQL Server with Entity Framework Core for records; Qdrant and OpenAI embeddings for fault knowledge and resolution retrieval.',
    },
    {
      name: 'Email and telemetry',
      detail:
        'Mailpit captures driver emails locally and on Linux. OpenTelemetry supplies the Datadog telemetry pipeline.',
    },
    {
      name: 'Specifications and API reference',
      detail:
        'OpenSpec requirements and scenarios; generated OpenAPI, Mintlify documentation and Swagger UI for API testing.',
    },
    {
      name: 'Development and verification',
      detail: 'Docker Compose on Linux, xUnit backend checks and Playwright browser checks.',
    },
  ];
  readonly dashboardFlow = [
    {
      number: '01',
      icon: 'lucideCar',
      title: 'Vehicle reports a code',
      detail: 'A Silverado or Sierra sends its vehicle ID and fault code.',
    },
    {
      number: '02',
      icon: 'lucideDatabase',
      title: 'API records the event',
      detail:
        'Validate the request. Persist the incident and linked driver notification in SQL Server.',
    },
    {
      number: '03',
      icon: 'lucideBot',
      title: 'Agent uses MCP tools',
      detail: 'Microsoft Agent Framework coordinates authorized fault lookup and email tools.',
    },
    {
      number: '04',
      icon: 'lucideNetwork',
      title: 'Verify fault knowledge',
      detail: 'Exact Qdrant code matching is checked against approved knowledge and applicability.',
    },
  ];
  icons = [
    'lucideLayoutDashboard',
    'lucideCar',
    'lucideUsers',
    'lucideTriangleAlert',
    'lucideBell',
    'lucideUsers',
    'lucideNetwork',
    'lucideWrench',
    'lucideKeyRound',
    'lucideFiles',
  ];
  incidentFilter = signal('');
  incidentVehicleFilter = signal('');
  fleetFilter = signal('');
  userRows = signal<any[]>([]);
  ownAccount = signal<any>(null);
  accessToken = signal('');
  tokenBusy = signal(false);
  tokenError = signal('');
  tokenExpiresAt = signal<string | null>(null);
  tokenIdentity = signal('');
  processingNotifications = signal<Record<string, boolean>>({});
  visibleAreas = computed(() =>
    this.auth.kind() === 'demo' ? areas.filter((a) => a[1] !== 'users') : areas,
  );
  demoUsername = '';
  demoPassword = '';
  demoLoginBusy = signal(false);
  demoLoginError = signal('');
  async loginDemo() {
    this.demoLoginBusy.set(true);
    this.demoLoginError.set('');
    try {
      await this.auth.loginDemo(this.demoUsername, this.demoPassword);
    } catch (e) {
      this.demoLoginError.set(e instanceof Error ? e.message : 'Sign-in failed.');
    } finally {
      this.demoPassword = '';
      this.demoLoginBusy.set(false);
    }
  }
  documentationUrl =
    location.hostname === 'localhost' || location.hostname === '127.0.0.1'
      ? 'http://localhost:5083'
      : '/docs/';
  apiTestingUrl =
    location.hostname === 'localhost' || location.hostname === '127.0.0.1'
      ? 'http://127.0.0.1:5080/swagger/index.html'
      : '/swagger/index.html';
  userPage = signal(1);
  userTotal = signal(0);
  notificationRows = signal<any[]>([]);
  notificationPage = signal(1);
  notificationTotal = signal(0);
  notificationDriver = signal('');
  selectedFaults = signal<Record<string, string>>({});
  dispatching = signal<Record<string, boolean>>({});
  private dispatchKeys = new Map<string, { code: string; key: string }>();
  vehicleIncidentCounts = signal<Record<string, number | undefined>>({});
  fleetRows = signal<any[]>([]);
  fleetPage = signal(1);
  fleetTotal = signal(0);
  fleetIsVehicles = computed(() => this.group() === 'Vehicles');
  private route = inject(ActivatedRoute);
  private routeArea = signal('incidents');
  @Input() set area(value: string) {
    this.routeArea.set(value ?? 'incidents');
  }
  group = computed(() => areas.find((a) => a[1] === this.routeArea())?.[0] ?? 'Incidents');
  actions = computed(() =>
    this.group() === 'Vehicles'
      ? ops.filter((o) => o.id === 'CreateIncident')
      : this.group() === 'Drivers'
        ? []
        : ops.filter((o) => o.group === this.group() && o.id !== 'CreateIncident'),
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
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.notificationDriver.set(params.get('driverId') ?? '');
      this.incidentVehicleFilter.set(params.get('vehicleId') ?? '');
      this.incidentFilter.set(params.get('incidentId') ?? '');
      this.fleetFilter.set(params.get('vehicleId') ?? params.get('driverId') ?? '');
    });
    effect(() => {
      this.group();
      this.accessToken.set('');
      this.tokenError.set('');
      this.notificationDriver();
      this.incidentFilter();
      this.incidentVehicleFilter();
      this.fleetFilter();
      this.reset();
      this.ontologyQuery.set('');
      this.incidentReview.set(false);

      this.fleetRows.set([]);
      const a = ['Ontology', 'Fault codes'].includes(this.group()) ? [] : this.actions();
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
        if (this.group() === 'Token' && this.auth.state() === 'signed-in') void this.loadToken();
        if (
          this.group() === 'Users' &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadUsers();
        if (
          this.group() === 'Ontology' &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadOntology();
        if (
          this.group() === 'Fault codes' &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadFaultCodes();
        if (
          this.group() === 'Notifications' &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadNotifications();
        if (
          ['Vehicles', 'Drivers'].includes(this.group()) &&
          this.auth.state() === 'signed-in' &&
          !this.permissionsLoading()
        )
          void this.reloadFleet();
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
  readonly actionLabel = (id: string) => ops.find((o) => o.id === id)?.label ?? '';
  description() {
    return (
      {
        Dashboard:
          'The agentic flow, shared domain knowledge and specifications behind Fault Code.',
        Incidents:
          'Review vehicle fault incidents and their investigation and notification status.',
        Vehicles: 'View vehicles, their drivers and dispatch fault codes.',
        Drivers: 'View drivers and their email addresses.',
        Engineering: 'Track escalations, evidence and approved resolutions.',
        Notifications: 'Review recorded notification history and delivery status.',
        Documents: 'Manage approved technical documents and search grounded evidence.',
        'Fault codes': 'Browse steering fault codes for the Silverado and Sierra test fleet.',
        Ontology: 'Browse the fault-code model and its connections to operational records.',
        'Fault definitions': 'Maintain fault knowledge and engineering publication.',
        'Agent activity': 'Inspect recorded advisory and investigation activity.',
        Users: 'View your identity and assigned roles.',
        Token: 'View, copy or refresh the API access token.',
        Tickets: 'Review Fault Code tickets, acceptance criteria and team discussion.',
        Integrations: 'Review authorized integration registrations.',
        'Operational health':
          'Inspect application readiness and distinguish configured services from verified integrations.',
      } as Record<string, string>
    )[this.group()];
  }
  private async loadPermissions() {
    this.permissionsLoading.set(true);
    try {
      const identity = await this.auth.identity();
      this.permissions.set(identity.permissions);
    } catch (e) {
      this.fail(e);
    } finally {
      this.permissionsLoading.set(false);
      if (this.group() === 'Token' && this.auth.state() === 'signed-in') void this.loadToken();
      if (this.group() === 'Incidents' && this.auth.state() === 'signed-in')
        void this.reloadIncidents();
      if (this.group() === 'Users' && this.auth.state() === 'signed-in') void this.reloadUsers();
      if (this.group() === 'Ontology' && this.auth.state() === 'signed-in')
        void this.reloadOntology();
      if (this.group() === 'Fault codes' && this.auth.state() === 'signed-in')
        void this.reloadFaultCodes();
      if (this.group() === 'Notifications' && this.auth.state() === 'signed-in')
        void this.reloadNotifications();
      if (['Vehicles', 'Drivers'].includes(this.group()) && this.auth.state() === 'signed-in')
        void this.reloadFleet();
    }
  }
  driverName(driver: any): string {
    return (driver.displayName || 'Driver details unavailable').replace(/^Demo\s+/, '');
  }
  vehicleName(vehicle: any): string {
    const match = /^SYN-(SILVERADO|SIERRA)-(\d+)$/.exec(vehicle.identifier ?? '');
    return match
      ? match[1] === 'SILVERADO'
        ? 'Chevrolet Silverado'
        : 'GMC Sierra'
      : vehicle.identifier;
  }
  bodyFields(action: Operation): Schema {
    if (this.group() !== 'Vehicles' || action.id !== 'CreateIncident') return action.body!;
    return {
      ...action.body!,
      properties: Object.fromEntries(
        Object.entries(action.body?.properties ?? {}).filter(([key]) => key !== 'vehicleId'),
      ),
      required: action.body?.required?.filter((key) => key !== 'vehicleId'),
    };
  }
  canDispatch() {
    const operation = ops.find((o) => o.id === 'CreateIncident');
    return !!operation && this.allowed(operation);
  }
  vehicleFaultCodes(vehicle: any) {
    const catalog = this.faultCodeRows().filter(
      (f) => f.system === vehicle.system && f.vehicles.includes(this.vehicleName(vehicle)),
    );
    const prefix = (
      { EPS: 'SYN-EPS:', 'steer-by-wire': 'SYN-SBW:', 'steering-ADAS': 'SYN-ADAS:' } as Record<
        string,
        string
      >
    )[vehicle.system];
    if (!prefix) return catalog;
    let code = prefix + 'UNLISTED';
    let suffix = 1;
    while (catalog.some((f) => f.code === code)) code = prefix + 'UNLISTED-' + ++suffix;
    return [...catalog, { code, unlisted: true }];
  }
  selectVehicleFault(id: string, code: string) {
    if (this.dispatching()[id]) return;
    this.selectedFaults.update((values) => ({ ...values, [id]: code }));
    if (this.dispatchKeys.get(id)?.code !== code) this.dispatchKeys.delete(id);
  }
  async dispatchFault(vehicle: any) {
    const code = this.selectedFaults()[vehicle.id];
    if (
      !code ||
      this.dispatching()[vehicle.id] ||
      !vehicle.active ||
      !this.canDispatch() ||
      vehicle.associationError ||
      vehicle.drivers?.length !== 1 ||
      !this.vehicleFaultCodes(vehicle).some((f) => f.code === code)
    )
      return;
    const operation = ops.find((o) => o.id === 'CreateIncident')!;
    let replay = this.dispatchKeys.get(vehicle.id);
    if (!replay || replay.code !== code) {
      replay = { code, key: crypto.randomUUID() };
      this.dispatchKeys.set(vehicle.id, replay);
    }
    this.dispatching.update((values) => ({ ...values, [vehicle.id]: true }));
    try {
      const result = await this.api.request(
        operation.path,
        'POST',
        { vehicleId: vehicle.id, code },
        { 'Idempotency-Key': replay.key },
      );
      if (!result.data.incidentId || !result.data.notificationId)
        throw new Error('Missing dispatch receipt');
      toast.success('Dispatched');
      void this.refreshVehicleIncidentCount(vehicle.id);
      this.dispatchKeys.delete(vehicle.id);
    } catch (error) {
      const message =
        error instanceof ApiError && error.status === 403
          ? 'Dispatch access denied.'
          : 'Dispatch was not confirmed. Retry to check the same request.';
      toast.error(message);
    } finally {
      this.dispatching.update((values) => ({ ...values, [vehicle.id]: false }));
    }
  }
  async refreshVehicleIncidentCount(vehicleId: string, epoch = this.epoch) {
    if (!this.incidentAllowed()) return;
    try {
      const result = await this.api.request(
        '/api/v1/incidents?vehicleId=' + encodeURIComponent(vehicleId) + '&page=1&pageSize=1',
      );
      const count = result.data.totalCount;
      if (
        epoch === this.epoch &&
        this.auth.state() === 'signed-in' &&
        Number.isSafeInteger(count) &&
        count >= 0
      )
        this.vehicleIncidentCounts.update((values) => ({ ...values, [vehicleId]: count }));
    } catch {
      if (epoch === this.epoch)
        this.vehicleIncidentCounts.update((values) => ({ ...values, [vehicleId]: undefined }));
    }
  }
  async reloadFleet(page = 1) {
    if (this.auth.state() !== 'signed-in' || this.permissionsLoading()) return;
    this.reset();

    this.fleetRows.set([]);
    this.vehicleIncidentCounts.set({});
    const vehicles = this.fleetIsVehicles();
    const operation = ops.find((o) => o.id === (vehicles ? 'ListVehicles' : 'ListDrivers'))!;
    if (!this.allowed(operation)) {
      this.error.set(true);
      this.message.set('Access denied. This action requires additional permissions.');
      return;
    }
    const epoch = ++this.epoch;
    this.busy.set(true);
    try {
      if (vehicles) await this.reloadFaultCodes();
      const filter = this.fleetFilter();
      const result = filter
        ? {
            data: {
              items: [
                (await this.api.request(operation.path + '/' + encodeURIComponent(filter))).data,
              ],
              totalCount: 1,
            },
          }
        : await this.api.request(operation.path + '?page=' + page + '&pageSize=20');
      const rows = result.data.items ?? [];
      const enriched = rows;
      if (vehicles)
        await Promise.all(rows.map((row: any) => this.refreshVehicleIncidentCount(row.id, epoch)));
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      this.fleetRows.set(enriched);
      this.fleetPage.set(page);
      this.fleetTotal.set(result.data.totalCount ?? rows.length);
    } catch (e) {
      if (epoch === this.epoch) this.fail(e);
    } finally {
      if (epoch === this.epoch) this.busy.set(false);
    }
  }
  incidentAllowed() {
    const operation = ops.find((o) => o.id === 'ListIncidents');
    return !!operation && this.allowed(operation);
  }
  async reloadUsers(page = 1) {
    if (this.auth.kind() === 'demo') {
      this.userRows.set([]);
      this.accessToken.set('');
      return;
    }
    if (this.auth.state() !== 'signed-in' || this.permissionsLoading()) return;
    const epoch = ++this.epoch;
    this.userRows.set([]);
    this.busy.set(true);
    this.error.set(false);
    try {
      const identity = await this.auth.identity();
      const account = { data: identity };
      this.ownAccount.set(account.data);
      const result = { data: { items: [identity], totalCount: 1 } };
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      this.userRows.set(result.data.items ?? []);
      this.userPage.set(page);
      this.userTotal.set(result.data.totalCount ?? 0);
    } catch (e) {
      if (epoch === this.epoch) this.fail(e);
    } finally {
      if (epoch === this.epoch) this.busy.set(false);
    }
  }
  async loadToken(refresh = false) {
    if (this.tokenBusy() || this.auth.state() !== 'signed-in') return;
    const epoch = this.epoch;
    this.tokenBusy.set(true);
    this.tokenError.set('');
    this.accessToken.set('');
    try {
      const result = await this.auth.apiToken(refresh);
      if (epoch !== this.epoch || this.group() !== 'Token' || this.auth.state() !== 'signed-in')
        return;
      this.accessToken.set(result.accessToken);
      this.tokenExpiresAt.set(result.expiresAt);
      this.tokenIdentity.set(result.identityName);
      if (refresh) toast.success('Entra access token refreshed');
    } catch {
      if (epoch === this.epoch)
        this.tokenError.set('Could not retrieve the API token. Try refreshing.');
    } finally {
      this.tokenBusy.set(false);
    }
  }
  async copyAccessToken() {
    try {
      await navigator.clipboard.writeText(this.accessToken());
      toast.success('Token copied');
    } catch {
      toast.error('Could not copy token.');
    }
  }
  async processNotification(row: any) {
    this.processingNotifications.update((v) => ({ ...v, [row.id]: true }));
    try {
      const current = await this.api.request('/api/v1/notifications/' + encodeURIComponent(row.id));
      await this.api.request(
        '/api/v1/notifications/' + encodeURIComponent(row.id),
        'PUT',
        { state: 'AwaitingAgent' },
        { 'If-Match': current.etag!, 'Idempotency-Key': crypto.randomUUID() },
      );
      await this.reloadNotifications(this.notificationPage());
    } catch {
      toast.error('Email processing did not complete. Review its recorded status.');
      await this.reloadNotifications(this.notificationPage());
    } finally {
      this.processingNotifications.update((v) => ({ ...v, [row.id]: false }));
    }
  }
  async reloadFaultCodes() {
    const epoch = this.epoch;
    this.faultCodeRows.set([]);
    try {
      const codes: any[] = [];
      let page = 1;
      let total = 0;
      do {
        const response = await this.api.request(
          '/api/v1/fault-codes?page=' + page + '&pageSize=100&retired=false',
        );
        codes.push(...(response.data.items ?? []));
        total = response.data.totalCount ?? codes.length;
        if (!response.data.items?.length) break;
        page++;
      } while (codes.length < total);
      const result = {
        data: {
          items: codes.map((f) => ({
            code: f.code,
            system: f.system,
            description: f.draft.description,
            vehicles: ['Chevrolet Silverado', 'GMC Sierra'],
          })),
          scope: '',
        },
      };
      if (epoch === this.epoch && this.auth.state() === 'signed-in') {
        this.faultCodeRows.set(result.data.items ?? []);
        this.faultCodeScope.set(result.data.scope ?? '');
      }
    } catch (error) {
      if (epoch === this.epoch) this.fail(error);
    }
  }
  async reloadOntology() {
    const epoch = this.epoch;
    this.ontologyRows.set([]);
    this.ontologyError.set('');
    if (this.auth.state() === 'signed-in') this.ontologyRows.set(domainModel);
  }

  async reloadNotifications(page = 1) {
    if (this.auth.state() !== 'signed-in' || this.permissionsLoading()) return;
    const epoch = ++this.epoch;
    this.busy.set(true);
    this.error.set(false);
    this.notificationRows.set([]);
    try {
      const driverId = this.route.snapshot.queryParamMap.get('driverId') ?? '';
      this.notificationDriver.set(driverId);
      const result = await this.api.request(
        '/api/v1/notifications?page=' +
          page +
          '&pageSize=20' +
          (driverId ? '&driverId=' + encodeURIComponent(driverId) : '') +
          (this.incidentFilter() ? '&incidentId=' + encodeURIComponent(this.incidentFilter()) : ''),
      );
      const rows = result.data.items ?? [];
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      this.notificationRows.set(rows);
      this.notificationPage.set(page);
      this.notificationTotal.set(result.data.totalCount ?? rows.length);
    } catch (e) {
      if (epoch === this.epoch) this.fail(e);
    } finally {
      if (epoch === this.epoch) this.busy.set(false);
    }
  }
  async reloadIncidents(page = 1) {
    if (this.auth.state() !== 'signed-in' || this.permissionsLoading()) return;
    this.incidentReview.set(false);
    this.reset();
    this.choose(ops.find((o) => o.id === 'ListIncidents'));
    this.parameters = { page, pageSize: 20 };
    const vehicleId = this.incidentVehicleFilter();
    if (vehicleId) this.parameters.vehicleId = vehicleId;
    if (this.incidentAllowed()) await this.execute();
  }
  async reviewIncident(record: any) {
    this.incidentReview.set(true);
    this.resolutionNotes = '';
    this.reset();
    this.choose(ops.find((o) => o.id === 'GetIncident'));
    this.parameters = { id: record.id };
    await this.execute();
    this.resolutionNotes = this.data()?.resolution?.text ?? '';
  }
  canResolveIncident() {
    const update = ops.find((o) => o.id === 'UpdateIncident');
    return !!update && this.allowed(update);
  }
  incidentDatadogUrl() {
    const reference = this.data()?.datadog;
    if (!reference?.id || !Number.isSafeInteger(reference.number) || reference.number < 1)
      return null;
    const expected = 'https://app.datadoghq.com/incidents/' + reference.number;
    return reference.url === expected ? expected : null;
  }
  async resolveIncident() {
    const record = this.data();
    const resolution = this.resolutionNotes.trim();
    if (
      this.busy() ||
      !record?.id ||
      record.status === 'Resolved' ||
      !resolution ||
      !this.canResolveIncident()
    )
      return;
    const fingerprint = JSON.stringify([record.id, record.version, resolution]);
    if (this.resolutionAttempt?.fingerprint !== fingerprint)
      this.resolutionAttempt = { fingerprint, key: crypto.randomUUID() };
    const epoch = ++this.epoch;
    this.busy.set(true);
    this.error.set(false);
    this.message.set('');
    try {
      await this.api.request(
        '/api/v1/incidents/' + encodeURIComponent(record.id),
        'PUT',
        { status: 'Resolved', resolution },
        { 'If-Match': '"' + record.version + '"', 'Idempotency-Key': this.resolutionAttempt!.key },
      );
      const result = await this.api.request('/api/v1/incidents/' + encodeURIComponent(record.id));
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      this.data.set(result.data);
      this.resolutionNotes = result.data.resolution?.text ?? resolution;
      toast.success('Incident resolved');
    } catch (e) {
      if (epoch === this.epoch) this.fail(e);
    } finally {
      if (epoch === this.epoch) this.busy.set(false);
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
    this.accessToken.set('');
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
        429: e.dailyQuota
          ? 'Daily API limit reached (200 requests). Try again after midnight UTC.'
          : 'Too many requests. Wait before trying again.',
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
