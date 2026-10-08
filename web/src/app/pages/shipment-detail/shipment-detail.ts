import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { ApiService, problemMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ShipmentDetail, ShipmentStatus } from '../../core/models';
import { TrackingService } from '../../core/tracking.service';
import { MapMarker, MapRoute, MapView, unwrapLongitude } from '../../shared/map';
import { statusLabel, StatusBadge, StatusLabelPipe } from '../../shared/status';

@Component({
  selector: 'app-shipment-detail',
  imports: [RouterLink, DatePipe, DecimalPipe, FormsModule, MapView, StatusBadge, StatusLabelPipe],
  template: `
    <a routerLink="/shipments" class="back">← All shipments</a>

    @if (shipment(); as s) {
      <div class="page-head">
        <div>
          <h1 class="mono">{{ s.reference }}</h1>
          <p>{{ s.origin.name }} → {{ s.destination.name }} · {{ s.mode }} · {{ s.carrierName }}</p>
        </div>
        <app-status-badge [status]="s.status" [delayed]="s.isDelayed" />
      </div>

      <div class="layout">
        <div class="grid">
          <section class="card map-card">
            <app-map [markers]="markers()" [route]="route()" />
          </section>

          <section class="card facts">
            <div><span>Customer</span><strong>{{ s.customerName }}</strong></div>
            <div><span>Cargo</span><strong>{{ s.cargoDescription }}</strong></div>
            <div><span>Weight</span><strong>{{ s.weightKg | number }} kg</strong></div>
            <div><span>Booked</span><strong>{{ s.createdAt | date: 'd MMM y, HH:mm' }}</strong></div>
            <div><span>ETA</span><strong [class.late]="s.isDelayed">{{ s.estimatedArrival | date: 'd MMM y, HH:mm' }}</strong></div>
            @if (s.deliveredAt) {
              <div><span>Delivered</span><strong>{{ s.deliveredAt | date: 'd MMM y, HH:mm' }}</strong></div>
            }
          </section>

          @if (auth.isStaff() && actions().length) {
            <section class="card">
              <h2>Update shipment</h2>
              <div class="actions">
                @for (next of actions(); track next) {
                  <button class="btn" [class.btn-danger]="next === 'Cancelled'" [disabled]="busy()" (click)="record(next)">
                    {{ actionLabel(next) }}
                  </button>
                }
              </div>
              <label class="note">Note <small>(optional)</small>
                <input [(ngModel)]="note" placeholder="e.g. Loaded on vessel" />
              </label>

              <details>
                <summary>Change ETA</summary>
                <div class="eta">
                  <input type="datetime-local" [(ngModel)]="newEta" />
                  <input [(ngModel)]="etaReason" placeholder="Reason, e.g. port congestion" />
                  <button class="btn btn-ghost" [disabled]="busy() || !newEta || !etaReason" (click)="reviseEta()">Save ETA</button>
                </div>
              </details>
              @if (actionError()) {
                <div class="error">{{ actionError() }}</div>
              }
            </section>
          }
        </div>

        <section class="card timeline">
          <h2>History</h2>
          <ol>
            @for (e of s.events; track e.id) {
              <li [class.fresh]="e.id === freshEventId()">
                <span [class]="'pin s-' + e.status"></span>
                <div>
                  <strong>{{ e.status | statusLabel }}</strong>
                  <span class="muted"> · {{ e.occurredAt | date: 'd MMM, HH:mm' }}</span>
                  @if (e.location) { <div>{{ e.location }}</div> }
                  @if (e.note) { <div class="muted">{{ e.note }}</div> }
                </div>
              </li>
            }
          </ol>
        </section>
      </div>
    } @else if (loadError()) {
      <div class="error">{{ loadError() }}</div>
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .back { display: inline-block; margin-bottom: 12px; text-decoration: none; }
    .layout { display: grid; grid-template-columns: 1.6fr 1fr; gap: 16px; align-items: start; }
    .map-card { padding: 0; height: 360px; }
    .map-card app-map { height: 100%; }
    .facts { display: grid; grid-template-columns: repeat(3, 1fr); gap: 14px; }
    .facts span { display: block; color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .04em; }
    .late { color: var(--danger); }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
    .note { margin-bottom: 12px; }
    details summary { cursor: pointer; color: var(--accent); font-weight: 600; margin-bottom: 8px; }
    .eta { display: grid; grid-template-columns: 200px 1fr auto; gap: 8px; margin-bottom: 8px; }
    .timeline { max-height: calc(100vh - 160px); overflow-y: auto; }
    ol { list-style: none; margin: 0; padding: 0; }
    li { display: grid; grid-template-columns: 18px 1fr; gap: 10px; padding: 10px 0; border-bottom: 1px solid var(--border); }
    li.fresh { animation: fresh 2s ease-out; }
    @keyframes fresh { from { background: #fef9c3; } to { background: transparent; } }
    .pin { width: 12px; height: 12px; border-radius: 50%; margin-top: 5px; border: 2px solid currentColor; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } .facts { grid-template-columns: 1fr 1fr; } .eta { grid-template-columns: 1fr; } }
  `,
})
export class ShipmentDetailPage {
  private readonly api = inject(ApiService);
  private readonly tracking = inject(TrackingService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  /** Bound from the :id route parameter. */
  readonly id = input.required<string>();

  protected readonly shipment = signal<ShipmentDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly freshEventId = signal<number | null>(null);
  protected note = '';
  protected newEta = '';
  protected etaReason = '';

  /** Position pings (same status) come from carrier feeds, so they aren't offered as a manual action. */
  protected readonly actions = computed(() => {
    const s = this.shipment();
    return s ? s.nextStatuses.filter(next => next !== s.status) : [];
  });

  protected actionLabel(next: ShipmentStatus): string {
    if (next === 'InTransit' && this.shipment()?.status === 'ArrivedAtPort') return 'Depart again (transshipment)';
    if (next === 'Cancelled') return 'Cancel shipment';
    return 'Mark ' + statusLabel(next).toLowerCase();
  }

  protected readonly markers = computed<MapMarker[]>(() => {
    const s = this.shipment();
    if (s?.currentLatitude == null || s.currentLongitude == null) return [];
    return [{
      id: s.id, lat: s.currentLatitude, lng: unwrapLongitude(s.origin.longitude, s.currentLongitude),
      label: `${s.reference} · ${s.status}`,
      tone: s.isDelayed ? 'danger' : s.status === 'Delivered' ? 'success' : 'accent',
    }];
  });

  protected readonly route = computed<MapRoute | null>(() => {
    const s = this.shipment();
    if (!s) return null;
    // Unwrap longitudes relative to the origin so routes over the date line draw as one line.
    const ref = s.origin.longitude;
    const travelled = s.events
      .filter(e => e.latitude != null && e.longitude != null)
      .map(e => [e.latitude!, unwrapLongitude(ref, e.longitude!)] as [number, number])
      .reverse();
    return {
      from: [s.origin.latitude, ref],
      to: [s.destination.latitude, unwrapLongitude(ref, s.destination.longitude)],
      travelled,
    };
  });

  constructor() {
    effect(() => this.load(Number(this.id())));

    // Live: reload this shipment when the hub says it changed (from another user or the simulator).
    this.tracking.updates
      .pipe(filter(u => u.shipmentId === Number(this.id())), takeUntilDestroyed(this.destroyRef))
      .subscribe(update => {
        this.freshEventId.set(update.latestEvent.id);
        this.load(update.shipmentId);
      });
  }

  protected record(status: ShipmentStatus): void {
    const s = this.shipment();
    if (!s) return;
    // Arriving puts the shipment at the destination port on the map.
    const at = status === 'ArrivedAtPort' ? s.destination : null;
    this.run(this.api.recordEvent(s.id, {
      status, note: this.note || null, location: at?.name ?? null, latitude: at?.latitude ?? null, longitude: at?.longitude ?? null,
    }));
  }

  protected reviseEta(): void {
    const s = this.shipment();
    if (!s) return;
    this.run(this.api.reviseEta(s.id, new Date(this.newEta).toISOString(), this.etaReason));
  }

  private run(request: ReturnType<ApiService['recordEvent']>): void {
    this.busy.set(true);
    this.actionError.set(null);
    request.subscribe({
      next: updated => {
        this.shipment.set(updated);
        this.busy.set(false);
        this.note = '';
        this.newEta = '';
        this.etaReason = '';
      },
      error: e => {
        this.busy.set(false);
        this.actionError.set(problemMessage(e));
        this.load(Number(this.id())); // e.g. after a 409, show the latest state
      },
    });
  }

  private load(id: number): void {
    this.api.shipment(id).subscribe({
      next: s => this.shipment.set(s),
      error: e => this.loadError.set(problemMessage(e)),
    });
  }
}
