import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, problemMessage } from '../../core/api.service';
import { Carrier, Customer, Port } from '../../core/models';

@Component({
  selector: 'app-new-shipment',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <a routerLink="/shipments" class="back">← All shipments</a>
    <div class="page-head"><h1>New shipment</h1></div>

    <form class="card" [formGroup]="form" (ngSubmit)="submit()">
      <label>Customer
        <select formControlName="customerId">
          @for (c of customers(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
        </select>
      </label>
      <label>Carrier
        <select formControlName="carrierId">
          @for (c of carriers(); track c.id) { <option [ngValue]="c.id">{{ c.name }} ({{ c.mode }})</option> }
        </select>
      </label>
      <label>Origin
        <select formControlName="originPortCode">
          @for (p of ports(); track p.code) { <option [value]="p.code">{{ p.name }} ({{ p.code }})</option> }
        </select>
      </label>
      <label>Destination
        <select formControlName="destinationPortCode">
          @for (p of ports(); track p.code) { <option [value]="p.code">{{ p.name }} ({{ p.code }})</option> }
        </select>
      </label>
      <label class="wide">Cargo description
        <input formControlName="cargoDescription" placeholder="e.g. Jasmine rice, 20 pallets" />
      </label>
      <label>Weight (kg) <input type="number" formControlName="weightKg" min="1" /></label>
      <label>Estimated arrival <input type="datetime-local" formControlName="estimatedArrival" /></label>

      @if (error()) { <div class="error wide">{{ error() }}</div> }

      <div class="wide buttons">
        <a class="btn btn-ghost" routerLink="/shipments">Cancel</a>
        <button class="btn" type="submit" [disabled]="form.invalid || busy()">Book shipment</button>
      </div>
    </form>
  `,
  styles: `
    .back { display: inline-block; margin-bottom: 12px; text-decoration: none; }
    form { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; max-width: 760px; }
    .wide { grid-column: 1 / -1; }
    .buttons { display: flex; justify-content: flex-end; gap: 8px; }
    @media (max-width: 640px) { form { grid-template-columns: 1fr; } }
  `,
})
export class NewShipmentPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  protected readonly customers = signal<Customer[]>([]);
  protected readonly carriers = signal<Carrier[]>([]);
  protected readonly ports = signal<Port[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    customerId: [0, Validators.min(1)],
    carrierId: [0, Validators.min(1)],
    originPortCode: ['THLCH', Validators.required],
    destinationPortCode: ['SGSIN', Validators.required],
    cargoDescription: ['', [Validators.required, Validators.maxLength(500)]],
    weightKg: [1000, [Validators.required, Validators.min(1)]],
    estimatedArrival: [toLocalInput(new Date(Date.now() + 7 * 86_400_000)), Validators.required],
  });

  ngOnInit(): void {
    forkJoin([this.api.customers(), this.api.carriers(), this.api.ports()]).subscribe(([customers, carriers, ports]) => {
      this.customers.set(customers);
      this.carriers.set(carriers);
      this.ports.set(ports);
      this.form.patchValue({ customerId: customers[0]?.id ?? 0, carrierId: carriers[0]?.id ?? 0 });
    });
  }

  protected submit(): void {
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api.createShipment({ ...value, estimatedArrival: new Date(value.estimatedArrival).toISOString() }).subscribe({
      next: created => void this.router.navigate(['/shipments', created.id]),
      error: e => { this.busy.set(false); this.error.set(problemMessage(e)); },
    });
  }
}

function toLocalInput(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
