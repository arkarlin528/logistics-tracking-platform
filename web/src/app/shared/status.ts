import { Component, computed, input, Pipe, PipeTransform } from '@angular/core';
import { ShipmentStatus } from '../core/models';

const LABELS: Record<ShipmentStatus, string> = {
  Booked: 'Booked',
  PickedUp: 'Picked up',
  InTransit: 'In transit',
  ArrivedAtPort: 'Arrived at port',
  CustomsClearance: 'Customs clearance',
  OutForDelivery: 'Out for delivery',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
};

export function statusLabel(status: ShipmentStatus): string {
  return LABELS[status] ?? status;
}

@Pipe({ name: 'statusLabel' })
export class StatusLabelPipe implements PipeTransform {
  transform(status: ShipmentStatus): string {
    return statusLabel(status);
  }
}

@Component({
  selector: 'app-status-badge',
  template: `
    <span class="badge" [class]="'badge s-' + status()">{{ label() }}</span>
    @if (delayed()) {
      <span class="badge badge-delayed">Delayed</span>
    }
  `,
  styles: `:host { display: inline-flex; gap: 4px; flex-wrap: wrap; }`,
})
export class StatusBadge {
  readonly status = input.required<ShipmentStatus>();
  readonly delayed = input(false);
  readonly label = computed(() => statusLabel(this.status()));
}
