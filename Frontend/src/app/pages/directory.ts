import { Component, computed, inject, signal } from '@angular/core';
import { Api } from '../core/api';
import { Session } from '../core/session';
import { liveQuery } from '../core/live-query';
import { QueryState } from '../shared/query-state';
@Component({
  selector: 'app-directory',
  imports: [QueryState],
  template: `<div class="page-heading">
      <div>
        <h1>Traders</h1>
        <p>The people participating in this exchange.</p>
      </div>
      <span class="context-label">{{ q.data()?.length ?? '—' }} participants</span>
    </div>
    <app-query-state
      [updated]="q.updated()"
      [loading]="q.loading()"
      [error]="q.error()"
      [hasData]="!!q.data()"
      (refresh)="q.refresh()"
    />
    <section class="surface">
      <div class="section-toolbar">
        <h2>Trader directory</h2>
        <label class="sr-only" for="trader-search">Search traders by name or ID</label
        ><input
          id="trader-search"
          type="search"
          placeholder="Search name or ID"
          [value]="search()"
          (input)="search.set($any($event.target).value)"
        />
      </div>
      @if (q.data()) {
        <div class="table-scroll" role="region" aria-label="Trader directory" tabindex="0">
          <table>
            <thead>
              <tr>
                <th>Trader</th>
                <th>Trader ID</th>
                <th>Workspace</th>
              </tr>
            </thead>
            <tbody>
              @for (trader of filtered(); track trader.id) {
                <tr>
                  <td class="strong">{{ trader.name }}</td>
                  <td class="muted">{{ trader.id }}</td>
                  <td>
                    @if (trader.id === session.trader()?.id) {
                      <span class="badge">You</span>
                    } @else {
                      <span class="muted">Participant</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (!filtered().length) {
          <div class="empty">
            <h3>No traders found</h3>
            <p>Try another name or ID.</p>
            <button class="button secondary" (click)="search.set('')">Clear search</button>
          </div>
        }
      }
    </section>
    <p class="footnote">To use a different trader's workspace, sign out first.</p>`,
})
export class Directory {
  private api = inject(Api);
  session = inject(Session);
  q = liveQuery(() => this.api.traders());
  search = signal('');
  filtered = computed(() =>
    (this.q.data() ?? []).filter(
      (t) =>
        t.id > 0 && `${t.name} ${t.id}`.toLowerCase().includes(this.search().trim().toLowerCase()),
    ),
  );
}
