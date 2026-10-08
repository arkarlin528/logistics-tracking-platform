import { Component, effect, inject, OnDestroy } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { TrackingService } from '../core/tracking.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a class="brand" routerLink="/">
        <span class="brand-mark">◆</span> ShipTrack
      </a>
      <nav>
        @if (auth.isStaff()) {
          <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a>
        }
        <a routerLink="/shipments" routerLinkActive="active">Shipments</a>
        <a routerLink="/map" routerLinkActive="active">Live map</a>
        <a routerLink="/rates" routerLinkActive="active">Rates</a>
      </nav>
      <div class="right">
        <span class="live" [class]="'live live-' + tracking.state()" [title]="'Realtime: ' + tracking.state()">
          <span class="dot"></span>{{ tracking.state() === 'live' ? 'Live' : tracking.state() }}
        </span>
        <span class="who">
          <strong>{{ auth.user()?.displayName }}</strong>
          <small>{{ auth.user()?.role }}</small>
        </span>
        <button class="btn btn-ghost" (click)="auth.logout()">Sign out</button>
      </div>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
  styles: `
    .topbar {
      position: sticky; top: 0; z-index: 1000;
      display: flex; align-items: center; gap: 24px;
      padding: 0 24px; height: 60px;
      background: var(--surface); border-bottom: 1px solid var(--border);
    }
    .brand { font-weight: 700; font-size: 18px; color: var(--text); text-decoration: none; white-space: nowrap; }
    .brand-mark { color: var(--accent); }
    nav { display: flex; gap: 4px; }
    nav a {
      padding: 8px 12px; border-radius: 8px; color: var(--muted); text-decoration: none; font-weight: 500;
    }
    nav a:hover { background: var(--hover); color: var(--text); }
    nav a.active { background: var(--accent-soft); color: var(--accent); }
    .right { margin-left: auto; display: flex; align-items: center; gap: 16px; }
    .who { display: flex; flex-direction: column; line-height: 1.2; text-align: right; }
    .who small { color: var(--muted); }
    .live { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--muted); text-transform: capitalize; }
    .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--muted); }
    .live-live { color: var(--success); }
    .live-live .dot { background: var(--success); box-shadow: 0 0 0 0 var(--success); animation: pulse 2s infinite; }
    .live-reconnecting .dot, .live-connecting .dot { background: var(--warning); }
    @keyframes pulse {
      0% { box-shadow: 0 0 0 0 rgba(22, 163, 74, .5); }
      70% { box-shadow: 0 0 0 8px rgba(22, 163, 74, 0); }
      100% { box-shadow: 0 0 0 0 rgba(22, 163, 74, 0); }
    }
    @media (max-width: 800px) {
      .topbar { flex-wrap: wrap; height: auto; padding: 8px 16px; gap: 8px; }
      nav { order: 3; width: 100%; overflow-x: auto; }
      .who { display: none; }
    }
  `,
})
export class Shell implements OnDestroy {
  protected readonly auth = inject(AuthService);
  protected readonly tracking = inject(TrackingService);

  constructor() {
    effect(() => {
      if (this.auth.isSignedIn()) void this.tracking.start();
      else void this.tracking.stop();
    });
  }

  ngOnDestroy(): void {
    void this.tracking.stop();
  }
}
