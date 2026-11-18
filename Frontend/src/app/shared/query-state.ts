import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
@Component({
  selector: 'app-query-state',
  imports: [DatePipe],
  template: `
    <div class="refresh-bar">
      <span class="muted">
        @if (updated()) {
          Updated {{ updated() | date: 'HH:mm:ss' }} · Refreshes every 5s
        } @else {
          Waiting for the exchange
        }
      </span>
      <button class="button secondary small" (click)="refresh.emit()" [disabled]="loading()">
        {{ loading() ? 'Refreshing…' : 'Refresh' }}
      </button>
    </div>
    @if (error()) {
      <div class="notice error" role="alert">
        <strong>{{ hasData() ? 'Showing previously loaded data.' : 'Data unavailable.' }}</strong>
        {{ error() }}
      </div>
    }
    @if (loading() && !hasData()) {
      <div class="skeleton" aria-label="Loading exchange data" role="status">
        <span></span><span></span><span></span><span></span>
      </div>
    }
  `,
})
export class QueryState {
  updated = input<Date | null>(null);
  loading = input(false);
  error = input('');
  hasData = input(false);
  refresh = output();
}
