import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Paged, SHIPMENT_STATUSES, ShipmentFilter, ShipmentSummary } from '../../core/models';
import { TrackingService } from '../../core/tracking.service';
import { StatusBadge, StatusLabelPipe } from '../../shared/status';

@Component({
  selector: 'app-shipments',
  imports: [FormsModule, RouterLink, DatePipe, StatusBadge, StatusLabelPipe],
  template: `
    <div class="page-head">
      <div>
        <h1>Shipments</h1>
        <p>{{ auth.isStaff() ? 'All customers' : 'Your shipments' }} · rows highlight when they change live</p>
      </div>
      @if (auth.isStaff()) {
        <a class="btn" routerLink="/shipments/new">+ New shipment</a>
      }
    </div>

    <div class="card">
      <div class="filters">
        <input placeholder="Search reference, cargo or customer…" [ngModel]="filter().search"
               (ngModelChange)="searchChanged.next($event)" />
        <select [ngModel]="filter().status" (ngModelChange)="apply({ status: $event })">
          <option value="">All statuses</option>
          @for (s of statuses; track s) {
            <option [value]="s">{{ s | statusLabel }}</option>
          }
        </select>
        <label class="check">
          <input type="checkbox" [ngModel]="filter().delayed === true" (ngModelChange)="apply({ delayed: $event ? true : null })" />
          Delayed only
        </label>
      </div>

      @if (error()) {
        <div class="error">{{ error() }}</div>
      }

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Reference</th><th>Status</th><th>Route</th><th>Mode</th>
              @if (auth.isStaff()) { <th>Customer</th> }
              <th>Carrier</th><th>ETA</th>
            </tr>
          </thead>
          <tbody>
            @for (s of page()?.items ?? []; track s.id) {
              <tr class="clickable" [class.flash]="flashing().has(s.id)" (click)="open(s.id)">
                <td class="mono"><a [routerLink]="['/shipments', s.id]" (click)="$event.stopPropagation()">{{ s.reference }}</a></td>
                <td><app-status-badge [status]="s.status" [delayed]="s.isDelayed" /></td>
                <td class="mono">{{ s.originCode }} → {{ s.destinationCode }}</td>
                <td>{{ s.mode }}</td>
                @if (auth.isStaff()) { <td>{{ s.customerName }}</td> }
                <td>{{ s.carrierName }}</td>
                <td [class.late]="s.isDelayed">{{ s.estimatedArrival | date: 'd MMM, HH:mm' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">{{ loading() ? 'Loading…' : 'No shipments match these filters.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (page(); as p) {
        <div class="pager">
          <span class="muted">{{ p.totalCount }} shipments · page {{ p.page }} of {{ p.totalPages || 1 }}</span>
          <button class="btn btn-ghost" [disabled]="p.page <= 1" (click)="apply({ page: p.page - 1 }, false)">Previous</button>
          <button class="btn btn-ghost" [disabled]="p.page >= p.totalPages" (click)="apply({ page: p.page + 1 }, false)">Next</button>
        </div>
      }
    </div>
  `,
  styles: `
    .filters { display: grid; grid-template-columns: 1fr 200px auto; gap: 10px; margin-bottom: 14px; }
    .check { display: flex; align-items: center; gap: 8px; font-weight: 500; white-space: nowrap; }
    .late { color: var(--danger); font-weight: 600; }
    .pager { display: flex; align-items: center; justify-content: flex-end; gap: 8px; margin-top: 12px; }
    .pager span { margin-right: auto; }
    @media (max-width: 700px) { .filters { grid-template-columns: 1fr; } }
  `,
})
export class ShipmentsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly tracking = inject(TrackingService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly statuses = SHIPMENT_STATUSES;
  protected readonly filter = signal<ShipmentFilter>({ search: '', status: '', delayed: null, page: 1, pageSize: 15 });
  protected readonly page = signal<Paged<ShipmentSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly flashing = signal(new Set<number>());
  protected readonly searchChanged = new Subject<string>();

  ngOnInit(): void {
    this.load();
    this.searchChanged.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(search => this.apply({ search }));

    // Patch visible rows in place instead of reloading the page, then flash them.
    this.tracking.updates.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(update => {
      const current = this.page();
      if (!current?.items.some(s => s.id === update.shipmentId)) return;
      this.page.set({
        ...current,
        items: current.items.map(s => s.id === update.shipmentId
          ? { ...s, status: update.status, isDelayed: update.isDelayed, estimatedArrival: update.estimatedArrival }
          : s),
      });
      this.flash(update.shipmentId);
    });
  }

  protected apply(change: Partial<ShipmentFilter>, resetPage = true): void {
    this.filter.update(f => ({ ...f, ...change, ...(resetPage ? { page: 1 } : {}) }));
    this.load();
  }

  protected open(id: number): void {
    void this.router.navigate(['/shipments', id]);
  }

  private load(): void {
    this.loading.set(true);
    this.api.shipments(this.filter()).subscribe({
      next: page => { this.page.set(page); this.loading.set(false); this.error.set(null); },
      error: () => { this.loading.set(false); this.error.set('Could not load shipments.'); },
    });
  }

  private flash(id: number): void {
    this.flashing.update(s => new Set(s).add(id));
    setTimeout(() => this.flashing.update(s => { const next = new Set(s); next.delete(id); return next; }), 1200);
  }
}
