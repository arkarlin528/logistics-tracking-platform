import { AfterViewInit, Component, ElementRef, effect, input, OnDestroy, output, viewChild } from '@angular/core';
import * as L from 'leaflet';

export interface MapMarker {
  id: number;
  lat: number;
  lng: number;
  label: string;
  /** CSS colour token name, e.g. 'accent' or 'danger'. */
  tone: 'accent' | 'danger' | 'muted' | 'success';
}

export interface MapRoute {
  from: [number, number];
  to: [number, number];
  travelled?: [number, number][];
}

/**
 * Shifts a longitude by ±360 so it sits within 180° of a reference. Lets a route like
 * Los Angeles → Port Klang be drawn as one continuous line across the Pacific.
 */
export function unwrapLongitude(reference: number, lng: number): number {
  let delta = (lng - reference) % 360;
  if (delta > 180) delta -= 360;
  if (delta <= -180) delta += 360;
  return reference + delta;
}

const TONES: Record<MapMarker['tone'], string> = {
  accent: '#2563eb',
  danger: '#dc2626',
  muted: '#64748b',
  success: '#16a34a',
};

/**
 * Thin Leaflet wrapper. Markers and routes are inputs, so parents just pass data and the map
 * redraws when SignalR updates arrive. Circle markers avoid Leaflet's image-icon bundling issues.
 */
@Component({
  selector: 'app-map',
  template: `<div #host class="map-host"></div>`,
  styles: `
    :host { display: block; }
    .map-host { height: 100%; min-height: 280px; border-radius: var(--radius); overflow: hidden; }
  `,
})
export class MapView implements AfterViewInit, OnDestroy {
  readonly markers = input<MapMarker[]>([]);
  readonly route = input<MapRoute | null>(null);
  /** Fit the view to the markers/route whenever the data changes (off for the live map, so it doesn't jump). */
  readonly autoFit = input(true);
  readonly markerClick = output<number>();

  private readonly host = viewChild.required<ElementRef<HTMLDivElement>>('host');
  private map: L.Map | null = null;
  private layer = L.layerGroup();
  private fitted = false;
  private bounds: L.LatLngBounds | null = null;
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    effect(() => {
      const markers = this.markers();
      const route = this.route();
      if (this.map) this.draw(markers, route);
    });
  }

  ngAfterViewInit(): void {
    this.map = L.map(this.host().nativeElement, { worldCopyJump: true, zoomControl: true }).setView([10, 105], 4);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 18,
      attribution: '&copy; OpenStreetMap contributors',
    }).addTo(this.map);
    this.layer.addTo(this.map);
    this.draw(this.markers(), this.route());

    // Leaflet measures its container once. Re-measure whenever the layout settles or changes,
    // and redo the initial fit, otherwise tiles render in a strip and the view is off-centre.
    this.resizeObserver = new ResizeObserver(() => {
      if (!this.map) return;
      this.map.invalidateSize();
      if (this.bounds && (this.autoFit() || !this.fitted)) this.fit();
    });
    this.resizeObserver.observe(this.host().nativeElement);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.map?.remove();
    this.map = null;
  }

  private fit(): void {
    const element = this.host().nativeElement;
    if (!this.map || !this.bounds || element.clientWidth === 0 || element.clientHeight === 0) return;
    this.map.fitBounds(this.bounds, { padding: [40, 40], maxZoom: 6, animate: false });
    this.fitted = true;
  }

  private draw(markers: MapMarker[], route: MapRoute | null): void {
    this.layer.clearLayers();
    const bounds: L.LatLngExpression[] = [];

    if (route) {
      L.polyline([route.from, route.to], { color: '#94a3b8', weight: 2, dashArray: '6 6' }).addTo(this.layer);
      if (route.travelled?.length) {
        L.polyline(route.travelled, { color: TONES.accent, weight: 3 }).addTo(this.layer);
      }
      for (const point of [route.from, route.to]) {
        L.circleMarker(point, { radius: 5, color: '#334155', weight: 2, fillColor: '#fff', fillOpacity: 1 }).addTo(this.layer);
      }
      bounds.push(route.from, route.to);
    }

    for (const marker of markers) {
      const circle = L.circleMarker([marker.lat, marker.lng], {
        radius: 8,
        color: '#fff',
        weight: 2,
        fillColor: TONES[marker.tone],
        fillOpacity: 0.95,
      })
        .bindTooltip(marker.label)
        .on('click', () => this.markerClick.emit(marker.id));
      circle.addTo(this.layer);
      bounds.push([marker.lat, marker.lng]);
    }

    this.bounds = bounds.length ? L.latLngBounds(bounds) : null;
    if (this.autoFit() || !this.fitted) this.fit();
  }
}
