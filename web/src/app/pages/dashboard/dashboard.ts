import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DashboardSummary } from '../../core/models';
import { TrackingService } from '../../core/tracking.service';
import { StatusBadge, StatusLabelPipe } from '../../shared/status';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe, DecimalPipe, PercentPipe, StatusBadge, StatusLabelPipe],
  template: `
    <div class="page-head">
      <div>
        <h1>Operations dashboard</h1>
        <p>Updates live as shipments move.</p>
      </div>
      <a class="btn" routerLink="/shipments/new">+ New shipment</a>
    </div>

    @if (summary(); as s) {
      <div class="grid kpis">
        <div class="card kpi"><span>Active shipments</span><strong>{{ s.active }}</strong></div>
        <div class="card kpi" [class.alert]="s.delayed > 0">
          <span>Delayed</span><strong>{{ s.delayed }}</strong>
        </div>
        <div class="card kpi"><span>Delivered (7 days)</span><strong>{{ s.deliveredLast7Days }}</strong></div>
        <div class="card kpi"><span>On-time rate (30 days)</span><strong>{{ s.onTimeRateLast30Days | percent: '1.0-0' }}</strong></div>
      </div>

      <div class="grid two">
        <section class="card">
          <h2>Shipments by status</h2>
          @for (row of s.byStatus; track row.status) {
            <div class="bar-row">
              <span class="bar-label">{{ row.status | statusLabel }}</span>
              <div class="bar"><div [class]="'fill s-' + row.status" [style.width.%]="(row.count / maxCount()) * 100"></div></div>
              <span class="bar-count">{{ row.count | number }}</span>
            </div>
          }
        </section>

        <section class="card">
          <h2>Most overdue</h2>
          @for (d of s.mostDelayed; track d.id) {
            <a class="overdue" [routerLink]="['/shipments', d.id]">
              <span class="mono">{{ d.reference }}</span>
              <span class="muted">{{ d.customerName }}</span>
              <span class="due">ETA {{ d.estimatedArrival | date: 'd MMM' }}</span>
            </a>
          } @empty {
            <p class="empty">Nothing overdue. 🎉</p>
          }
        </section>
      </div>
    } @else if (error()) {
      <div class="error">{{ error() }}</div>
    } @else {
      <p class="muted">Loading…</p>
    }

    <section class="card feed">
      <h2>Live activity</h2>
      @for (u of tracking.recent(); track $index) {
        <a class="feed-row" [routerLink]="['/shipments', u.shipmentId]">
          <span class="time muted">{{ u.latestEvent.occurredAt | date: 'HH:mm:ss' }}</span>
          <span class="mono">{{ u.reference }}</span>
          <app-status-badge [status]="u.status" [delayed]="u.isDelayed" />
          <span class="muted">{{ u.latestEvent.note || u.latestEvent.location }}</span>
        </a>
      } @empty {
        <p class="muted">Waiting for updates… The demo simulator moves a shipment every few seconds.</p>
      }
    </section>
  `,
  styles: `
    .kpis { grid-template-columns: repeat(4, 1fr); margin-bottom: 16px; }
    .kpi { display: grid; gap: 4px; }
    .kpi span { color: var(--muted); font-weight: 500; }
    .kpi strong { font-size: 30px; }
    .kpi.alert strong { color: var(--danger); }
    .two { grid-template-columns: 1fr 1fr; margin-bottom: 16px; }
    .bar-row { display: grid; grid-template-columns: 140px 1fr 40px; align-items: center; gap: 10px; margin: 8px 0; }
    .bar { height: 10px; background: var(--hover); border-radius: 999px; overflow: hidden; }
    /* The global .s-* status classes set a text colour; use it as the bar colour. */
    .fill { height: 100%; border-radius: 999px; transition: width .4s; background: currentColor; }
    .bar-count { text-align: right; font-weight: 600; }
    .overdue, .feed-row {
      display: flex; gap: 12px; align-items: center; padding: 8px 6px; border-bottom: 1px solid var(--border);
      color: inherit; text-decoration: none;
    }
    .overdue:hover, .feed-row:hover { background: var(--hover); }
    .due { margin-left: auto; color: var(--danger); font-weight: 600; }
    .feed { max-height: 420px; overflow-y: auto; }
    .time { width: 64px; }
    @media (max-width: 900px) { .kpis, .two { grid-template-columns: 1fr 1fr; } .two { grid-template-columns: 1fr; } }
  `,
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly tracking = inject(TrackingService);

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly maxCount = computed(() => Math.max(1, ...(this.summary()?.byStatus.map(s => s.count) ?? [1])));

  ngOnInit(): void {
    this.load();
    // Re-count when things change, but at most once a second during bursts.
    this.tracking.updates.pipe(debounceTime(1000), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  private load(): void {
    this.api.dashboard().subscribe({
      next: s => this.summary.set(s),
      error: () => this.error.set('Could not load the dashboard.'),
    });
  }
}
