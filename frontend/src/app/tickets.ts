import {
  Component,
  DestroyRef,
  Pipe,
  PipeTransform,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { Marked, Renderer } from 'marked';
import { Auth } from './auth';

interface Ticket {
  id: string;
  identifier: string;
  title: string;
  status: string;
  statusType: string;
  priority: string;
  assignee: string | null;
  labels: string[];
  project: string | null;
  createdAt: string;
  updatedAt: string;
  dueDate: string | null;
  completedAt: string | null;
  url: string;
}
interface Reference {
  id: string;
  identifier: string;
  title: string;
  status: string;
  relationship: string;
}
interface TicketList {
  items: Ticket[];
  retrievedAt: string;
  team: string;
}
interface TicketDetail {
  ticket: Ticket;
  description: string | null;
  creator: string | null;
  cycle: string | null;
  milestone: string | null;
  estimate: number | null;
  parent: Reference | null;
  subtasks: Reference[];
  related: Reference[];
  comments: {
    id: string;
    author: string | null;
    body: string;
    createdAt: string;
    updatedAt: string;
  }[];
  attachments: { title: string; subtitle: string | null; url: string }[];
  retrievedAt: string;
}
const escapeHtml = (s: string) =>
  s.replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!,
  );
// Ticket content is untrusted. Do not load external images or allow raw HTML.
// Angular's normal innerHTML sanitizer remains enabled after Markdown rendering.
@Pipe({ name: 'ticketMarkdown' })
export class TicketMarkdown implements PipeTransform {
  private readonly markdown = new Marked({
    async: false,
    renderer: Object.assign(new Renderer(), {
      html: ({ text }: { text: string }) => escapeHtml(text),
      image: ({ text }: { text: string }) => `<span>[Image: ${escapeHtml(text)}]</span>`,
      checkbox: ({ checked }: { checked: boolean }) => `<span>${checked ? '☑' : '☐'} </span>`,
      link(this: Renderer, { href, tokens }: { href: string; tokens: any[] }) {
        const text = this.parser.parseInline(tokens);
        try {
          const u = new URL(href);
          if (u.protocol === 'https:' && !u.username && !u.password)
            return `<a href="${escapeHtml(u.href)}" target="_blank" rel="noopener noreferrer">${text}</a>`;
        } catch {
          /* plain text */
        }
        return text;
      },
    }),
  });
  transform(value: string | null): string {
    return this.markdown.parse(value ?? '', { async: false });
  }
}
@Component({
  selector: 'app-tickets',
  imports: [
    DatePipe,
    RouterLink,
    FormsModule,
    HlmCardImports,
    HlmTableImports,
    HlmSelectImports,
    HlmFieldImports,
    HlmInput,
    HlmButton,
    HlmBadge,
    HlmAlertImports,
    TicketMarkdown,
  ],
  template: `
    <section hlmCard class="min-w-0 p-6" aria-label="Team tickets">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 hlmCardTitle>{{ detail()?.ticket?.identifier || 'Fault Code tickets' }}</h2>
        <div class="flex gap-2">
          @if (ticketId()) {
            <a hlmBtn variant="outline" routerLink="/admin/tickets">Back to tickets</a>
          }
          <button hlmBtn variant="outline" [disabled]="loading()" (click)="load(true)">
            Refresh
          </button>
        </div>
      </div>
      @if (loading()) {
        <p role="status">Loading tickets…</p>
      }
      @if (error()) {
        <div hlmAlert variant="destructive" role="alert">{{ error() }}</div>
      }
      @if (!ticketId()) {
        <div class="grid gap-4 sm:grid-cols-2">
          <div hlmField>
            <label hlmFieldLabel for="ticket-search">Search tickets</label>
            <input
              hlmInput
              id="ticket-search"
              placeholder="ID, title or label"
              [ngModel]="search()"
              (ngModelChange)="search.set($event); page.set(1)"
            />
          </div>
          <div hlmField>
            <label hlmFieldLabel for="ticket-status">Status</label>
            <hlm-select
              [itemToString]="statusLabel"
              [value]="status()"
              (valueChange)="status.set($event ?? 'all'); page.set(1)"
            >
              <hlm-select-trigger buttonId="ticket-status" class="w-full"
                ><hlm-select-value placeholder="All statuses"
              /></hlm-select-trigger>
              <hlm-select-content *hlmSelectPortal role="region" aria-label="Ticket status choices">
                <hlm-select-item value="all">All statuses</hlm-select-item>
                @for (state of statuses(); track state) {
                  <hlm-select-item [value]="state">{{ state }}</hlm-select-item>
                }
              </hlm-select-content>
            </hlm-select>
          </div>
        </div>
        <div class="overflow-x-auto">
          <table hlmTable>
            <caption hlmCaption>
              {{
                filtered().length
              }}
              tickets ·
              {{
                list()?.team || 'Fault Code · FAU'
              }}
            </caption>
            <thead hlmTHead>
              <tr hlmTr>
                <th hlmTh>Ticket</th>
                <th hlmTh>Status</th>
                <th hlmTh>Priority</th>
                <th hlmTh>Project</th>
                <th hlmTh>Updated</th>
              </tr>
            </thead>
            <tbody hlmTBody>
              @for (item of visible(); track item.id) {
                <tr hlmTr>
                  <td hlmTd class="min-w-64 max-w-lg whitespace-normal">
                    <a
                      hlmBtn
                      variant="link"
                      class="h-auto justify-start whitespace-normal p-0 text-left"
                      routerLink="/admin/tickets"
                      [queryParams]="{ ticketId: item.id }"
                      >{{ item.identifier }} · {{ item.title }}</a
                    >
                  </td>
                  <td hlmTd>
                    <span hlmBadge variant="secondary">{{ item.status }}</span>
                  </td>
                  <td hlmTd>{{ item.priority }}</td>
                  <td hlmTd>{{ item.project || '—' }}</td>
                  <td hlmTd class="whitespace-nowrap">{{ item.updatedAt | date: 'mediumDate' }}</td>
                </tr>
              } @empty {
                @if (!loading() && !error()) {
                  <tr hlmTr>
                    <td hlmTd colspan="5">No matching tickets.</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
        <div class="flex flex-wrap items-center gap-3">
          <button hlmBtn variant="outline" [disabled]="page() === 1" (click)="page.set(page() - 1)">
            Previous
          </button>
          <span class="text-sm">Page {{ page() }} of {{ pages() }}</span>
          <button
            hlmBtn
            variant="outline"
            [disabled]="page() >= pages()"
            (click)="page.set(page() + 1)"
          >
            Next
          </button>
        </div>
      }
      @if (detail(); as d) {
        <h3 class="text-xl font-semibold">{{ d.ticket.title }}</h3>
        <dl class="grid grid-cols-2 gap-x-6 gap-y-3 text-sm md:grid-cols-4">
          @for (field of metadata(d); track field[0]) {
            <div>
              <dt class="text-muted-foreground">{{ field[0] }}</dt>
              <dd class="mt-1 break-words font-medium">{{ field[1] }}</dd>
            </div>
          }
          <div>
            <dt class="text-muted-foreground">Created</dt>
            <dd>{{ d.ticket.createdAt | date: 'medium' }}</dd>
          </div>
          <div>
            <dt class="text-muted-foreground">Updated</dt>
            <dd>{{ d.ticket.updatedAt | date: 'medium' }}</dd>
          </div>
          @if (d.ticket.completedAt) {
            <div>
              <dt class="text-muted-foreground">Completed</dt>
              <dd>{{ d.ticket.completedAt | date: 'medium' }}</dd>
            </div>
          }
        </dl>
        <div class="flex flex-wrap gap-2">
          @for (label of d.ticket.labels; track label) {
            <span hlmBadge variant="outline">{{ label }}</span>
          }
        </div>
        <section aria-label="Ticket description" class="space-y-3">
          <h3 class="font-semibold">Description and acceptance criteria</h3>
          @if (d.description) {
            <article
              class="break-words text-sm leading-6 [&_h1]:my-4 [&_h1]:text-xl [&_h2]:my-4 [&_h2]:text-lg [&_h3]:my-3 [&_h3]:font-semibold [&_p]:my-3 [&_ul]:list-disc [&_ul]:ps-6 [&_ol]:list-decimal [&_ol]:ps-6 [&_a]:underline [&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-muted [&_pre]:p-3 [&_code]:font-mono [&_table]:block [&_table]:overflow-x-auto [&_td]:border [&_td]:p-2 [&_th]:border [&_th]:p-2 [&_blockquote]:border-s-2 [&_blockquote]:ps-3"
              [innerHTML]="d.description | ticketMarkdown"
            ></article>
          } @else {
            <p class="text-sm text-muted-foreground">No description provided.</p>
          }
        </section>
        @if (references(d).length) {
          <section aria-label="Related tickets" class="space-y-2">
            <h3 class="font-semibold">Parent, subtasks and related tickets</h3>
            @for (ref of references(d); track $index) {
              <div class="flex flex-wrap items-center gap-2">
                <span hlmBadge variant="outline">{{ ref.relationship }}</span
                ><a
                  hlmBtn
                  variant="link"
                  class="h-auto whitespace-normal p-0 text-left"
                  routerLink="/admin/tickets"
                  [queryParams]="{ ticketId: ref.id }"
                  >{{ ref.identifier }} · {{ ref.title }}</a
                ><span class="text-sm text-muted-foreground">{{ ref.status }}</span>
              </div>
            }
          </section>
        }
        @if (d.attachments.length) {
          <section class="space-y-2" aria-label="Attachments">
            <h3 class="font-semibold">Attachments</h3>
            @for (a of d.attachments; track $index) {
              <div>
                <a
                  hlmBtn
                  variant="link"
                  [href]="a.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="h-auto whitespace-normal p-0"
                  >{{ a.title }}</a
                >
                @if (a.subtitle) {
                  <p class="text-sm text-muted-foreground">{{ a.subtitle }}</p>
                }
              </div>
            }
          </section>
        }
        <section aria-label="Ticket comments" class="space-y-4">
          <h3 class="font-semibold">Comments ({{ d.comments.length }})</h3>
          @for (comment of d.comments; track comment.id) {
            <section hlmCard class="gap-2 p-4">
              <div class="flex flex-wrap gap-2 text-sm">
                <span class="font-medium">{{ comment.author || 'Former team member' }}</span
                ><time class="text-muted-foreground">{{ comment.createdAt | date: 'medium' }}</time>
              </div>
              <article
                class="break-words text-sm leading-6 [&_p]:my-2 [&_a]:underline [&_ul]:list-disc [&_ul]:ps-6 [&_pre]:overflow-x-auto [&_pre]:bg-muted [&_pre]:p-3"
                [innerHTML]="comment.body | ticketMarkdown"
              ></article>
            </section>
          } @empty {
            <p class="text-sm text-muted-foreground">No comments yet.</p>
          }
        </section>
      }
      @if (retrievedAt()) {
        <p class="text-xs text-muted-foreground">
          Retrieved {{ retrievedAt() | date: 'medium' }} · Read only
        </p>
      }
    </section>
  `,
})
export class Tickets {
  private readonly auth = inject(Auth);
  private readonly route = inject(ActivatedRoute);
  private controller?: AbortController;
  private epoch = 0;
  readonly ticketId = signal('');
  readonly list = signal<TicketList | null>(null);
  readonly detail = signal<TicketDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly search = signal('');
  readonly status = signal('all');
  readonly statusLabel = (value: string) => (value === 'all' ? 'All statuses' : value);
  readonly page = signal(1);
  readonly statuses = computed(() =>
    [...new Set(this.list()?.items.map((t) => t.status) ?? [])].sort(),
  );
  readonly filtered = computed(() =>
    (this.list()?.items ?? []).filter(
      (t) =>
        (this.status() === 'all' || t.status === this.status()) &&
        [t.identifier, t.title, t.assignee, t.project, ...t.labels]
          .join(' ')
          .toLowerCase()
          .includes(this.search().trim().toLowerCase()),
    ),
  );
  readonly visible = computed(() =>
    this.filtered().slice((this.page() - 1) * 20, this.page() * 20),
  );
  readonly pages = computed(() => Math.max(1, Math.ceil(this.filtered().length / 20)));
  readonly retrievedAt = computed(() =>
    this.ticketId() ? this.detail()?.retrievedAt : this.list()?.retrievedAt,
  );
  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.epoch++;
      this.controller?.abort();
      this.list.set(null);
      this.detail.set(null);
    });
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.ticketId.set(params.get('ticketId') ?? '');
      void this.load();
    });
    effect(() => {
      if (this.auth.state() !== 'signed-in') {
        this.epoch++;
        this.controller?.abort();
        this.list.set(null);
        this.detail.set(null);
      }
    });
  }
  metadata(d: TicketDetail) {
    return [
      ['Status', d.ticket.status],
      ['Priority', d.ticket.priority],
      ['Assignee', d.ticket.assignee || 'Unassigned'],
      ['Project', d.ticket.project || '—'],
      ['Creator', d.creator || '—'],
      ['Cycle', d.cycle || '—'],
      ['Milestone', d.milestone || '—'],
      ['Estimate', d.estimate == null ? '—' : String(d.estimate)],
      ['Due', d.ticket.dueDate || '—'],
    ];
  }
  references(d: TicketDetail): Reference[] {
    return [...(d.parent ? [d.parent] : []), ...d.subtasks, ...d.related];
  }
  async load(refresh = false) {
    const epoch = ++this.epoch;
    this.controller?.abort();
    this.controller = new AbortController();
    this.error.set('');
    this.detail.set(null);
    this.loading.set(true);
    const id = this.ticketId();
    if (id && !/^[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(id)) {
      this.error.set('Ticket not found.');
      this.loading.set(false);
      return;
    }
    if (!id && !refresh && this.list()) {
      this.loading.set(false);
      return;
    }
    if (!id) this.list.set(null);
    try {
      const headers = await this.auth.apiHeaders();
      const response = await fetch('/workspace/tickets' + (id ? '/' + id : '?refresh=' + refresh), {
        headers,
        credentials: 'same-origin',
        cache: 'no-store',
        redirect: 'error',
        signal: this.controller.signal,
      });
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      if (response.status === 401) {
        this.auth.expire();
        throw new Error('Your session has expired.');
      }
      if (!response.ok)
        throw new Error(
          response.status === 404
            ? 'Ticket not found.'
            : response.status === 403
              ? 'You do not have permission to review tickets.'
              : 'Tickets could not be retrieved. Try refreshing.',
        );
      const data = await response.json();
      if (epoch !== this.epoch || this.auth.state() !== 'signed-in') return;
      if (id) this.detail.set(data);
      else {
        this.list.set(data);
        this.page.set(1);
      }
    } catch (e) {
      if (epoch === this.epoch)
        this.error.set(
          e instanceof Error && e.name !== 'AbortError'
            ? e.message
            : 'Tickets could not be retrieved.',
        );
    } finally {
      if (epoch === this.epoch) this.loading.set(false);
    }
  }
}
