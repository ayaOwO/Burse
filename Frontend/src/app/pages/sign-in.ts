import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Session } from '../core/session';
import { errorMessage } from '../core/api';
@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule],
  template: `<section class="entry-layout">
    <div class="entry-intro">
      <h1>A clear view.<br />A considered trade.</h1>
      <p>
        Follow the market, place your orders, and see where you stand. Your exchange workspace
        starts here.
      </p>
      <div class="entry-note">
        <span class="status-dot"></span> Simulated exchange · Seeded market
      </div>
    </div>
    <div class="entry-form">
      <h2>Enter the exchange</h2>
      <p class="muted">Use your existing trader ID to open your workspace.</p>
      <form (submit)="$event.preventDefault(); submit()">
        <label for="trader-id">Trader ID</label
        ><input
          id="trader-id"
          type="text"
          inputmode="numeric"
          autocomplete="username"
          [formControl]="id"
          aria-describedby="id-help id-error"
          placeholder="Enter your trader ID"
        />
        <p id="id-help" class="field-help">
          For the supplied demo, try trader 1 (Amit) or 2 (Tal).
        </p>
        @if (id.invalid && id.touched) {
          <p id="id-error" class="field-error">
            Enter a whole-number trader ID between 1 and 2,147,483,647.
          </p>
        }
        @if (session.error()) {
          <p role="alert" class="notice error">{{ session.error() }}</p>
        }
        <button type="submit" class="button primary full" [disabled]="pending()">
          {{ pending() ? 'Opening workspace…' : 'Continue to market' }}
        </button>
      </form>
      <p class="entry-disclaimer">
        Your ID is remembered in this browser until you sign out. This demo uses trader selection,
        without password authentication.
      </p>
      @if (session.storageNote()) {
        <p class="notice">{{ session.storageNote() }}</p>
      }
    </div>
  </section>`,
})
export class SignIn {
  session = inject(Session);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  id = new FormControl(this.session.savedId(), {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[1-9]\d*$/), Validators.max(2147483647)],
  });
  pending = signal(false);
  submit() {
    this.id.markAsTouched();
    if (this.id.invalid || this.pending()) return;
    this.pending.set(true);
    this.session.error.set('');
    this.session
      .identify(Number(this.id.value))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pending.set(false);
          void this.router.navigateByUrl('/market');
        },
        error: (error) => {
          this.pending.set(false);
          this.session.error.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'No trader found for this ID. Check the ID and try again.'
              : errorMessage(error),
          );
        },
      });
  }
}
