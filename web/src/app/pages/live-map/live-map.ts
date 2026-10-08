import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ShipmentSummary } from '../../core/models';
import { TrackingService } from '../../core/tracking.service';
import { MapMarker, MapView } from '../../shared/map';

@Component({
  selector: 'app-live-map',
  imports: [MapView],
  template: `
    <div class="page-head">
      <div>
        <h1>Live map</h1>
        <p>{{ active().length }} shipments on the move · <span class="legend accent"></span> on time
          <span class="legend danger"></span> delayed · click a dot for details</p>
      </div>
    </div>
    <div class="card map-card">
      <app-map [markers]="markers()" [autoFit]="false" (markerClick)="open($event)" />
    </div>
  `,
  styles: `
    .map-card { padding: 0; height: calc(100vh - 170px); min-height: 420px; }
    .map-card app-map { height: 100%; }
    .legend { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin: 0 4px 0 8px; }
    .legend.accent { background: var(--accent); }
    .legend.danger { background: var(--danger); }
  `,
})
export class LiveMapPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly tracking = inject(TrackingService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly active = signal<ShipmentSummary[]>([]);
  protected readonly markers = computed<MapMarker[]>(() => this.active()
    .filter(s => s.currentLatitude != null && s.currentLongitude != null)
    .map(s => ({
      id: s.id,
      lat: s.currentLatitude!,
      lng: s.currentLongitude!,
      label: `${s.reference} · ${s.originCode} → ${s.destinationCode}`,
      tone: s.isDelayed ? 'danger' : 'accent',
    })));

  ngOnInit(): void {
    this.load();
    this.tracking.updates.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(update => {
      const finished = update.status === 'Delivered' || update.status === 'Cancelled';
      const known = this.active().some(s => s.id === update.shipmentId);
      if (!known && !finished) {
        this.load(); // a newly booked shipment
        return;
      }
      this.active.update(list => list
        .filter(s => !(finished && s.id === update.shipmentId))
        .map(s => s.id === update.shipmentId
          ? { ...s, status: update.status, isDelayed: update.isDelayed,
              currentLatitude: update.currentLatitude, currentLongitude: update.currentLongitude }
          : s));
    });
  }

  protected open(id: number): void {
    void this.router.navigate(['/shipments', id]);
  }

  private load(): void {
    // Delivered/cancelled shipments are filtered out client-side: the list endpoint is paged and 100 is plenty for the demo.
    this.api.shipments({ pageSize: 100 }).subscribe(page =>
      this.active.set(page.items.filter(s => s.status !== 'Delivered' && s.status !== 'Cancelled')));
  }
}
